// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlDictionaryConverter<TValue> : YamlConverter<Dictionary<string, TValue>?>
{
    private YamlConverter? _valueConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(Dictionary<string, TValue>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not Dictionary<string, TValue> dictionary)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must be a '{typeof(Dictionary<string, TValue>)}'.");
        }

        return PopulateDictionary(reader, dictionary);
    }

    public override Dictionary<string, TValue>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (Dictionary<string, TValue>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a dictionary unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(reader);
        }

        _valueConverter ??= reader.GetConverter(typeof(TValue));
        var anchor = reader.Anchor;
        reader.Read();

        var options = reader.Options;
        var dictionary = new Dictionary<string, TValue>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var mergeEnabled = options.Schema is YamlSchemaKind.Core or YamlSchemaKind.Extended;
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HashSet<string>? explicitKeys = mergeEnabled ? new HashSet<string>(comparer) : null;
        HashSet<string>? seenKeys = options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins ? null : new HashSet<string>(comparer);
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, dictionary);
        }

        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
            }

            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            if (mergeEnabled && string.Equals(key, "<<", StringComparison.Ordinal))
            {
                ReadAndApplyMerge(reader, dictionary, explicitKeys);
                continue;
            }

            explicitKeys?.Add(key);

            var wasSeen = seenKeys is not null && !seenKeys.Add(key);
            if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
            {
                throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, key);
            }

            if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
            {
                reader.Skip();
                continue;
            }

            var value = _valueConverter.Read(reader, typeof(TValue));
            dictionary[key] = (TValue)value!;
        }

        reader.Read();
        return dictionary;
    }

    public override void Write(YamlWriter writer, Dictionary<string, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= writer.GetConverter(typeof(TValue));

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
            _valueConverter.Write(writer, pair.Value);
        }
        writer.WriteEndMapping();
    }

    private void ReadAndApplyMerge(YamlReader reader, Dictionary<string, TValue> dictionary, HashSet<string>? explicitKeys)
    {
        // Merge key is only applied for Core/Extended schemas (JSON/Failsafe treat it as a normal key).
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return;
        }

        if (reader.TokenType == YamlTokenType.StartMapping || reader.TokenType == YamlTokenType.Alias)
        {
            var merged = Read(reader);
            if (merged is null)
            {
                return;
            }

            ApplyMergeDictionary(dictionary, merged, explicitKeys);
            return;
        }

        if (reader.TokenType == YamlTokenType.StartSequence)
        {
            reader.Read();
            while (reader.TokenType != YamlTokenType.EndSequence)
            {
                if (reader.TokenType != YamlTokenType.StartMapping && reader.TokenType != YamlTokenType.Alias)
                {
                    throw new YamlException(reader.SourceName, reader.Start, reader.End, "Merge sequence entries must be mappings.");
                }

                var merged = Read(reader);
                if (merged is not null)
                {
                    ApplyMergeDictionary(dictionary, merged, explicitKeys);
                }
            }

            reader.Read();
            return;
        }

        throw new YamlException(reader.SourceName, reader.Start, reader.End, "Merge key value must be a mapping or a sequence of mappings.");
    }

    private static void ApplyMergeDictionary(Dictionary<string, TValue> target, Dictionary<string, TValue> merged, HashSet<string>? explicitKeys)
    {
        foreach (var pair in merged)
        {
            if (explicitKeys is not null && explicitKeys.Contains(pair.Key))
            {
                continue;
            }

            // Merge semantics: later merges override earlier merges, while explicit keys always win.
            target[pair.Key] = pair.Value;
        }
    }

    private Dictionary<string, TValue>? PopulateDictionary(YamlReader reader, Dictionary<string, TValue> dictionary)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (Dictionary<string, TValue>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a dictionary unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(reader);
        }

        _valueConverter ??= reader.GetConverter(typeof(TValue));
        var options = reader.Options;
        var mergeEnabled = options.Schema is YamlSchemaKind.Core or YamlSchemaKind.Extended;
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HashSet<string>? explicitKeys = mergeEnabled ? new HashSet<string>(comparer) : null;
        HashSet<string>? seenKeys = options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins ? null : new HashSet<string>(comparer);
        if (reader.ReferenceReader is not null && reader.Anchor is not null)
        {
            reader.ReferenceReader.Register(reader.Anchor, dictionary);
        }

        reader.Read();
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
            }

            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            if (mergeEnabled && string.Equals(key, "<<", StringComparison.Ordinal))
            {
                ReadAndApplyMerge(reader, dictionary, explicitKeys);
                continue;
            }

            explicitKeys?.Add(key);

            var wasSeen = seenKeys is not null && !seenKeys.Add(key);
            if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
            {
                throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, key);
            }

            if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
            {
                reader.Skip();
                continue;
            }

            var value = _valueConverter.Read(reader, typeof(TValue));
            dictionary[key] = (TValue)value!;
        }

        reader.Read();
        return dictionary;
    }
}

