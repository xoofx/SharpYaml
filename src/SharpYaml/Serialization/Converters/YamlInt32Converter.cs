using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlInt32Converter : YamlConverter<int>
{
    public static YamlInt32Converter Instance { get; } = new();

    public override int Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalarParser.TryParseInt32(value, out var result))
        {
            throw new FormatException($"Invalid integer scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, int value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

