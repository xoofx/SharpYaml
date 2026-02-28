using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlSingleConverter : YamlConverter<float>
{
    public static YamlSingleConverter Instance { get; } = new();

    public override float Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseDouble(value, out var parsed))
        {
            throw new FormatException($"Invalid float scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return (float)parsed;
    }

    public override void Write(YamlWriter writer, float value, YamlSerializerOptions options)
    {
        if (float.IsPositiveInfinity(value))
        {
            writer.WriteScalar(".inf");
            return;
        }

        if (float.IsNegativeInfinity(value))
        {
            writer.WriteScalar("-.inf");
            return;
        }

        if (float.IsNaN(value))
        {
            writer.WriteScalar(".nan");
            return;
        }

        writer.WriteScalar(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }
}