internal sealed class YamlDictionaryConverter<TKey, TValue> : YamlConverter<Dictionary<TKey, TValue>?>
{
    private YamlConverter? _keyConverter;
    private YamlConverter? _valueConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(Dictionary<TKey, TValue>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not Dictionary<TKey, TValue> dictionary)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must be a '{typeof(Dictionary<TKey, TValue>)}'.");
        }

        return PopulateDictionary(reader, dictionary);
    }

    public override Dictionary<TKey, TValue>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (Dictionary<TKey, TValue>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a dictionary unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(reader);
        }

        _keyConverter ??= reader.GetConverter(typeof(TKey));
        _valueConverter ??= reader.GetConverter(typeof(TValue));

        var anchor = reader.Anchor;
        reader.Read();

        var options = reader.Options;
        var dictionary = new Dictionary<TKey, TValue>();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, dictionary);
        }

        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
            }

            var keyStart = reader.Start;
            var keyEnd = reader.End;
            object? rawKey;
            try
            {
                rawKey = _keyConverter.Read(reader, typeof(TKey));
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }

            if (rawKey is null)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, "Dictionary key cannot be null.");
            }

            var key = (TKey)rawKey;

            object? rawValue;
            try
            {
                rawValue = _valueConverter.Read(reader, typeof(TValue));
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }

            if (dictionary.ContainsKey(key))
            {
                switch (options.DuplicateKeyHandling)
                {
                    case YamlDuplicateKeyHandling.Error:
                        throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, key.ToString() ?? string.Empty);
                    case YamlDuplicateKeyHandling.FirstWins:
                        break;
                    case YamlDuplicateKeyHandling.LastWins:
                        dictionary[key] = (TValue)rawValue!;
                        break;
                }
            }
            else
            {
                dictionary[key] = (TValue)rawValue!;
            }
        }

        reader.Read();
        return dictionary;
    }

    public override void Write(YamlWriter writer, Dictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= writer.GetConverter(typeof(TValue));

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
            WriteKey(writer, pair.Key);
            _valueConverter.Write(writer, pair.Value);
        }
        writer.WriteEndMapping();
    }

    private static void WriteKey(YamlWriter writer, TKey key)
    {
        if (key is null)
        {
            throw new YamlException(Mark.Empty, Mark.Empty, "Dictionary key cannot be null.");
        }

        if (key is string textKey)
        {
            writer.WritePropertyName(writer.ConvertDictionaryKey(textKey));
            return;
        }

        writer.WritePropertyName(FormatNonStringKey(key));
    }

    private static string FormatNonStringKey(TKey key)
    {
        if (key is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }

        if (key is double doubleValue)
        {
            if (double.IsPositiveInfinity(doubleValue))
            {
                return ".inf";
            }

            if (double.IsNegativeInfinity(doubleValue))
            {
                return "-.inf";
            }

            if (double.IsNaN(doubleValue))
            {
                return ".nan";
            }

            return doubleValue.ToString("R", CultureInfo.InvariantCulture);
        }

        if (key is float floatValue)
        {
            if (float.IsPositiveInfinity(floatValue))
            {
                return ".inf";
            }

            if (float.IsNegativeInfinity(floatValue))
            {
                return "-.inf";
            }

            if (float.IsNaN(floatValue))
            {
                return ".nan";
            }

            return floatValue.ToString("R", CultureInfo.InvariantCulture);
        }

        if (key is DateTime dateTime)
        {
            return dateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        if (key is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
        }

        if (key is Guid guid)
        {
            return guid.ToString("D");
        }

        if (key is TimeSpan timeSpan)
        {
            return timeSpan.ToString("c", CultureInfo.InvariantCulture);
        }

#if NET6_0_OR_GREATER
        if (key is DateOnly dateOnly)
        {
            return dateOnly.ToString("O", CultureInfo.InvariantCulture);
        }

        if (key is TimeOnly timeOnly)
        {
            return timeOnly.ToString("O", CultureInfo.InvariantCulture);
        }
#endif

        if (key is IFormattable formattable)
        {
            return formattable.ToString(format: null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return key.ToString() ?? string.Empty;
    }

    private Dictionary<TKey, TValue>? PopulateDictionary(YamlReader reader, Dictionary<TKey, TValue> dictionary)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (Dictionary<TKey, TValue>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a dictionary unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(reader);
        }

        _keyConverter ??= reader.GetConverter(typeof(TKey));
        _valueConverter ??= reader.GetConverter(typeof(TValue));

        var options = reader.Options;
        if (reader.ReferenceReader is not null && reader.Anchor is not null)
        {
            reader.ReferenceReader.Register(reader.Anchor, dictionary);
        }

        reader.Read();
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
            }

            var keyStart = reader.Start;
            var keyEnd = reader.End;
            object? rawKey;
            try
            {
                rawKey = _keyConverter.Read(reader, typeof(TKey));
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }

            if (rawKey is null)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, "Dictionary key cannot be null.");
            }

            var key = (TKey)rawKey;

            object? rawValue;
            try
            {
                rawValue = _valueConverter.Read(reader, typeof(TValue));
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }

            if (dictionary.ContainsKey(key))
            {
                switch (options.DuplicateKeyHandling)
                {
                    case YamlDuplicateKeyHandling.Error:
                        throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, key.ToString() ?? string.Empty);
                    case YamlDuplicateKeyHandling.FirstWins:
                        break;
                    case YamlDuplicateKeyHandling.LastWins:
                        dictionary[key] = (TValue)rawValue!;
                        break;
                }
            }
            else
            {
                dictionary[key] = (TValue)rawValue!;
            }
        }

        reader.Read();
        return dictionary;
    }
}

