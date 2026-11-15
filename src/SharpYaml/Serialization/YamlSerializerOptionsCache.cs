// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SharpYaml.Serialization;

/// <summary>
/// What the reflection-based serializer resolved for one <see cref="YamlSerializerOptions"/> instance and can use
/// again: the converter of each type, with the contract an object converter builds by reflection.
/// </summary>
/// <remarks>
/// <para>
/// Options are immutable, so what they resolve to does not change, and every reader and writer created for the same
/// options shares these converters instead of reading the attributes and members of each type again for each call. As
/// the converters registered in <see cref="YamlSerializerOptions.Converters"/> always were, a converter created from a
/// <see cref="YamlConverterAttribute"/> or by a <see cref="YamlConverterFactory"/> is therefore one instance used by
/// every call, possibly from several threads.
/// </para>
/// <para>
/// The cache belongs to the options instance, which holds it in a <see cref="Slot"/>, and lives as long as it. It
/// must not keep the types of a collectible assembly from being unloaded, as the default options live as long as
/// the application: see its tables.
/// </para>
/// </remarks>
internal sealed class YamlSerializerOptionsCache
{
    private readonly YamlSerializerOptions _owner;

    // A converter per type, in two tables that together do not keep a type from being unloaded. A type of a
    // collectible assembly is held weakly, with its converter alive only as long as the type. Any other type is
    // never unloaded and is held by a dictionary, which a lookup finds in less time than in a weak table: the
    // serializer looks up the converter of each value of an untyped collection, where the weak table alone was
    // measured at 30% of the time of writing a list of objects. Hardly any application has collectible types, so
    // their tables are created when the first one comes.
    private readonly ConcurrentDictionary<Type, YamlConverter?> _converters = new();
    private readonly ConcurrentDictionary<Type, YamlConverter?> _customConverters = new();
    private ConditionalWeakTable<Type, YamlConverter?>? _collectibleConverters;
    private ConditionalWeakTable<Type, YamlConverter?>? _collectibleCustomConverters;

    private YamlSerializerOptionsCache(YamlSerializerOptions owner) => _owner = owner;

    /// <summary>
    /// The length of the last document serialized to a string with these options, with some room: an application
    /// tends to serialize documents of a similar size, and a buffer of the right size from the start is not copied
    /// while it grows.
    /// </summary>
    /// <remarks>Only a hint: threads that serialize at once write it without synchronization, and any of their values will do.</remarks>
    public int LastStringLength;

    /// <summary>Gets the converter resolved for <paramref name="type"/> by <see cref="YamlReaderWriterBase.GetConverter"/>.</summary>
    public bool TryGetConverter(Type type, out YamlConverter converter)
        => _converters.TryGetValue(type, out converter!) || TryGetCollectible(ref _collectibleConverters, type, out converter!);

    /// <summary>
    /// Keeps <paramref name="converter"/> as the converter of <paramref name="type"/>, unless another thread resolved
    /// one first: every caller then uses the converter that was kept, which this returns.
    /// </summary>
    public YamlConverter GetOrAddConverter(Type type, YamlConverter converter)
        => GetOrAdd(_converters, ref _collectibleConverters, type, converter)!;

    /// <summary>
    /// Gets the converter found in <see cref="YamlSerializerOptions.Converters"/> for <paramref name="type"/> by
    /// <see cref="YamlReaderWriterBase.TryGetCustomConverter"/>, which is <see langword="null"/> when none handles it.
    /// </summary>
    public bool TryGetCustomConverter(Type type, out YamlConverter? converter)
        => _customConverters.TryGetValue(type, out converter) || TryGetCollectible(ref _collectibleCustomConverters, type, out converter);

    /// <summary>
    /// Keeps <paramref name="converter"/>, or <see langword="null"/> for none, as the converter that
    /// <see cref="YamlSerializerOptions.Converters"/> has for <paramref name="type"/>, as <see cref="GetOrAddConverter"/> does.
    /// </summary>
    public YamlConverter? GetOrAddCustomConverter(Type type, YamlConverter? converter)
        => GetOrAdd(_customConverters, ref _collectibleCustomConverters, type, converter);

    private static bool TryGetCollectible(ref ConditionalWeakTable<Type, YamlConverter?>? table, Type type, out YamlConverter? converter)
    {
        var collectibleTypes = Volatile.Read(ref table);
        if (collectibleTypes is null)
        {
            converter = null;
            return false;
        }

        return collectibleTypes.TryGetValue(type, out converter);
    }

    /// <summary>Keeps <paramref name="converter"/> for <paramref name="type"/> unless it has one, and returns the one that is kept.</summary>
    private static YamlConverter? GetOrAdd(ConcurrentDictionary<Type, YamlConverter?> types, ref ConditionalWeakTable<Type, YamlConverter?>? collectibleTypes, Type type, YamlConverter? converter)
    {
        if (!IsCollectible(type))
        {
            return types.GetOrAdd(type, converter);
        }

        var table = Volatile.Read(ref collectibleTypes)
            ?? Interlocked.CompareExchange(ref collectibleTypes, new ConditionalWeakTable<Type, YamlConverter?>(), null)
            ?? collectibleTypes!;
        return table.GetValue(type, _ => converter);
    }

#if NETSTANDARD2_0
    // The framework does not say there whether a type can be unloaded: every type is held weakly.
    private static bool IsCollectible(Type type) => true;
#else
    private static bool IsCollectible(Type type) => type.IsCollectible;
#endif

    /// <summary>
    /// The field of <see cref="YamlSerializerOptions"/> that holds its cache, created when it is first used.
    /// </summary>
    /// <remarks>
    /// Options are a record, and the cache is no part of their value. Every slot is therefore equal to every other,
    /// which keeps it out of the equality and the hash code the compiler writes for the record. A copy made with a
    /// <c>with</c> expression starts with the fields of the original, this one included, and may change any setting:
    /// a cache knows the options it was created for, and the copy, which is not them, creates its own.
    /// </remarks>
    internal struct Slot : IEquatable<Slot>
    {
        private YamlSerializerOptionsCache? _cache;

        public YamlSerializerOptionsCache Get(YamlSerializerOptions owner)
        {
            var cache = Volatile.Read(ref _cache);
            return cache is not null && ReferenceEquals(cache._owner, owner) ? cache : Create(owner, cache);
        }

        private YamlSerializerOptionsCache Create(YamlSerializerOptions owner, YamlSerializerOptionsCache? inherited)
        {
            // Only the threads that use these options write this field: when another one was faster, its cache is theirs.
            var created = new YamlSerializerOptionsCache(owner);
            var current = Interlocked.CompareExchange(ref _cache, created, inherited);
            return ReferenceEquals(current, inherited) ? created : current!;
        }

        public readonly bool Equals(Slot other) => true;

        public override readonly bool Equals(object? obj) => obj is Slot;

        public override readonly int GetHashCode() => 0;
    }
}