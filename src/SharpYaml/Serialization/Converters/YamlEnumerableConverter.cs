using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlEnumerableConverter<TElement> : YamlConverter<IEnumerable<TElement>?>
{
    private readonly IYamlConverterResolver _resolver;
    private YamlConverter? _elementConverter;

    public YamlEnumerableConverter(IYamlConverterResolver resolver)
    {
        _resolver = resolver;
    }

    public override IEnumerable<TElement>? Read(ref YamlReader reader, YamlSerializerOptions options)
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

        var list = new List<TElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            var value = _elementConverter.Read(ref reader, typeof(TElement), options);
            list.Add((TElement)value!);
        }

        reader.Read();
        return list;
    }

    public override void Write(YamlWriter writer, IEnumerable<TElement>? value, YamlSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= _resolver.GetConverter(typeof(TElement));

        writer.WriteStartSequence();
        foreach (var item in value)
        {
            _elementConverter.Write(writer, item, options);
        }
        writer.WriteEndSequence();
    }
}
