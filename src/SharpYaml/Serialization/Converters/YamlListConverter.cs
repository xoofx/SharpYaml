using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlListConverter<TElement> : YamlConverter<List<TElement>?>
{
    private readonly IYamlConverterResolver _resolver;
    private YamlConverter? _elementConverter;

    public YamlListConverter(IYamlConverterResolver resolver)
    {
        _resolver = resolver;
    }

    public override List<TElement>? Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType == YamlTokenType.Alias && reader.ReferenceReader is not null)
        {
            var alias = reader.Alias ?? throw new InvalidOperationException("Alias token did not provide an alias value.");
            var resolved = reader.ReferenceReader.Resolve(alias);
            reader.Read();
            return (List<TElement>)resolved;
        }

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
        var anchor = reader.Anchor;
        reader.Read();

        var list = new List<TElement>();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, list);
        }

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            var value = _elementConverter.Read(ref reader, typeof(TElement), options);
            list.Add((TElement)value!);
        }

        reader.Read();
        return list;
    }

    public override void Write(YamlWriter writer, List<TElement>? value, YamlSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= _resolver.GetConverter(typeof(TElement));

        if (writer.ReferenceWriter is not null)
        {
            if (writer.ReferenceWriter.TryGetAnchor(value, out var existing))
            {
                writer.WriteAlias(existing);
                return;
            }

            var anchor = writer.ReferenceWriter.GetOrAddAnchor(value);
            writer.WriteAnchor(anchor);
        }

        writer.WriteStartSequence();
        for (var i = 0; i < value.Count; i++)
        {
            _elementConverter.Write(writer, value[i], options);
        }
        writer.WriteEndSequence();
    }
}
