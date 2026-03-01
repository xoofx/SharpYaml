using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlStringConverter : YamlConverter<string?>
{
    public static YamlStringConverter Instance { get; } = new();

    public override string? Read(YamlReader reader)
    {
        if (reader.TokenType == YamlTokenType.Scalar)
        {
            var value = reader.ScalarValue ?? string.Empty;
            reader.Read();
            return value;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into string unless ReferenceHandling is Preserve.");
        }

        throw YamlThrowHelper.ThrowExpectedScalar(reader);
    }

    public override void Write(YamlWriter writer, string? value)
    {
        writer.WriteScalar(value);
    }
}
