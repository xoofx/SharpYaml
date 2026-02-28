using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlNullableConverter<T> : YamlConverter<T?> where T : struct
{
    private readonly YamlConverter<T> _inner;

    public YamlNullableConverter(YamlConverter<T> inner)
    {
        _inner = inner;
    }

    public override T? Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalarParser.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        return _inner.Read(ref reader, options);
    }

    public override void Write(YamlWriter writer, T? value, YamlSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        _inner.Write(writer, value.GetValueOrDefault(), options);
    }
}

