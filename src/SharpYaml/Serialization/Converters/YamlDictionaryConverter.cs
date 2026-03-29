// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace SharpYaml.Serialization.Converters;

internal static class YamlDictionaryConverterHelper
{
    internal static bool TryWriteReference(YamlWriter writer, object referenceValue)
    {
        ArgumentGuard.ThrowIfNull(writer);
        ArgumentGuard.ThrowIfNull(referenceValue);

        if (writer.ReferenceWriter is null)
        {
            return false;
        }

        if (writer.ReferenceWriter.TryGetAnchor(referenceValue, out var existing))
        {
            writer.WriteAlias(existing);
            return true;
        }

        var anchor = writer.ReferenceWriter.GetOrAddAnchor(referenceValue);
        writer.WriteAnchor(anchor);
        return false;
    }

    internal static void WriteEntries<TValue>(YamlWriter writer, IEnumerable<KeyValuePair<string, TValue>> entries, YamlConverter valueConverter)
    {
        ArgumentGuard.ThrowIfNull(writer);
        ArgumentGuard.ThrowIfNull(entries);
        ArgumentGuard.ThrowIfNull(valueConverter);

        writer.WriteStartMapping();
        foreach (var pair in entries)
        {
            var key = writer.ConvertDictionaryKey(pair.Key);
            writer.WritePropertyName(key);
            valueConverter.Write(writer, pair.Value);
        }

        writer.WriteEndMapping();
    }

    internal static void WriteEntries<TKey, TValue>(YamlWriter writer, IEnumerable<KeyValuePair<TKey, TValue>> entries, YamlConverter valueConverter)
    {
        ArgumentGuard.ThrowIfNull(writer);
        ArgumentGuard.ThrowIfNull(entries);
        ArgumentGuard.ThrowIfNull(valueConverter);

        writer.WriteStartMapping();
        foreach (var pair in entries)
        {
            WriteKey(writer, pair.Key);
            valueConverter.Write(writer, pair.Value);
        }

        writer.WriteEndMapping();
    }

    internal static void WriteKey<TKey>(YamlWriter writer, TKey key)
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

    internal static string FormatNonStringKey<T>(T key)
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

