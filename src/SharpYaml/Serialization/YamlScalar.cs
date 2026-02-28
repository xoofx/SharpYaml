using System;
using System.Globalization;

namespace SharpYaml.Serialization;

/// <summary>
/// Provides YAML scalar parsing helpers aligned with the YAML 1.2 core schema conventions used by <see cref="SharpYaml.YamlSerializer"/>.
/// </summary>
public static class YamlScalar
{
    /// <summary>
    /// Determines whether a scalar represents YAML null (for example <c>null</c> or <c>~</c>).
    /// </summary>
    public static bool IsNull(ReadOnlySpan<char> value)
    {
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

        // Remove underscores (allocate to avoid stack-spans escaping analysis).
        ReadOnlySpan<char> cleaned = value;
        if (value.Contains('_'))
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

        return ulong.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out result);
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

        if (value.Contains('_'))
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
            return decimal.TryParse(underscoreRemoved.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Parses a YAML integer scalar into <see cref="long"/>, including common base prefixes (<c>0x</c>, <c>0o</c>, <c>0b</c>) and underscores.
    /// </summary>
    public static bool TryParseInt64(ReadOnlySpan<char> value, out long result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        // Remove underscores (allocate to avoid stack-spans escaping analysis).
        ReadOnlySpan<char> cleaned = value;
        string? underscoreRemoved = null;
        if (value.Contains('_'))
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

        if (!ulong.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude))
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

        if (value.Contains('_'))
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
            return double.TryParse(underscoreRemovedDouble.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
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

            checked
            {
                accumulator = (accumulator * (ulong)numberBase) + (ulong)digit;
            }
        }

        result = accumulator;
        return true;
    }
}
