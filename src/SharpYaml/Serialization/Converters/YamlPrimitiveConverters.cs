using System;
using System.Globalization;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlByteConverter : YamlConverter<byte>
{
    public static YamlByteConverter Instance { get; } = new();

    public override byte Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed > byte.MaxValue)
        {
            throw new FormatException($"Invalid byte scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (byte)parsed;
    }

    public override void Write(YamlWriter writer, byte value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlSByteConverter : YamlConverter<sbyte>
{
    public static YamlSByteConverter Instance { get; } = new();

    public override sbyte Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw new FormatException($"Invalid sbyte scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (sbyte)parsed;
    }

    public override void Write(YamlWriter writer, sbyte value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlInt16Converter : YamlConverter<short>
{
    public static YamlInt16Converter Instance { get; } = new();

    public override short Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed is < short.MinValue or > short.MaxValue)
        {
            throw new FormatException($"Invalid int16 scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (short)parsed;
    }

    public override void Write(YamlWriter writer, short value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlUInt16Converter : YamlConverter<ushort>
{
    public static YamlUInt16Converter Instance { get; } = new();

    public override ushort Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed > ushort.MaxValue)
        {
            throw new FormatException($"Invalid uint16 scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (ushort)parsed;
    }

    public override void Write(YamlWriter writer, ushort value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlUInt32Converter : YamlConverter<uint>
{
    public static YamlUInt32Converter Instance { get; } = new();

    public override uint Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseUInt32(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw new FormatException($"Invalid uint32 scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return parsed;
    }

    public override void Write(YamlWriter writer, uint value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlUInt64Converter : YamlConverter<ulong>
{
    public static YamlUInt64Converter Instance { get; } = new();

    public override ulong Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw new FormatException($"Invalid uint64 scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return parsed;
    }

    public override void Write(YamlWriter writer, ulong value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlCharConverter : YamlConverter<char>
{
    public static YamlCharConverter Instance { get; } = new();

    public override char Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        var text = reader.ScalarValue ?? string.Empty;
        if (text.Length != 1)
        {
            throw new FormatException($"Invalid char scalar '{text}'.");
        }

        reader.Read();
        return text[0];
    }

    public override void Write(YamlWriter writer, char value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString());
}

internal sealed class YamlDecimalConverter : YamlConverter<decimal>
{
    public static YamlDecimalConverter Instance { get; } = new();

    public override decimal Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseDecimal(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw new FormatException($"Invalid decimal scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return parsed;
    }

    public override void Write(YamlWriter writer, decimal value, YamlSerializerOptions options)
        => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlIntPtrConverter : YamlConverter<nint>
{
    public static YamlIntPtrConverter Instance { get; } = new();

    public override nint Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw new FormatException($"Invalid nint scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (nint)parsed;
    }

    public override void Write(YamlWriter writer, nint value, YamlSerializerOptions options)
        => writer.WriteScalar(((long)value).ToString(CultureInfo.InvariantCulture));
}

internal sealed class YamlUIntPtrConverter : YamlConverter<nuint>
{
    public static YamlUIntPtrConverter Instance { get; } = new();

    public override nuint Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        if (!YamlScalarParser.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw new FormatException($"Invalid nuint scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (nuint)parsed;
    }

    public override void Write(YamlWriter writer, nuint value, YamlSerializerOptions options)
        => writer.WriteScalar(((ulong)value).ToString(CultureInfo.InvariantCulture));
}

