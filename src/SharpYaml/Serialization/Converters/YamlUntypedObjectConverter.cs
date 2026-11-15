// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlUntypedObjectConverter : YamlConverter
{
    public static YamlUntypedObjectConverter Instance { get; } = new();

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

    public override object? Read(YamlReader reader, Type typeToConvert)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return rootAliasValue;
        }

        var options = reader.Options;
        if (options.UnsafeAllowDeserializeFromTagTypeName && reader.Tag is not null)
        {
            var activated = TryReadUnsafeTaggedValue(reader);
            if (activated is not null)
            {
                return activated;
            }
        }

        switch (reader.TokenType)
        {
            case YamlTokenType.Scalar:
                var scalar = YamlScalar.ResolveObject(reader);
                reader.Read();
                return scalar;

            case YamlTokenType.StartSequence:
                var sequenceAnchor = reader.Anchor;
                reader.Read();
                var list = new List<object?>();
                if (reader.ReferenceReader is not null && sequenceAnchor is not null)
                {
                    reader.ReferenceReader.Register(sequenceAnchor, list);
                }

                while (reader.TokenType != YamlTokenType.EndSequence)
                {
                    list.Add(Read(reader, typeof(object)));
                }
                reader.Read();
                return list;

            case YamlTokenType.StartMapping:
                return ReadMapping(reader);

            default:
                throw YamlThrowHelper.ThrowUnexpectedToken(reader);
        }
    }

    public override void Write(YamlWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var converter = writer.GetConverter(value.GetType());
        converter.Write(writer, value);
    }

#if !NETSTANDARD2_0
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2057",
        Justification = "This opt-in feature enables tag-based activation by runtime type name and is not compatible with trimming. It is guarded by UnsafeAllowDeserializeFromTagTypeName.")]
