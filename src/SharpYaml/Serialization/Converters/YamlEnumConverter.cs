using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlEnumConverter<TEnum> : YamlConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue ?? string.Empty;
        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
        {
            reader.Read();
            return parsed;
        }

        if (YamlScalar.TryParseInt64(text.AsSpan(), out var numeric))
        {
            reader.Read();
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        throw YamlThrowHelper.ThrowInvalidEnumScalar(reader, text);
    }

    public override void Write(YamlWriter writer, TEnum value)
    {
        writer.WriteScalar(value.ToString());
    }
}
