// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlIReadOnlyListConverter<TElement> : YamlConverter<IReadOnlyList<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override IReadOnlyList<TElement>? Read(YamlReader reader)
    {
        var list = SequenceReadHelpers.ReadList<TElement>(reader, ref _elementConverter, "IReadOnlyList", typeof(IReadOnlyList<TElement>));
        return list;
    }

    public override void Write(YamlWriter writer, IReadOnlyList<TElement>? value)
    {
        SequenceReadHelpers.WriteEnumerable(writer, value, ref _elementConverter);
    }
}

internal sealed class YamlIReadOnlyCollectionConverter<TElement> : YamlConverter<IReadOnlyCollection<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override IReadOnlyCollection<TElement>? Read(YamlReader reader)
    {
        var list = SequenceReadHelpers.ReadList<TElement>(reader, ref _elementConverter, "IReadOnlyCollection", typeof(IReadOnlyCollection<TElement>));
        return list;
    }

    public override void Write(YamlWriter writer, IReadOnlyCollection<TElement>? value)
    {
        SequenceReadHelpers.WriteEnumerable(writer, value, ref _elementConverter);
    }
}

internal sealed class YamlIListConverter<TElement> : YamlConverter<IList<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(IList<TElement>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not IList<TElement> list)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must implement '{typeof(IList<TElement>)}'.");
        }

        return SequenceReadHelpers.PopulateCollection(reader, list, ref _elementConverter, "IList", typeToConvert);
    }

    public override IList<TElement>? Read(YamlReader reader)
    {
        var list = SequenceReadHelpers.ReadList<TElement>(reader, ref _elementConverter, "IList", typeof(IList<TElement>));
        return list;
    }

    public override void Write(YamlWriter writer, IList<TElement>? value)
    {
        SequenceReadHelpers.WriteEnumerable(writer, value, ref _elementConverter);
    }
}

internal sealed class YamlICollectionConverter<TElement> : YamlConverter<ICollection<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(ICollection<TElement>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not ICollection<TElement> collection)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must implement '{typeof(ICollection<TElement>)}'.");
        }

        return SequenceReadHelpers.PopulateCollection(reader, collection, ref _elementConverter, "ICollection", typeToConvert);
    }

    public override ICollection<TElement>? Read(YamlReader reader)
    {
        var list = SequenceReadHelpers.ReadList<TElement>(reader, ref _elementConverter, "ICollection", typeof(ICollection<TElement>));
        return list;
    }

    public override void Write(YamlWriter writer, ICollection<TElement>? value)
    {
        SequenceReadHelpers.WriteEnumerable(writer, value, ref _elementConverter);
    }
}

internal sealed class YamlMutableCollectionConverter<TCollection, TElement> : YamlConverter<TCollection?>
    where TCollection : class, ICollection<TElement>, new()
{
    private YamlConverter? _elementConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(TCollection);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not ICollection<TElement> collection)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must implement '{typeof(ICollection<TElement>)}'.");
        }

        return SequenceReadHelpers.PopulateCollection(reader, collection, ref _elementConverter, typeof(TCollection).Name, typeToConvert);
    }

    public override TCollection? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (TCollection)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Aliases are not supported when deserializing into {typeof(TCollection)} unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        _elementConverter ??= reader.GetConverter(typeof(TElement));
        var anchor = reader.Anchor;
        reader.Read();

        var collection = new TCollection();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, collection);
        }

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(_elementConverter, typeof(TElement), typeof(TCollection), collection, out var value))
            {
                collection.Add((TElement)value!);
            }
        }

        reader.Read();
        return collection;
    }

    public override void Write(YamlWriter writer, TCollection? value)
    {
        SequenceReadHelpers.WriteEnumerable(writer, value, ref _elementConverter);
    }
}

