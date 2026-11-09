// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A block mapping: an ordered sequence of "key: value" entries sharing the same indentation.
/// </summary>
public sealed class YamlEditableMap : YamlEditableNode
{
    private readonly List<YamlEditableMapEntry> _entries = [];

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="YamlEditableMap"/> class.
    /// </summary>
    public YamlEditableMap() : base(YamlEditableKind.Map)
    {
    }

    /// <summary>
    /// Gets the entries of this mapping, in source order.
    /// </summary>
    public IReadOnlyList<YamlEditableMapEntry> Entries => _entries;

    /// <summary>
    /// Gets the value for <paramref name="key"/>, or <see langword="null"/> if the key is not
    /// present.
    /// </summary>
    public YamlEditableNode? GetValue(string key) =>
        _entries.FirstOrDefault(entry => entry.Key == key)?.Value;

    /// <summary>
    /// Sets the value for <paramref name="key"/>. If the key already exists, its value node is
    /// replaced in place, keeping the entry's original indentation and surrounding comments. If
    /// the key does not exist yet, a new entry is appended using <paramref name="indent"/>
    /// (defaults to the column of the last existing key, or an empty string for the root).
    /// </summary>
    public void SetValue(string key, YamlEditableNode value, string? indent = null)
    {
        ArgumentGuard.ThrowIfNull(key);
        if (key.Length == 0)
        {
            throw new ArgumentException("The key must not be empty.", nameof(key));
        }

        ArgumentGuard.ThrowIfNull(value);

        var existing = _entries.FirstOrDefault(entry => entry.Key == key);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        var resolvedIndent = indent
            ?? (_entries.Count > 0 ? YamlEditableLayout.GetKeyColumn(_entries[_entries.Count - 1]) : string.Empty);
        AddEntry(new YamlEditableMapEntry(key, value, resolvedIndent));
    }

    /// <summary>
    /// Appends an already-constructed entry, reparenting it to this mapping. The previous last
    /// line keeps its comment, and the new entry starts on its own line.
    /// </summary>
    public void AddEntry(YamlEditableMapEntry entry)
    {
        ThrowIfParented(entry);
        YamlEditableLayout.PrepareAppend(_entries.Count > 0 ? _entries[_entries.Count - 1] : null, entry);
        AddParsedEntry(entry);
    }

    /// <summary>
    /// Inserts an already-constructed entry at <paramref name="index"/>, reparenting it to this
    /// mapping. The new entry starts on its own line, before the comment lines of the entry that
    /// is at <paramref name="index"/> now. If this mapping is a list item whose first key is on
    /// the same line as the "-", an entry inserted at index 0 takes that place.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the number of entries.</exception>
    public void InsertEntry(int index, YamlEditableMapEntry entry)
    {
        if (index < 0 || index > _entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == _entries.Count)
        {
            AddEntry(entry);
            return;
        }

        ThrowIfParented(entry);
        var next = _entries[index];
        if (index == 0 && YamlEditableLayout.SharesLineWithDash(next, this))
        {
            next.Indent = YamlEditableLayout.GetKeyColumn(next);
            entry.Indent = string.Empty;
        }

        YamlEditableLayout.PrepareInsert(next, entry);
        entry.Parent = this;
        _entries.Insert(index, entry);
    }

    /// <summary>
    /// Removes the entry for <paramref name="key"/>, with its comment lines and its line.
    /// Returns <see langword="false"/> if the key was not present. If this mapping is a list item
    /// and the removed key was on the same line as the "-", the next key moves to that line.
    /// </summary>
    /// <remarks>
    /// Removing the only entry of a mapping that is a list item leaves an empty item; remove the
    /// list item instead.
    /// </remarks>
    public bool RemoveEntry(string key)
    {
        var index = _entries.FindIndex(entry => entry.Key == key);
        if (index < 0)
        {
            return false;
        }

        var existing = _entries[index];
        if (index == 0 && YamlEditableLayout.SharesLineWithDash(existing, this)
            && _entries.Count > 1 && _entries[1].LeadingTrivia is null)
        {
            _entries[1].Indent = string.Empty;
        }

        existing.Parent = null;
        _entries.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Appends an entry read by the parser, without changing its trivia.
    /// </summary>
    internal void AddParsedEntry(YamlEditableMapEntry entry)
    {
        ThrowIfParented(entry);
        entry.Parent = this;
        _entries.Add(entry);
    }

    private static void ThrowIfParented(YamlEditableMapEntry entry)
    {
        ArgumentGuard.ThrowIfNull(entry);
        if (entry.Parent != null)
        {
            throw new InvalidOperationException("The entry is already parented to another node.");
        }
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
    {
        foreach (var entry in _entries)
        {
            entry.WriteTo(writer);
        }
    }
}
