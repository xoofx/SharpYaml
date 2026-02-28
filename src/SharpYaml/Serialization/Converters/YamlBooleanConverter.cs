using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlBooleanConverter : YamlConverter<bool>
{
    public static YamlBooleanConverter Instance { get; } = new();

    public override bool Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseBool(value, out var result))
        {
            throw new FormatException($"Invalid boolean scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, bool value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value ? "true" : "false");
    }
}
