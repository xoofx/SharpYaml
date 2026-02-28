using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlEnumConverter<TEnum> : YamlConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
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

        throw YamlThrowHelper.ThrowInvalidEnumScalar(ref reader, text);
    }

    public override void Write(YamlWriter writer, TEnum value, YamlSerializerOptions options)
    {
        writer.WriteScalar(value.ToString());
    }
}
