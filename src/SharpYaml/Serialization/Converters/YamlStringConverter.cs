using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlStringConverter : YamlConverter<string?>
{
    public static YamlStringConverter Instance { get; } = new();

    public override string? Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType == YamlTokenType.Scalar)
        {
            var value = reader.ScalarValue ?? string.Empty;
            reader.Read();
            return value;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new NotSupportedException("Aliases are not supported when deserializing into string.");
        }

        throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
    }

    public override void Write(YamlWriter writer, string? value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value);
    }
}

