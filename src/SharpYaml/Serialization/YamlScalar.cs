// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using System.Numerics;
using SharpYaml.Schemas;
using ScalarEvent = SharpYaml.Events.Scalar;

namespace SharpYaml.Serialization;

/// <summary>
/// Provides YAML scalar parsing helpers aligned with the YAML 1.2 core schema conventions used by <see cref="SharpYaml.YamlSerializer"/>.
/// </summary>
public static class YamlScalar
{
    private static readonly ExtendedSchema s_extendedSchema = new();
    private static readonly FailsafeSchema s_failsafeSchema = new();
    private static readonly JsonSchema s_jsonSchema = new();

    // decimal.MaxValue (2^96 - 1) rounds up to 2^96 as a double: a double in the decimal range is strictly between these bounds.
    private const double DecimalMinValueAsDouble = (double)decimal.MinValue;
    private const double DecimalMaxValueAsDouble = (double)decimal.MaxValue;

    /// <summary>
    /// Determines whether a scalar represents YAML null (for example <c>null</c> or <c>~</c>).
    /// </summary>
    public static bool IsNull(ReadOnlySpan<char> value)
    {
        // Null is empty, "~" or "null" once trimmed: a value that starts with any other visible ASCII character is not.
        if (value.Length != 0 && value[0] is > ' ' and < '\x7F' and not ('n' or 'N' or '~'))
        {
            return false;
        }

        value = Trim(value);
        if (value.Length == 0)
        {
            return true;
        }

        if (value.Length == 1 && value[0] == '~')
        {
            return true;
        }

        return value.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a YAML boolean scalar (<c>true</c>/<c>false</c>).
    /// </summary>
    public static bool TryParseBool(ReadOnlySpan<char> value, out bool result)
    {
        value = Trim(value);
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parses a YAML integer scalar into <see cref="int"/>.
    /// </summary>
    public static bool TryParseInt32(ReadOnlySpan<char> value, out int result)
    {
        if (TryParseInt64(value, out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parses a YAML integer scalar into <see cref="uint"/>.
    /// </summary>
    public static bool TryParseUInt32(ReadOnlySpan<char> value, out uint result)
    {
        if (TryParseUInt64(value, out var ulongValue) && ulongValue <= uint.MaxValue)
        {
            result = (uint)ulongValue;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parses a YAML integer scalar into <see cref="ulong"/>, including common base prefixes (<c>0x</c>, <c>0o</c>, <c>0b</c>) and underscores.
    /// </summary>
    public static bool TryParseUInt64(ReadOnlySpan<char> value, out ulong result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        if (value[0] == '-')
        {
            result = default;
            return false;
        }

        ReadOnlySpan<char> cleaned = value;
        if (ContainsChar(value, '_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            var underscoreRemoved = new string(buffer, 0, written);
            cleaned = underscoreRemoved.AsSpan();
        }

        if (cleaned.Length > 0 && cleaned[0] == '+')
        {
            cleaned = cleaned.Slice(1);
        }

        if (cleaned.Length >= 2 && cleaned[0] == '0')
        {
            var prefix = cleaned[1];
            if (prefix is 'x' or 'X')
            {
                return TryParseUInt64Base(cleaned.Slice(2), 16, out result);
            }

            if (prefix is 'o' or 'O')
            {
                return TryParseUInt64Base(cleaned.Slice(2), 8, out result);
            }

            if (prefix is 'b' or 'B')
            {
                return TryParseUInt64Base(cleaned.Slice(2), 2, out result);
            }
        }

#if NETSTANDARD2_0
        return ulong.TryParse(cleaned.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out result);
#else
        return ulong.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out result);
#endif
    }

    /// <summary>
    /// Parses a YAML floating-point scalar into <see cref="decimal"/>.
    /// </summary>
    public static bool TryParseDecimal(ReadOnlySpan<char> value, out decimal result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        if (value.Equals(".inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("+.inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("-.inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
        {
            result = default;
            return false;
        }

        if (ContainsChar(value, '_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            var underscoreRemoved = new string(buffer, 0, written);
            return decimal.TryParse(underscoreRemoved, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

#if NETSTANDARD2_0
        return decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
    }

    /// <summary>
    /// Parses the most common integer directly: an optional <c>-</c> and one to eighteen decimal digits, which cannot
    /// overflow and has no whitespace to trim, no underscore and no base prefix. Anything else is left to the caller.
    /// </summary>
    private static bool TryParseShortDecimal(ReadOnlySpan<char> value, out long result)
    {
        var index = value.Length != 0 && value[0] == '-' ? 1 : 0;
        if ((uint)(value.Length - index - 1) > 17)
        {
            result = 0;
            return false;
        }

        long magnitude = 0;
        for (var i = index; i < value.Length; i++)
        {
            var digit = (uint)(value[i] - '0');
            if (digit > 9)
            {
                result = 0;
                return false;
            }

            magnitude = (magnitude * 10) + digit;
        }

        result = index == 0 ? magnitude : -magnitude;
        return true;
    }

    /// <summary>
    /// Parses a YAML integer scalar into <see cref="long"/>, including common base prefixes (<c>0x</c>, <c>0o</c>, <c>0b</c>) and underscores.
    /// </summary>
    public static bool TryParseInt64(ReadOnlySpan<char> value, out long result)
    {
        if (TryParseShortDecimal(value, out result))
        {
            return true;
        }

        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        ReadOnlySpan<char> cleaned = value;
        string? underscoreRemoved = null;
        if (ContainsChar(value, '_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            underscoreRemoved = new string(buffer, 0, written);
            cleaned = underscoreRemoved.AsSpan();
        }

        var sign = 1;
        if (cleaned.Length > 0 && (cleaned[0] == '+' || cleaned[0] == '-'))
        {
            if (cleaned[0] == '-')
            {
                sign = -1;
            }

            cleaned = cleaned.Slice(1);
        }

        if (cleaned.Length == 0)
        {
            result = default;
            return false;
        }

        ulong magnitude;
        if (cleaned.Length >= 2 && cleaned[0] == '0')
        {
            var prefix = cleaned[1];
            if (prefix is 'x' or 'X')
            {
                if (!TryParseUInt64Base(cleaned.Slice(2), 16, out magnitude))
                {
                    result = default;
                    return false;
                }

                return TryApplySignedMagnitude(magnitude, sign, out result);
            }

            if (prefix is 'o' or 'O')
            {
                if (!TryParseUInt64Base(cleaned.Slice(2), 8, out magnitude))
                {
                    result = default;
                    return false;
                }

                return TryApplySignedMagnitude(magnitude, sign, out result);
            }

            if (prefix is 'b' or 'B')
            {
                if (!TryParseUInt64Base(cleaned.Slice(2), 2, out magnitude))
                {
                    result = default;
                    return false;
                }

                return TryApplySignedMagnitude(magnitude, sign, out result);
            }
        }

#if NETSTANDARD2_0
        if (!ulong.TryParse(cleaned.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out magnitude))
#else
        if (!ulong.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude))
#endif
        {
            result = default;
            return false;
        }

        return TryApplySignedMagnitude(magnitude, sign, out result);
    }

    /// <summary>
    /// Parses a YAML floating-point scalar into <see cref="double"/>, including <c>.inf</c> and <c>.nan</c>.
    /// </summary>
    public static bool TryParseDouble(ReadOnlySpan<char> value, out double result)
    {
        value = Trim(value);

        // The infinities and not-a-number start with a dot or a sign.
        if (value.Length != 0 && value[0] is '.' or '+' or '-')
        {
            if (value.Equals(".inf", StringComparison.OrdinalIgnoreCase) || value.Equals("+.inf", StringComparison.OrdinalIgnoreCase))
            {
                result = double.PositiveInfinity;
                return true;
            }

            if (value.Equals("-.inf", StringComparison.OrdinalIgnoreCase))
            {
                result = double.NegativeInfinity;
                return true;
            }

            if (value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
            {
                result = double.NaN;
                return true;
            }
        }

        if (ContainsChar(value, '_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            var underscoreRemovedDouble = new string(buffer, 0, written);
            return double.TryParse(underscoreRemovedDouble, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

#if NETSTANDARD2_0
        return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
    }

    /// <summary>
    /// Determines whether the current scalar token represents YAML null while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    public static bool IsNull(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            return false;
        }

        if (reader.Options.UseSchema)
        {
            return TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                   string.Equals(defaultTag, JsonSchema.NullShortTag, StringComparison.Ordinal) &&
                   value is null;
        }

        if (HasStringTag(reader))
        {
            return false;
        }

        return IsNull(reader.ScalarValueSpan, reader.ScalarStyle);
    }

    /// <summary>
    /// Parses the current scalar token as a YAML boolean while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed boolean value.</param>
    public static bool TryParseBool(YamlReader reader, out bool result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                string.Equals(defaultTag, JsonSchema.BoolShortTag, StringComparison.Ordinal) &&
                value is bool boolean)
            {
                result = boolean;
                return true;
            }

            result = default;
            return false;
        }

        return TryParseBool(reader.ScalarValueSpan, out result);
    }

    /// <summary>
    /// Parses the current scalar token as a YAML integer while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed integer value.</param>
    public static bool TryParseInt32(YamlReader reader, out int result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                string.Equals(defaultTag, JsonSchema.IntShortTag, StringComparison.Ordinal) &&
                TryConvertToInt64(value, out var parsed) &&
                parsed is >= int.MinValue and <= int.MaxValue)
            {
                result = (int)parsed;
                return true;
            }

            result = default;
            return false;
        }

        return TryParseInt32(reader.ScalarValueSpan, out result);
    }

    /// <summary>
    /// Parses the current scalar token as an unsigned YAML integer while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed unsigned integer value.</param>
    public static bool TryParseUInt32(YamlReader reader, out uint result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                string.Equals(defaultTag, JsonSchema.IntShortTag, StringComparison.Ordinal) &&
                TryConvertToUInt64(value, out var parsed) &&
                parsed <= uint.MaxValue)
            {
                result = (uint)parsed;
                return true;
            }

            result = default;
            return false;
        }

        return TryParseUInt32(reader.ScalarValueSpan, out result);
    }

    /// <summary>
    /// Parses the current scalar token as an unsigned YAML integer while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed unsigned integer value.</param>
    public static bool TryParseUInt64(YamlReader reader, out ulong result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                string.Equals(defaultTag, JsonSchema.IntShortTag, StringComparison.Ordinal) &&
                TryConvertToUInt64(value, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        return TryParseUInt64(reader.ScalarValueSpan, out result);
    }

    /// <summary>
    /// Parses the current scalar token as a YAML integer while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed integer value.</param>
    public static bool TryParseInt64(YamlReader reader, out long result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                string.Equals(defaultTag, JsonSchema.IntShortTag, StringComparison.Ordinal) &&
                TryConvertToInt64(value, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        return TryParseInt64(reader.ScalarValueSpan, out result);
    }

    /// <summary>
    /// Parses the current scalar token as a YAML floating-point value while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed floating-point value.</param>
    public static bool TryParseDouble(YamlReader reader, out double result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                (string.Equals(defaultTag, JsonSchema.FloatShortTag, StringComparison.Ordinal) ||
                 string.Equals(defaultTag, JsonSchema.IntShortTag, StringComparison.Ordinal)) &&
                TryConvertToDouble(value, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        return TryParseDouble(reader.ScalarValueSpan, out result);
    }

    /// <summary>
    /// Parses the current scalar token as a YAML decimal value while honoring scalar style and <see cref="YamlSerializerOptions.UseSchema"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on a scalar token.</param>
    /// <param name="result">The parsed decimal value.</param>
    public static bool TryParseDecimal(YamlReader reader, out decimal result)
    {
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out var defaultTag, out var value) &&
                (string.Equals(defaultTag, JsonSchema.FloatShortTag, StringComparison.Ordinal) ||
                 string.Equals(defaultTag, JsonSchema.IntShortTag, StringComparison.Ordinal)))
            {
                // A float resolves to a double, which keeps only about 15 significant digits: read the decimal from what the
                // schema reads in the text instead. An explicit !!float converts an integer text to a double, and that
                // integer is not always the decimal number of its digits (017 is octal 15 in the Extended schema).
                if (value is double or float)
                {
                    if (reader.Tag is not null &&
                        GetSchema(reader.Options.Schema).TryParse(AsPlain(CreateScalar(reader)), true, out var textTag, out var textValue) &&
                        string.Equals(textTag, JsonSchema.IntShortTag, StringComparison.Ordinal))
                    {
                        value = textValue;
                    }
                    else if (TryParseDecimal(reader.ScalarValueSpan, out result))
                    {
                        return true;
                    }
                }

                if (TryConvertToDecimal(value, out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        return TryParseDecimal(reader.ScalarValueSpan, out result);
    }

    internal static object? ResolveObject(YamlReader reader)
    {
        if (reader.Tag is not null && reader.Tag != "!")
        {
            var schema = GetSchema(reader.Options.Schema);
            var shortTag = schema.ShortenTag(reader.Tag);
            if (shortTag is SchemaBase.StrShortTag or JsonSchema.BoolShortTag or JsonSchema.IntShortTag or JsonSchema.FloatShortTag or JsonSchema.NullShortTag)
            {
                if (TryResolveExplicitTag(schema, shortTag, CreateScalar(reader), out var taggedValue))
                {
                    return !reader.Options.UseSchema && shortTag == JsonSchema.IntShortTag && taggedValue is int taggedInteger
                        ? (long)taggedInteger : taggedValue;
                }
                throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Scalar is not valid for the explicit tag '{reader.Tag}'.");
            }
        }
        if (reader.Options.UseSchema)
        {
            if (TryResolveSchemaScalar(reader, out _, out var value))
            {
                return value;
            }

            return reader.ScalarValue ?? string.Empty;
        }

        if (!IsPlainStyle(reader.ScalarStyle) || HasStringTag(reader))
        {
            return reader.ScalarValue ?? string.Empty;
        }

        var text = reader.ScalarValueSpan;
        var kind = YamlCoreScalarResolver.Classify(text);
        if (kind == YamlCoreScalarResolver.Kind.Null)
        {
            return null;
        }

        if (kind == YamlCoreScalarResolver.Kind.Boolean && TryParseBool(text, out var boolean))
        {
            return boolean;
        }

        if (kind == YamlCoreScalarResolver.Kind.Integer && TryParseInt64(text, out var integer))
        {
            return integer;
        }

        if (kind == YamlCoreScalarResolver.Kind.Integer)
        {
            if (TryParseUInt64(text, out var unsigned)) return unsigned;
            return YamlCoreScalarResolver.ParseInteger(reader.ScalarValue!);
        }

        if (kind == YamlCoreScalarResolver.Kind.Float && TryParseDouble(text, out var floating))
        {
            return floating;
        }

        return reader.ScalarValue ?? string.Empty;
    }

    private static bool IsNull(ReadOnlySpan<char> value, ScalarStyle style) => IsPlainStyle(style) && IsNull(value);

    /// <summary>Parses the current scalar as an arbitrary-precision YAML Core integer.</summary>
    /// <param name="reader">The reader positioned on a scalar.</param>
    /// <param name="result">The parsed integer, or zero on failure.</param>
    /// <returns>Whether the scalar represents an integer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    public static bool TryParseBigInteger(YamlReader reader, out BigInteger result)
    {
        ArgumentGuard.ThrowIfNull(reader);
        result = BigInteger.Zero;
        if (reader.Options.UseSchema)
        {
            if (!TryResolveSchemaScalar(reader, out var tag, out var value) || tag != JsonSchema.IntShortTag) return false;
            switch (value)
            {
                case BigInteger big: result = big; return true;
                case ulong unsigned: result = unsigned; return true;
                case long signed: result = signed; return true;
                case int small: result = small; return true;
                default: return false;
            }
        }
        var text = reader.ScalarValue ?? string.Empty;
        if (YamlCoreScalarResolver.Classify(text) != YamlCoreScalarResolver.Kind.Integer) return false;
        result = YamlCoreScalarResolver.ParseInteger(text);
        return true;
    }

    private static bool HasStringTag(YamlReader reader)
    {
        if (reader.Tag == "!") return true;
        if (reader.Tag is null)
        {
            return false;
        }

        var schema = GetSchema(reader.Options.Schema);
        return string.Equals(schema.ShortenTag(reader.Tag), SchemaBase.StrShortTag, StringComparison.Ordinal);
    }

    private static bool IsPlainStyle(ScalarStyle style) => style is ScalarStyle.Any or ScalarStyle.Plain;

    private static bool TryResolveSchemaScalar(YamlReader reader, out string? defaultTag, out object? value)
    {
        if (reader.Tag == "!")
        {
            defaultTag = SchemaBase.StrShortTag;
            value = reader.ScalarValue ?? string.Empty;
            return true;
        }
        var schema = GetSchema(reader.Options.Schema);
        var scalar = CreateScalar(reader);
        if (reader.Tag is not null)
        {
            var shortTag = schema.ShortenTag(reader.Tag);
            if (shortTag is not null)
            {
                if (TryResolveExplicitTag(schema, shortTag, scalar, out value))
                {
                    defaultTag = shortTag;
                    return true;
                }

                defaultTag = shortTag;
                value = null;
                return false;
            }
        }

        return schema.TryParse(scalar, true, out defaultTag, out value);
    }

    private static bool TryResolveExplicitTag(IYamlSchema schema, string shortTag, ScalarEvent scalar, out object? value)
    {
        if (string.Equals(shortTag, SchemaBase.StrShortTag, StringComparison.Ordinal))
        {
            value = scalar.Value;
            return true;
        }

        if (schema.TryParse(AsPlain(scalar), true, out var resolvedTag, out value) && string.Equals(resolvedTag, shortTag, StringComparison.Ordinal))
        {
            return true;
        }

        if (shortTag == JsonSchema.FloatShortTag && resolvedTag == JsonSchema.IntShortTag && value is not null)
        {
            value = value is BigInteger large ? (double)large : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }

        if (string.Equals(shortTag, JsonSchema.NullShortTag, StringComparison.Ordinal) && IsNull(scalar.Value.AsSpan()))
        {
            value = null;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Returns <paramref name="scalar"/> as a plain scalar, so that the schema resolves its text whatever its quotes.</summary>
    private static ScalarEvent AsPlain(ScalarEvent scalar)
        => IsPlainStyle(scalar.Style)
            ? scalar
            : new ScalarEvent(
                scalar.Anchor,
                scalar.Tag,
                scalar.Value,
                ScalarStyle.Plain,
                scalar.IsPlainImplicit,
                scalar.IsQuotedImplicit,
                scalar.Start,
                scalar.End);

    private static ScalarEvent CreateScalar(YamlReader reader)
        => new(
            reader.Anchor,
            reader.Tag,
            reader.ScalarValue ?? string.Empty,
            reader.ScalarStyle,
            isPlainImplicit: IsPlainStyle(reader.ScalarStyle),
            isQuotedImplicit: !IsPlainStyle(reader.ScalarStyle),
            reader.Start,
            reader.End);

    private static IYamlSchema GetSchema(YamlSchemaKind schemaKind)
        => schemaKind switch
        {
            YamlSchemaKind.Json => s_jsonSchema,
            YamlSchemaKind.Failsafe => s_failsafeSchema,
            YamlSchemaKind.Extended => s_extendedSchema,
            _ => CoreSchema.Instance,
        };

    private static bool TryConvertToInt64(object? value, out long result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue when ulongValue <= (ulong)long.MaxValue:
                result = (long)ulongValue;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryConvertToUInt64(object? value, out ulong result)
    {
        switch (value)
        {
            case int intValue when intValue >= 0:
                result = (ulong)intValue;
                return true;
            case long longValue when longValue >= 0:
                result = (ulong)longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case BigInteger large:
                result = (double)large;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = floatValue;
                return true;
            case double doubleValue:
                result = doubleValue;
                return true;
            case decimal decimalValue:
                result = (double)decimalValue;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case BigInteger large when large >= (BigInteger)decimal.MinValue && large <= (BigInteger)decimal.MaxValue:
                result = (decimal)large;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            // NaN and infinities fail the range check too.
            case float floatValue when floatValue > DecimalMinValueAsDouble && floatValue < DecimalMaxValueAsDouble:
                result = (decimal)floatValue;
                return true;
            case double doubleValue when doubleValue > DecimalMinValueAsDouble && doubleValue < DecimalMaxValueAsDouble:
                result = (decimal)doubleValue;
                return true;
            case decimal decimalValue:
                result = decimalValue;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
    {
        while (value.Length > 0 && char.IsWhiteSpace(value[0]))
        {
            value = value.Slice(1);
        }

        while (value.Length > 0 && char.IsWhiteSpace(value[^1]))
        {
            value = value.Slice(0, value.Length - 1);
        }

        return value;
    }

    private static bool ContainsChar(ReadOnlySpan<char> value, char c)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == c)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryApplySignedMagnitude(ulong magnitude, int sign, out long result)
    {
        if (sign >= 0)
        {
            if (magnitude > (ulong)long.MaxValue)
            {
                result = default;
                return false;
            }

            result = (long)magnitude;
            return true;
        }

        var maxNegativeMagnitude = (ulong)long.MaxValue + 1;
        if (magnitude > maxNegativeMagnitude)
        {
            result = default;
            return false;
        }

        if (magnitude == maxNegativeMagnitude)
        {
            result = long.MinValue;
            return true;
        }

        result = -(long)magnitude;
        return true;
    }

    private static bool TryParseUInt64Base(ReadOnlySpan<char> value, int numberBase, out ulong result)
    {
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        ulong accumulator = 0;
        var radix = (ulong)numberBase;
        var limit = ulong.MaxValue / radix;
        var lastDigit = ulong.MaxValue % radix;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            int digit;
            if (c is >= '0' and <= '9')
            {
                digit = c - '0';
            }
            else if (c is >= 'a' and <= 'f')
            {
                digit = 10 + (c - 'a');
            }
            else if (c is >= 'A' and <= 'F')
            {
                digit = 10 + (c - 'A');
            }
            else
            {
                result = default;
                return false;
            }

            if (digit >= numberBase)
            {
                result = default;
                return false;
            }

            if (accumulator > limit || (accumulator == limit && (ulong)digit > lastDigit))
            {
                result = default;
                return false;
            }
            accumulator = (accumulator * radix) + (ulong)digit;
        }

        result = accumulator;
        return true;
    }
}
