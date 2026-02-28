using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlInt64Converter : YamlConverter<long>
{
    public static YamlInt64Converter Instance { get; } = new();

    public override long Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalarParser.TryParseInt64(value, out var result))
        {
            throw new FormatException($"Invalid integer scalar '{reader.ScalarValue}'.");
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, long value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

