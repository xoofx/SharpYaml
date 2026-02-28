using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlInt64Converter : YamlConverter<long>
{
    public static YamlInt64Converter Instance { get; } = new();

    public override long Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseInt64(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidIntegerScalar(ref reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, long value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
