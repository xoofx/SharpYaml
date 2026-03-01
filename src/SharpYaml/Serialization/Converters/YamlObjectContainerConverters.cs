using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlDictionaryObjectConverter : YamlConverter<Dictionary<string, object?>?>
{
    public static YamlDictionaryObjectConverter Instance { get; } = new();

    public override Dictionary<string, object?>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (Dictionary<string, object?>)rootAliasValue!;
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
            throw YamlThrowHelper.ThrowExpectedMapping(reader);
        }

        var anchor = reader.Anchor;
        reader.Read();

        var options = reader.Options;
        var dict = new Dictionary<string, object?>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, dict);
        }

        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
            }

            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            var value = reader.GetConverter(typeof(object)).Read(reader, typeof(object));
            if (dict.ContainsKey(key))
            {
                switch (options.DuplicateKeyHandling)
                {
                    case YamlDuplicateKeyHandling.Error:
                        throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, key);
                    case YamlDuplicateKeyHandling.FirstWins:
                        break;
                    case YamlDuplicateKeyHandling.LastWins:
                        dict[key] = value;
                        break;
                }
            }
            else
            {
                dict[key] = value;
            }
        }

        reader.Read();
        return dict;
    }

    public override void Write(YamlWriter writer, Dictionary<string, object?>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

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
            var key = writer.ConvertDictionaryKey(pair.Key);
            writer.WritePropertyName(key);
            writer.GetConverter(typeof(object)).Write(writer, pair.Value);
        }
        writer.WriteEndMapping();
    }
}

internal sealed class YamlListObjectConverter : YamlConverter<List<object?>?>
{
    public static YamlListObjectConverter Instance { get; } = new();

    public override List<object?>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (List<object?>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a list unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        var anchor = reader.Anchor;
        reader.Read();

        var list = new List<object?>();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, list);
        }

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            var value = reader.GetConverter(typeof(object)).Read(reader, typeof(object));
            list.Add(value);
        }

        reader.Read();
        return list;
    }

    public override void Write(YamlWriter writer, List<object?>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

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
            writer.GetConverter(typeof(object)).Write(writer, value[i]);
        }
        writer.WriteEndSequence();
    }
}

internal sealed class YamlObjectArrayConverter : YamlConverter<object[]?>
{
    public static YamlObjectArrayConverter Instance { get; } = new();

    public override object[]? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (object[])rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into an array unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        var anchor = reader.Anchor;
        reader.Read();

        var list = new List<object?>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            var value = reader.GetConverter(typeof(object)).Read(reader, typeof(object));
            list.Add(value);
        }

        reader.Read();
        var array = list.ToArray();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, array);
        }

        return array;
    }

    public override void Write(YamlWriter writer, object[]? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

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
            writer.GetConverter(typeof(object)).Write(writer, value[i]);
        }
        writer.WriteEndSequence();
    }
}
