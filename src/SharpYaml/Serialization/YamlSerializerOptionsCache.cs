// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

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
/// The cache is attached to the options instance rather than stored in it: options are a record, and a copy made with
/// a <c>with</c> expression must not inherit what the original resolved with other settings.
/// </para>
/// </remarks>
internal sealed class YamlSerializerOptionsCache
{
    private static readonly ConditionalWeakTable<YamlSerializerOptions, YamlSerializerOptionsCache> s_caches = new();

    /// <summary>Gets the cache of <paramref name="options"/>, which lives as long as the options.</summary>
    public static YamlSerializerOptionsCache For(YamlSerializerOptions options)
        => s_caches.GetValue(options, static _ => new YamlSerializerOptionsCache());

    /// <summary>The converter resolved for each type, by <see cref="YamlReaderWriterBase.GetConverter"/>.</summary>
    public ConcurrentDictionary<Type, YamlConverter> Converters { get; } = new();

    /// <summary>
    /// The converter found in <see cref="YamlSerializerOptions.Converters"/> for each type, or <see langword="null"/>
    /// when there is none, by <see cref="YamlReaderWriterBase.TryGetCustomConverter"/>.
    /// </summary>
    public ConcurrentDictionary<Type, YamlConverter?> CustomConverters { get; } = new();
}