#endif
    private object? TryReadUnsafeTaggedValue(YamlReader reader)
    {
        var tag = reader.Tag;
        if (string.IsNullOrWhiteSpace(tag) || tag[0] != '!')
        {
            return null;
        }

        var typeName = tag.Substring(1);
        var type = Type.GetType(typeName, throwOnError: false);
        if (type is null && typeName.Contains(",mscorlib", StringComparison.Ordinal))
        {
            type = Type.GetType(typeName.Replace(",mscorlib", ",System.Private.CoreLib"), throwOnError: false);
        }

        if (type is null)
        {
            return null;
        }

        var converter = reader.GetConverter(type);
        return converter.Read(reader, type);
    }

    private object ReadMapping(YamlReader reader)
    {
        var anchor = reader.Anchor;
        var options = reader.Options;
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var stringDictionary = new Dictionary<string, object?>(comparer);
        IDictionary dictionary = stringDictionary;
        var mergeEnabled = options.Schema is YamlSchemaKind.Core or YamlSchemaKind.Extended;
        var detectDuplicates = options.DuplicateKeyHandling != YamlDuplicateKeyHandling.LastWins;

        // While the mapping only has scalar keys and no merge key, stringDictionary holds exactly the explicit keys read
        // so far: its own keys detect duplicates and no key set is allocated. A merge key adds other keys, and a complex
        // key replaces the dictionary (PromoteMapping): the sets then take over, with the keys read until there.
        var keysInDictionary = true;
        HashSet<string>? explicitKeys = null;
        HashSet<string>? seenKeys = null;
        if (anchor is not null) reader.RegisterAnchor(anchor, dictionary);
        reader.Read();

        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            reader.ExpandAlias();
            object key;
            if (reader.TokenType == YamlTokenType.Scalar)
            {
                key = reader.ScalarValue ?? string.Empty;
                var isMerge = mergeEnabled && (string)key == "<<" && reader.ScalarStyle == ScalarStyle.Plain;
                reader.Read();
                if (isMerge)
                {
                    if (keysInDictionary)
                    {
                        keysInDictionary = false;
                        explicitKeys = new HashSet<string>(stringDictionary.Keys, comparer);
                        seenKeys = detectDuplicates ? new HashSet<string>(stringDictionary.Keys, comparer) : null;
                    }

                    ReadAndApplyMerge(reader, ref dictionary, explicitKeys, anchor);
                    continue;
                }

                explicitKeys?.Add((string)key);
                var wasSeen = keysInDictionary
                    ? detectDuplicates && stringDictionary.ContainsKey((string)key)
                    : seenKeys is not null && !seenKeys.Add((string)key);
                if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                {
                    throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, (string)key);
                }
                if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
                {
                    reader.Skip();
                    continue;
                }
            }
            else
            {
                if (keysInDictionary)
                {
                    keysInDictionary = false;
                    explicitKeys = mergeEnabled ? new HashSet<string>(stringDictionary.Keys, comparer) : null;
                    seenKeys = detectDuplicates ? new HashSet<string>(stringDictionary.Keys, comparer) : null;
                }

                PromoteMapping(reader, ref dictionary, anchor);
                key = Read(reader, typeof(object))
                    ?? throw new YamlException(reader.SourceName, reader.Start, reader.End, "A complex mapping key cannot be null.");
            }

            dictionary[key] = Read(reader, typeof(object));
        }
        reader.Read();
        return dictionary;
    }

    private static void PromoteMapping(YamlReader reader, ref IDictionary dictionary, string? anchor)
    {
        if (dictionary is Dictionary<object, object?>) return;
        var promoted = new Dictionary<object, object?>(reader.Options.PropertyNameCaseInsensitive
            ? MappingKeyComparer.IgnoreCase : MappingKeyComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary) promoted.Add(entry.Key, entry.Value);
        if (reader.WasResolvedByAlias(dictionary) ||
            (anchor is not null && !reader.TryReplaceAnchoredObject(anchor, dictionary, promoted)))
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End,
                "A mapping with complex keys cannot replace a mapping already referenced by an earlier alias.");
        }
        dictionary = promoted;
    }

    private void ReadAndApplyMerge(YamlReader reader, ref IDictionary dictionary, HashSet<string>? explicitKeys, string? anchor)
    {
        reader.ExpandAlias();
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return;
        }

        if (reader.TokenType == YamlTokenType.StartMapping)
        {
            var mapping = (IDictionary)Read(reader, typeof(object))!;
            ApplyMergeDictionary(reader, ref dictionary, mapping, explicitKeys, anchor);
            return;
        }
        if (reader.TokenType == YamlTokenType.StartSequence)
        {
            reader.Read();
            while (reader.TokenType != YamlTokenType.EndSequence)
            {
                reader.ExpandAlias();
                if (Read(reader, typeof(object)) is not IDictionary source)
                {
                    throw new YamlException(reader.SourceName, reader.Start, reader.End, "Merge sequence entries must be mappings.");
                }
                ApplyMergeDictionary(reader, ref dictionary, source, explicitKeys, anchor);
            }
            reader.Read();
            return;
        }
        throw new YamlException(reader.SourceName, reader.Start, reader.End, "Merge key value must be a mapping or a sequence of mappings.");
    }

    private static void ApplyMergeDictionary(YamlReader reader, ref IDictionary target, IDictionary merged, HashSet<string>? explicitKeys, string? anchor)
    {
        foreach (DictionaryEntry pair in merged)
        {
            if (pair.Key is string key && explicitKeys?.Contains(key) == true) continue;
            if (pair.Key is not string) PromoteMapping(reader, ref target, anchor);
            // Earlier mappings in a merge sequence take precedence over later ones.
            if (!target.Contains(pair.Key)) target[pair.Key] = pair.Value;
        }
    }

    private sealed class MappingKeyComparer : IEqualityComparer<object>
    {
        public static readonly MappingKeyComparer Ordinal = new(StringComparer.Ordinal);
        public static readonly MappingKeyComparer IgnoreCase = new(StringComparer.OrdinalIgnoreCase);
        private readonly StringComparer _strings;
        private MappingKeyComparer(StringComparer strings) => _strings = strings;
        public new bool Equals(object? x, object? y) => x is string a && y is string b ? _strings.Equals(a, b) : ReferenceEquals(x, y);
        public int GetHashCode(object value) => value is string text ? _strings.GetHashCode(text) : RuntimeHelpers.GetHashCode(value);
    }
}