        return key is null ? string.Empty : key.ToString() ?? string.Empty;
    }

    internal static TDictionary? ReadStringDictionary<TDictionary, TValue>(
        YamlReader reader,
        ref YamlConverter? valueConverter,
        Func<YamlSerializerOptions, TDictionary> createDictionary,
        string containerKind,
        TDictionary? dictionary = null)
        where TDictionary : class, IDictionary<string, TValue>
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (TDictionary)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Aliases are not supported when deserializing into a {containerKind} unless ReferenceHandling is Preserve.");
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

        valueConverter ??= reader.GetConverter(typeof(TValue));

        var options = reader.Options;
        dictionary ??= createDictionary(options);
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var mergeEnabled = options.Schema is YamlSchemaKind.Core or YamlSchemaKind.Extended;
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
                ReadAndApplyMerge<TDictionary, TValue>(reader, ref valueConverter, dictionary, explicitKeys, createDictionary, containerKind);
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

            var previousKey = reader.CurrentKey;
            reader.CurrentKey = key;
            var value = valueConverter.Read(reader, typeof(TValue));
            reader.CurrentKey = previousKey;
            dictionary[key] = (TValue)value!;
        }

        reader.Read();
        return dictionary;
    }

    internal static TDictionary? ReadDictionary<TDictionary, TKey, TValue>(
        YamlReader reader,
        ref YamlConverter? keyConverter,
        ref YamlConverter? valueConverter,
        Func<TDictionary> createDictionary,
        string containerKind,
        TDictionary? dictionary = null)
        where TDictionary : class, IDictionary<TKey, TValue>
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (TDictionary)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Aliases are not supported when deserializing into a {containerKind} unless ReferenceHandling is Preserve.");
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

        keyConverter ??= reader.GetConverter(typeof(TKey));
        valueConverter ??= reader.GetConverter(typeof(TValue));

        dictionary ??= createDictionary();
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
                rawKey = keyConverter.Read(reader, typeof(TKey));
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

            var previousKey = reader.CurrentKey;
            reader.CurrentKey = rawKey.ToString();
            object? rawValue;
            try
            {
                rawValue = valueConverter.Read(reader, typeof(TValue));
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }
            finally
            {
                reader.CurrentKey = previousKey;
            }

            var key = (TKey)rawKey;
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

    private static void ReadAndApplyMerge<TDictionary, TValue>(
        YamlReader reader,
        ref YamlConverter? valueConverter,
        TDictionary dictionary,
        HashSet<string>? explicitKeys,
        Func<YamlSerializerOptions, TDictionary> createDictionary,
        string containerKind)
        where TDictionary : class, IDictionary<string, TValue>
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return;
        }

        if (reader.TokenType == YamlTokenType.StartMapping || reader.TokenType == YamlTokenType.Alias)
        {
            var merged = ReadStringDictionary<TDictionary, TValue>(reader, ref valueConverter, createDictionary, containerKind);
            if (merged is null)
            {
                return;
            }

            ApplyMergeDictionary<TValue>(dictionary, merged, explicitKeys);
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

                var merged = ReadStringDictionary<TDictionary, TValue>(reader, ref valueConverter, createDictionary, containerKind);
                if (merged is not null)
                {
                    ApplyMergeDictionary<TValue>(dictionary, merged, explicitKeys);
                }
            }

            reader.Read();
            return;
        }

        throw new YamlException(reader.SourceName, reader.Start, reader.End, "Merge key value must be a mapping or a sequence of mappings.");
    }

    private static void ApplyMergeDictionary<TValue>(
        IDictionary<string, TValue> target,
        IEnumerable<KeyValuePair<string, TValue>> merged,
        HashSet<string>? explicitKeys)
    {
        foreach (var pair in merged)
        {
            if (explicitKeys is not null && explicitKeys.Contains(pair.Key))
            {
                continue;
            }

            target[pair.Key] = pair.Value;
        }
    }
}

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
        => YamlDictionaryConverterHelper.ReadStringDictionary<Dictionary<string, TValue>, TValue>(
            reader,
            ref _valueConverter,
            static options => new Dictionary<string, TValue>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
            "dictionary");

    public override void Write(YamlWriter writer, Dictionary<string, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        WriteEntries(writer, value, value);
    }

    internal void WriteEntries(YamlWriter writer, object referenceValue, IEnumerable<KeyValuePair<string, TValue>> entries)
    {
        ArgumentGuard.ThrowIfNull(writer);
        ArgumentGuard.ThrowIfNull(referenceValue);
        ArgumentGuard.ThrowIfNull(entries);

        _valueConverter ??= writer.GetConverter(typeof(TValue));
        if (YamlDictionaryConverterHelper.TryWriteReference(writer, referenceValue))
        {
            return;
        }

        YamlDictionaryConverterHelper.WriteEntries(writer, entries, _valueConverter);
    }

    private Dictionary<string, TValue>? PopulateDictionary(YamlReader reader, Dictionary<string, TValue> dictionary)
        => YamlDictionaryConverterHelper.ReadStringDictionary<Dictionary<string, TValue>, TValue>(
            reader,
            ref _valueConverter,
            static options => new Dictionary<string, TValue>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
            "dictionary",
            dictionary);
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
        => YamlDictionaryConverterHelper.ReadDictionary<Dictionary<TKey, TValue>, TKey, TValue>(
            reader,
            ref _keyConverter,
            ref _valueConverter,
            static () => new Dictionary<TKey, TValue>(),
            "dictionary");

    public override void Write(YamlWriter writer, Dictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        WriteEntries(writer, value, value);
    }

    internal void WriteEntries(YamlWriter writer, object referenceValue, IEnumerable<KeyValuePair<TKey, TValue>> entries)
    {
        ArgumentGuard.ThrowIfNull(writer);
        ArgumentGuard.ThrowIfNull(referenceValue);
        ArgumentGuard.ThrowIfNull(entries);

        _valueConverter ??= writer.GetConverter(typeof(TValue));
        if (YamlDictionaryConverterHelper.TryWriteReference(writer, referenceValue))
        {
            return;
        }

        YamlDictionaryConverterHelper.WriteEntries(writer, entries, _valueConverter);
    }

    private Dictionary<TKey, TValue>? PopulateDictionary(YamlReader reader, Dictionary<TKey, TValue> dictionary)
        => YamlDictionaryConverterHelper.ReadDictionary<Dictionary<TKey, TValue>, TKey, TValue>(
            reader,
            ref _keyConverter,
            ref _valueConverter,
            static () => new Dictionary<TKey, TValue>(),
            "dictionary",
            dictionary);
}