internal sealed class YamlIDictionaryConverter<TKey, TValue> : YamlConverter<IDictionary<TKey, TValue>?>
{
    private readonly YamlDictionaryConverter<TKey, TValue> _inner = new();

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(IDictionary<TKey, TValue>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not IDictionary<TKey, TValue> dictionary)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must implement '{typeof(IDictionary<TKey, TValue>)}'.");
        }

        if (dictionary is Dictionary<TKey, TValue> concrete)
        {
            return _inner.Populate(reader, typeof(Dictionary<TKey, TValue>), concrete);
        }

        var populated = (Dictionary<TKey, TValue>?)_inner.Read(reader);
        if (populated is null)
        {
            return null;
        }

        foreach (var pair in populated)
        {
            dictionary[pair.Key] = pair.Value;
        }

        return dictionary;
    }

    public override IDictionary<TKey, TValue>? Read(YamlReader reader) => _inner.Read(reader);

    public override void Write(YamlWriter writer, IDictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is Dictionary<TKey, TValue> dictionary)
        {
            _inner.Write(writer, dictionary);
            return;
        }

        // Fallback: enumerate entries into a Dictionary so we can reuse duplicate handling and key formatting.
        var materialized = new Dictionary<TKey, TValue>();
        foreach (var pair in value)
        {
            materialized[pair.Key] = pair.Value;
        }

        _inner.Write(writer, materialized);
    }
}

internal sealed class YamlIReadOnlyDictionaryConverter<TKey, TValue> : YamlConverter<IReadOnlyDictionary<TKey, TValue>?>
{
    private readonly YamlDictionaryConverter<TKey, TValue> _inner = new();

    public override IReadOnlyDictionary<TKey, TValue>? Read(YamlReader reader) => _inner.Read(reader);

    public override void Write(YamlWriter writer, IReadOnlyDictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is Dictionary<TKey, TValue> dictionary)
        {
            _inner.Write(writer, dictionary);
            return;
        }

        var materialized = new Dictionary<TKey, TValue>();
        foreach (var pair in value)
        {
            materialized[pair.Key] = pair.Value;
        }

        _inner.Write(writer, materialized);
    }
}

#if NET9_0_OR_GREATER
internal sealed class YamlOrderedDictionaryConverter<TValue> : YamlConverter<System.Collections.Generic.OrderedDictionary<string, TValue>?>
{
    private readonly YamlDictionaryConverter<TValue> _inner = new();

    public override System.Collections.Generic.OrderedDictionary<string, TValue>? Read(YamlReader reader)
    {
        var dict = _inner.Read(reader);
        if (dict is null) return null;
        var comparer = reader.Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var ordered = new System.Collections.Generic.OrderedDictionary<string, TValue>(dict.Count, comparer);
        foreach (var pair in dict)
        {
            ordered[pair.Key] = pair.Value;
        }
        return ordered;
    }

    public override void Write(YamlWriter writer, System.Collections.Generic.OrderedDictionary<string, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var dict = new Dictionary<string, TValue>(value.Count);
        foreach (var pair in value)
        {
            dict[pair.Key] = pair.Value;
        }
        _inner.Write(writer, dict);
    }
}

internal sealed class YamlOrderedDictionaryConverter<TKey, TValue> : YamlConverter<System.Collections.Generic.OrderedDictionary<TKey, TValue>?>
{
    private readonly YamlDictionaryConverter<TKey, TValue> _inner = new();

    public override System.Collections.Generic.OrderedDictionary<TKey, TValue>? Read(YamlReader reader)
    {
        var dict = _inner.Read(reader);
        if (dict is null) return null;
        var ordered = new System.Collections.Generic.OrderedDictionary<TKey, TValue>(dict.Count);
        foreach (var pair in dict)
        {
            ordered.Add(pair.Key, pair.Value);
        }
        return ordered;
    }

    public override void Write(YamlWriter writer, System.Collections.Generic.OrderedDictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var dict = new Dictionary<TKey, TValue>(value.Count);
        foreach (var pair in value)
        {
            dict[pair.Key] = pair.Value;
        }
        _inner.Write(writer, dict);
    }
}
#endif
