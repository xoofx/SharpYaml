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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed > byte.MaxValue)
        {
            throw YamlThrowHelper.ThrowInvalidByteScalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed is < sbyte.MinValue or > sbyte.MaxValue)
        {
            throw YamlThrowHelper.ThrowInvalidSByteScalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed is < short.MinValue or > short.MaxValue)
        {
            throw YamlThrowHelper.ThrowInvalidInt16Scalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed > ushort.MaxValue)
        {
            throw YamlThrowHelper.ThrowInvalidUInt16Scalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseUInt32(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw YamlThrowHelper.ThrowInvalidUInt32Scalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw YamlThrowHelper.ThrowInvalidUInt64Scalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        var text = reader.ScalarValue ?? string.Empty;
        if (text.Length != 1)
        {
            throw YamlThrowHelper.ThrowInvalidCharScalar(ref reader, text);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseDecimal(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw YamlThrowHelper.ThrowInvalidDecimalScalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw YamlThrowHelper.ThrowInvalidNIntScalar(ref reader);
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
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        if (!YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw YamlThrowHelper.ThrowInvalidNUIntScalar(ref reader);
        }

        reader.Read();
        return (nuint)parsed;
    }

    public override void Write(YamlWriter writer, nuint value, YamlSerializerOptions options)
        => writer.WriteScalar(((ulong)value).ToString(CultureInfo.InvariantCulture));
}
