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
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalarParser.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw new InvalidOperationException($"Expected a mapping token but found '{reader.TokenType}'.");
        }

        _valueConverter ??= _resolver.GetConverter(typeof(TValue));
        reader.Read();

        var dictionary = new Dictionary<string, TValue>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw new InvalidOperationException($"Expected a scalar key token but found '{reader.TokenType}'.");
            }

            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            var value = _valueConverter.Read(ref reader, typeof(TValue), options);
            if (dictionary.ContainsKey(key))
            {
                switch (options.DuplicateKeyHandling)
                {
                    case YamlDuplicateKeyHandling.Error:
                        throw new InvalidOperationException($"Duplicate mapping key '{key}'.");
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