internal sealed class YamlIDictionaryConverter<TKey, TValue> : YamlConverter<IDictionary<TKey, TValue>?>
{
    private YamlConverter? _keyConverter;
    private YamlConverter? _valueConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(IDictionary<TKey, TValue>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not IDictionary<TKey, TValue> dictionary)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must implement '{typeof(IDictionary<TKey, TValue>)}'.");
        }

        return YamlDictionaryConverterHelper.ReadDictionary<IDictionary<TKey, TValue>, TKey, TValue>(
            reader,
            ref _keyConverter,
            ref _valueConverter,
            static () => (IDictionary<TKey, TValue>)new Dictionary<TKey, TValue>(),
            "dictionary",
            dictionary);
    }

    public override IDictionary<TKey, TValue>? Read(YamlReader reader)
        => YamlDictionaryConverterHelper.ReadDictionary<IDictionary<TKey, TValue>, TKey, TValue>(
            reader,
            ref _keyConverter,
            ref _valueConverter,
            static () => (IDictionary<TKey, TValue>)new Dictionary<TKey, TValue>(),
            "dictionary");

    public override void Write(YamlWriter writer, IDictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= writer.GetConverter(typeof(TValue));
        if (YamlDictionaryConverterHelper.TryWriteReference(writer, value))
        {
            return;
        }

        YamlDictionaryConverterHelper.WriteEntries(writer, value, _valueConverter);
    }
}

internal sealed class YamlIReadOnlyDictionaryConverter<TKey, TValue> : YamlConverter<IReadOnlyDictionary<TKey, TValue>?>
{
    private YamlConverter? _keyConverter;
    private YamlConverter? _valueConverter;

    public override IReadOnlyDictionary<TKey, TValue>? Read(YamlReader reader)
        => YamlDictionaryConverterHelper.ReadDictionary<Dictionary<TKey, TValue>, TKey, TValue>(
            reader,
            ref _keyConverter,
            ref _valueConverter,
            static () => new Dictionary<TKey, TValue>(),
            "dictionary");

    public override void Write(YamlWriter writer, IReadOnlyDictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= writer.GetConverter(typeof(TValue));
        if (YamlDictionaryConverterHelper.TryWriteReference(writer, value))
        {
            return;
        }

        YamlDictionaryConverterHelper.WriteEntries(writer, value, _valueConverter);
    }
}

#if NET9_0_OR_GREATER
internal sealed class YamlOrderedDictionaryConverter<TValue> : YamlConverter<System.Collections.Generic.OrderedDictionary<string, TValue>?>
{
    private YamlConverter? _valueConverter;

    public override System.Collections.Generic.OrderedDictionary<string, TValue>? Read(YamlReader reader)
        => YamlDictionaryConverterHelper.ReadStringDictionary<System.Collections.Generic.OrderedDictionary<string, TValue>, TValue>(
            reader,
            ref _valueConverter,
            static options => new System.Collections.Generic.OrderedDictionary<string, TValue>(
                0,
                options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal),
            "ordered dictionary");

    public override void Write(YamlWriter writer, System.Collections.Generic.OrderedDictionary<string, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= writer.GetConverter(typeof(TValue));

        if (YamlDictionaryConverterHelper.TryWriteReference(writer, value))
        {
            return;
        }

        YamlDictionaryConverterHelper.WriteEntries(writer, value, _valueConverter);
    }
}

internal sealed class YamlOrderedDictionaryConverter<TKey, TValue> : YamlConverter<System.Collections.Generic.OrderedDictionary<TKey, TValue>?>
{
    private YamlConverter? _keyConverter;
    private YamlConverter? _valueConverter;

    public override System.Collections.Generic.OrderedDictionary<TKey, TValue>? Read(YamlReader reader)
        => YamlDictionaryConverterHelper.ReadDictionary<System.Collections.Generic.OrderedDictionary<TKey, TValue>, TKey, TValue>(
            reader,
            ref _keyConverter,
            ref _valueConverter,
            static () => new System.Collections.Generic.OrderedDictionary<TKey, TValue>(),
            "ordered dictionary");

    public override void Write(YamlWriter writer, System.Collections.Generic.OrderedDictionary<TKey, TValue>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _valueConverter ??= writer.GetConverter(typeof(TValue));

        if (YamlDictionaryConverterHelper.TryWriteReference(writer, value))
        {
            return;
        }

        YamlDictionaryConverterHelper.WriteEntries(writer, value, _valueConverter);
    }
}
#endif