internal sealed class YamlHashSetConverter<TElement> : YamlConverter<HashSet<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(HashSet<TElement>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not HashSet<TElement> set)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must be a '{typeof(HashSet<TElement>)}'.");
        }

        return SequenceReadHelpers.PopulateCollection(reader, set, ref _elementConverter, "HashSet", typeToConvert);
    }

    public override HashSet<TElement>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (HashSet<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a set unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        _elementConverter ??= reader.GetConverter(typeof(TElement));
        var anchor = reader.Anchor;
        reader.Read();

        var set = new HashSet<TElement>();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, set);
        }

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(_elementConverter, typeof(TElement), typeof(HashSet<TElement>), set, out var value))
            {
                set.Add((TElement)value!);
            }
        }

        reader.Read();
        return set;
    }

    public override void Write(YamlWriter writer, HashSet<TElement>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= writer.GetConverter(typeof(TElement));

        if (writer.ReferenceWriter is not null)
        {
            if (writer.ReferenceWriter.TryGetAnchor(value, out var existing))
            {
                writer.WriteAlias(existing);
                return;
            }

            var anchor = writer.ReferenceWriter.GetOrAddAnchor(value);
            if (anchor is not null)
            {
                writer.WriteAnchor(anchor);
            }
        }

        writer.WriteStartSequence();
        foreach (var item in value)
        {
            _elementConverter.Write(writer, item);
        }
        writer.WriteEndSequence();
    }
}

internal sealed class YamlISetConverter<TElement> : YamlConverter<ISet<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override bool CanPopulate(Type typeToConvert) => typeToConvert == typeof(ISet<TElement>);

    public override object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(existingValue);
        if (existingValue is not ISet<TElement> set)
        {
            throw new InvalidOperationException($"Existing value for '{typeToConvert}' must implement '{typeof(ISet<TElement>)}'.");
        }

        return SequenceReadHelpers.PopulateCollection(reader, set, ref _elementConverter, "ISet", typeToConvert);
    }

    public override ISet<TElement>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (ISet<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into a set unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        _elementConverter ??= reader.GetConverter(typeof(TElement));
        var anchor = reader.Anchor;
        reader.Read();

        ISet<TElement> set = new HashSet<TElement>();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, set);
        }

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(_elementConverter, typeof(TElement), typeof(ISet<TElement>), set, out var value))
            {
                set.Add((TElement)value!);
            }
        }

        reader.Read();
        return set;
    }

    public override void Write(YamlWriter writer, ISet<TElement>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= writer.GetConverter(typeof(TElement));

        if (writer.ReferenceWriter is not null)
        {
            if (writer.ReferenceWriter.TryGetAnchor(value, out var existing))
            {
                writer.WriteAlias(existing);
                return;
            }

            var anchor = writer.ReferenceWriter.GetOrAddAnchor(value);
            if (anchor is not null)
            {
                writer.WriteAnchor(anchor);
            }
        }

        writer.WriteStartSequence();
        foreach (var item in value)
        {
            _elementConverter.Write(writer, item);
        }
        writer.WriteEndSequence();
    }
}

internal sealed class YamlImmutableArrayConverter<TElement> : YamlConverter<ImmutableArray<TElement>>
{
    private YamlConverter? _elementConverter;

    public override ImmutableArray<TElement> Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (ImmutableArray<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into ImmutableArray unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return default;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        _elementConverter ??= reader.GetConverter(typeof(TElement));
        var rootAnchor = reader.Anchor;
        reader.Read();

        var builder = ImmutableArray.CreateBuilder<TElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(_elementConverter, typeof(TElement), typeof(ImmutableArray<TElement>), null, out var value))
            {
                builder.Add((TElement)value!);
            }
        }

        reader.Read();
        var result = builder.ToImmutable();
        if (rootAnchor is not null)
        {
            reader.RegisterAnchor(rootAnchor, result);
        }

        return result;
    }

    public override void Write(YamlWriter writer, ImmutableArray<TElement> value)
    {
        if (value.IsDefault)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= writer.GetConverter(typeof(TElement));

        writer.WriteStartSequence();
        for (var i = 0; i < value.Length; i++)
        {
            _elementConverter.Write(writer, value[i]);
        }
        writer.WriteEndSequence();
    }
}

