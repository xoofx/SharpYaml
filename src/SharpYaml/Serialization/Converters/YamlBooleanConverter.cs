using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlBooleanConverter : YamlConverter<bool>
{
    public static YamlBooleanConverter Instance { get; } = new();

    public override bool Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseBool(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidBooleanScalar(ref reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, bool value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value ? "true" : "false");
    }
}
