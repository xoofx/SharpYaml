// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.References;

/// <summary>
/// The objects deserialized from anchored nodes under <see cref="YamlReferenceHandling.Preserve"/>, by anchor name.
/// </summary>
/// <remarks>
/// An anchor name is bound to its latest node: <see cref="YamlAnchorTable"/> removes the object of a name when the
/// name is anchored again, before the converter of the new node registers its own object. The objects that aliases
/// resolved to are remembered, so that a converter does not replace an object that already escaped through an alias.
/// </remarks>
internal sealed class YamlReferenceReader
{
    private readonly Dictionary<string, object> _anchors = new(StringComparer.Ordinal);
    private HashSet<object>? _resolved;

    public void Register(string anchor, object value)
    {
        _anchors[anchor] = value;
    }

    /// <summary>Gets the object registered for <paramref name="alias"/> without resolving the alias to it.</summary>
    public bool TryGet(string alias, out object? value) => _anchors.TryGetValue(alias, out value);

    public bool Contains(string alias) => _anchors.ContainsKey(alias);

    public void Remove(string anchor) => _anchors.Remove(anchor);

    public void Clear()
    {
        _anchors.Clear();
        _resolved?.Clear();
    }

    /// <summary>Resolves <paramref name="alias"/> to its object, which is then known to be referenced by an alias.</summary>
    /// <exception cref="InvalidOperationException">No object is registered for <paramref name="alias"/>.</exception>
    public object Resolve(string alias)
    {
        if (_anchors.TryGetValue(alias, out var value))
        {
            MarkResolved(value);
            return value;
        }

        throw new InvalidOperationException($"Unknown YAML alias '*{alias}'.");
    }

    /// <summary>Records that an alias resolved to <paramref name="value"/>.</summary>
    public void MarkResolved(object value) => (_resolved ??= new HashSet<object>(ReferenceEqualityComparer.Instance)).Add(value);

    /// <summary>Gets whether an alias resolved to <paramref name="value"/>.</summary>
    public bool WasResolved(object value) => _resolved is not null && _resolved.Contains(value);

    /// <summary>
    /// Replaces the object registered for <paramref name="anchor"/>, as when a converter promotes the collection it
    /// registered to another type, unless an alias already resolved to <paramref name="current"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when an alias resolved to <paramref name="current"/>, which would then be a stale
    /// reference; otherwise <see langword="true"/>. Nothing is replaced when <paramref name="anchor"/> is no longer
    /// bound to <paramref name="current"/>, because a later node took the name.
    /// </returns>
    public bool TryReplace(string anchor, object current, object replacement)
    {
        if (WasResolved(current))
        {
            return false;
        }

        if (_anchors.TryGetValue(anchor, out var registered) && ReferenceEquals(registered, current))
        {
            _anchors[anchor] = replacement;
        }

        return true;
    }
}