internal sealed class YamlImmutableListConverter<TElement> : YamlConverter<ImmutableList<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override ImmutableList<TElement>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (ImmutableList<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into ImmutableList unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        _elementConverter ??= reader.GetConverter(typeof(TElement));
        var rootAnchor = reader.Anchor;
        reader.Read();

        var builder = ImmutableList.CreateBuilder<TElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(_elementConverter, typeof(TElement), typeof(ImmutableList<TElement>), null, out var value))
            {
                builder.Add((TElement)value!);
            }
        }

        reader.Read();
        var result = builder.ToImmutable();
        if (rootAnchor is not null)
        {
            reader.RegisterAnchor(rootAnchor, result);
        }

        return result;
    }

    public override void Write(YamlWriter writer, ImmutableList<TElement>? value)
    {
        SequenceReadHelpers.WriteEnumerable(writer, value, ref _elementConverter);
    }
}

internal sealed class YamlImmutableHashSetConverter<TElement> : YamlConverter<ImmutableHashSet<TElement>?>
{
    private YamlConverter? _elementConverter;

    public override ImmutableHashSet<TElement>? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (ImmutableHashSet<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into an immutable set unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        _elementConverter ??= reader.GetConverter(typeof(TElement));
        var rootAnchor = reader.Anchor;
        reader.Read();

        var builder = ImmutableHashSet.CreateBuilder<TElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(_elementConverter, typeof(TElement), typeof(ImmutableHashSet<TElement>), null, out var value))
            {
                builder.Add((TElement)value!);
            }
        }

        reader.Read();
        var result = builder.ToImmutable();
        if (rootAnchor is not null)
        {
            reader.RegisterAnchor(rootAnchor, result);
        }

        return result;
    }

    public override void Write(YamlWriter writer, ImmutableHashSet<TElement>? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _elementConverter ??= writer.GetConverter(typeof(TElement));

        writer.WriteStartSequence();
        foreach (var item in value)
        {
            _elementConverter.Write(writer, item);
        }
        writer.WriteEndSequence();
    }
}

internal static class SequenceReadHelpers
{
    public static List<TElement>? ReadList<TElement>(YamlReader reader, ref YamlConverter? elementConverter, string typeDisplayName, Type declaringType)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (List<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Aliases are not supported when deserializing into {typeDisplayName} unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        elementConverter ??= reader.GetConverter(typeof(TElement));
        var anchor = reader.Anchor;
        reader.Read();

        var list = new List<TElement>();
        if (reader.ReferenceReader is not null && anchor is not null)
        {
            reader.ReferenceReader.Register(anchor, list);
        }

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(elementConverter, typeof(TElement), declaringType, list, out var value))
            {
                list.Add((TElement)value!);
            }
        }

        reader.Read();
        return list;
    }

    public static void WriteEnumerable<TElement>(YamlWriter writer, IEnumerable<TElement>? value, ref YamlConverter? elementConverter)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        elementConverter ??= writer.GetConverter(typeof(TElement));

        if (writer.ReferenceWriter is not null)
        {
            if (writer.ReferenceWriter.TryGetAnchor(value, out var existing))
            {
                writer.WriteAlias(existing);
                return;
            }

            var anchor = writer.ReferenceWriter.GetOrAddAnchor(value);
            if (anchor is not null)
            {
                writer.WriteAnchor(anchor);
            }
        }

        writer.WriteStartSequence();
        foreach (var item in value)
        {
            elementConverter.Write(writer, item);
        }
        writer.WriteEndSequence();
    }

    public static ICollection<TElement>? PopulateCollection<TElement>(YamlReader reader, ICollection<TElement> collection, ref YamlConverter? elementConverter, string typeDisplayName, Type declaringType)
    {
        ArgumentGuard.ThrowIfNull(reader);
        ArgumentGuard.ThrowIfNull(collection);

        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (ICollection<TElement>)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Aliases are not supported when deserializing into {typeDisplayName} unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.StartSequence)
        {
            throw YamlThrowHelper.ThrowExpectedSequence(reader);
        }

        if (reader.ReferenceReader is not null && reader.Anchor is not null)
        {
            reader.ReferenceReader.Register(reader.Anchor, collection);
        }

        elementConverter ??= reader.GetConverter(typeof(TElement));
        reader.Read();

        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TryReadItem(elementConverter, typeof(TElement), declaringType, collection, out var value))
            {
                collection.Add((TElement)value!);
            }
        }

        reader.Read();
        return collection;
    }
}
