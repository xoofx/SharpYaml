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
        if (reader.TokenType == YamlTokenType.Alias && reader.ReferenceReader is not null)
        {
            var alias = reader.Alias ?? throw new InvalidOperationException("Alias token did not provide an alias value.");
            var resolved = reader.ReferenceReader.Resolve(alias);
            reader.Read();
            return (TElement[])resolved;
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
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

        var items = new List<TElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            var value = _elementConverter.Read(ref reader, typeof(TElement), options);
            items.Add((TElement)value!);
        }

        reader.Read();
        var array = items.ToArray();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, array);
        }

        return array;
    }

    public override void Write(YamlWriter writer, TElement[]? value, YamlSerializerOptions options)
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
        for (var i = 0; i < value.Length; i++)
        {
            _elementConverter.Write(writer, value[i], options);
        }
        writer.WriteEndSequence();
    }
}
