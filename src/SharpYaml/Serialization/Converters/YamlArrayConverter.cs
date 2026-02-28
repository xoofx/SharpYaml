using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlArrayConverter<TElement> : YamlConverter<TElement[]?>
{
    private readonly IYamlConverterResolver _resolver;
    private YamlConverter? _elementConverter;

    public YamlArrayConverter(IYamlConverterResolver resolver)
    {
        _resolver = resolver;
    }

    public override TElement[]? Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalarParser.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw new InvalidOperationException($"Expected a sequence token but found '{reader.TokenType}'.");
        }

        _elementConverter ??= _resolver.GetConverter(typeof(TElement));
        reader.Read();

        var items = new List<TElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            var value = _elementConverter.Read(ref reader, typeof(TElement), options);
            items.Add((TElement)value!);
        }

        reader.Read();
        return items.ToArray();
    }

    public override void Write(YamlWriter writer, TElement[]? value, YamlSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= _resolver.GetConverter(typeof(TElement));

        writer.WriteStartSequence();
        for (var i = 0; i < value.Length; i++)
        {
            _elementConverter.Write(writer, value[i], options);
        }
        writer.WriteEndSequence();
    }
}

