using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlEnumConverter<TEnum> : YamlConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw new InvalidOperationException($"Expected a scalar token but found '{reader.TokenType}'.");
        }

        var text = reader.ScalarValue ?? string.Empty;
        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
        {
            reader.Read();
            return parsed;
        }

        if (YamlScalarParser.TryParseInt64(text.AsSpan(), out var numeric))
        {
            reader.Read();
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        throw new FormatException($"Invalid enum scalar '{text}'.");
    }

    public override void Write(YamlWriter writer, TEnum value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value.ToString());
    }
}

