using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlDictionaryConverter<TValue> : YamlConverter<Dictionary<string, TValue>?>
{
    private readonly IYamlConverterResolver _resolver;
    private YamlConverter? _valueConverter;

    public YamlDictionaryConverter(IYamlConverterResolver resolver)
    {
        _resolver = resolver;
    }

    public override Dictionary<string, TValue>? Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (Dictionary<string, TValue>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a dictionary unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(ref reader);
        }

        _valueConverter ??= _resolver.GetConverter(typeof(TValue));
        var anchor = reader.Anchor;
        reader.Read();

        var dictionary = new Dictionary<string, TValue>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, dictionary);
        }

        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(ref reader);
            }

            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            var value = _valueConverter.Read(ref reader, typeof(TValue), options);
            if (dictionary.ContainsKey(key))
            {
                switch (options.DuplicateKeyHandling)
                {
                    case YamlDuplicateKeyHandling.Error:
                        throw YamlThrowHelper.ThrowDuplicateMappingKey(ref reader, key);
                    case YamlDuplicateKeyHandling.FirstWins:
                        break;
                    case YamlDuplicateKeyHandling.LastWins:
                        dictionary[key] = (TValue)value!;
                        break;
                }
            }
            else
            {
                dictionary[key] = (TValue)value!;
            }
        }

        reader.Read();
        return dictionary;
    }

    public override void Write(YamlWriter writer, Dictionary<string, TValue>? value, YamlSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= _resolver.GetConverter(typeof(TValue));

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

        writer.WriteStartMapping();
        foreach (var pair in value)
        {
            var key = options.DictionaryKeyPolicy?.ConvertName(pair.Key) ?? pair.Key;
            writer.WritePropertyName(key);
            _valueConverter.Write(writer, pair.Value, options);
        }
        writer.WriteEndMapping();
    }
}
