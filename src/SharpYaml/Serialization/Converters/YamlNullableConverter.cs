using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlNullableConverter<T> : YamlConverter<T?> where T : struct
{
    private YamlConverter<T>? _inner;

    public override T? Read(YamlReader reader)
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        _inner ??= (YamlConverter<T>)reader.GetConverter(typeof(T));
        return _inner.Read(reader);
    }

    public override void Write(YamlWriter writer, T? value)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        _inner ??= (YamlConverter<T>)writer.GetConverter(typeof(T));
        _inner.Write(writer, value.GetValueOrDefault());
    }
}
