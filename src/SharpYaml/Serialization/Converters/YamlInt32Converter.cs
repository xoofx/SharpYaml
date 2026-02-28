using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlInt32Converter : YamlConverter<int>
{
    public static YamlInt32Converter Instance { get; } = new();

    public override int Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseInt32(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidIntegerScalar(ref reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, int value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
