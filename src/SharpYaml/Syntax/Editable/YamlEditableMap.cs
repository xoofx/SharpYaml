// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpYaml.Tokens;

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
    /// present. <paramref name="key"/> is compared with the value of each key, so it also finds a
    /// quoted key: <c>name</c> finds <c>"name": x</c> and <c>'name': x</c>.
    /// </summary>
    /// <param name="key">The key to find.</param>
    /// <returns>The value of the entry for <paramref name="key"/>, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public YamlEditableNode? GetValue(string key)
    {
        ArgumentGuard.ThrowIfNull(key);
        return _entries.FirstOrDefault(entry => HasKey(entry, key))?.Value;
    }

    /// <summary>
    /// Sets the value for <paramref name="key"/>. If the key already exists (compared as in
    /// <see cref="GetValue"/>), its value node is replaced in place, keeping the entry's original
    /// key text, indentation and surrounding comments. If the key does not exist yet, a new entry
    /// is appended with <paramref name="key"/> as its raw key text, at the column of the last
    /// existing key (no indentation for the first key of the root).
    /// </summary>
    /// <param name="key">The key to find, or the raw text of the new key.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="value"/> already has a parent.</exception>
    public void SetValue(string key, YamlEditableNode value) => SetValue(key, value, null);

    /// <summary>
    /// Sets the value for <paramref name="key"/>, as <see cref="SetValue(string, YamlEditableNode)"/> does, and
    /// writes a new key after <paramref name="indent"/>.
    /// </summary>
    /// <param name="key">The key to find, or the raw text of the new key.</param>
    /// <param name="value">The new value.</param>
    /// <param name="indent">
    /// The indentation text of a new key, or <see langword="null"/> for the column of the last existing key.
    /// Ignored when the key exists.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="value"/> already has a parent.</exception>
    public void SetValue(string key, YamlEditableNode value, string? indent)
    {
        ArgumentGuard.ThrowIfNull(key);
        if (key.Length == 0)
        {
            throw new ArgumentException("The key must not be empty.", nameof(key));
        }

        ArgumentGuard.ThrowIfNull(value);

        var existing = _entries.FirstOrDefault(entry => HasKey(entry, key));
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
    /// line keeps its comment, and the new entry starts on its own line. An empty mapping that
    /// is a value, written as <c>{}</c>, becomes a block on the lines after its key.
    /// </summary>
    /// <param name="entry">The entry to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="entry"/> already has a parent.</exception>
    public void AddEntry(YamlEditableMapEntry entry)
    {
        ThrowIfParented(entry);
        var previous = _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
        YamlEditableLayout.PrepareAppend(this, previous, entry);
        AddParsedEntry(entry);
        YamlEditableLayout.IndentValue(entry);
        if (previous is null)
        {
            YamlEditableLayout.AfterEmptinessChanged(this, removedEntry: null);
        }
    }

    /// <summary>
    /// Inserts an already-constructed entry at <paramref name="index"/>, reparenting it to this
    /// mapping. The new entry starts on its own line, before the comment lines of the entry that
    /// is at <paramref name="index"/> now. If this mapping is a list item whose first key is on
    /// the same line as the "-", an entry inserted at index 0 takes that place.
    /// </summary>
    /// <param name="index">The index of the new entry.</param>
    /// <param name="entry">The entry to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the number of entries.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="entry"/> already has a parent.</exception>
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
        YamlEditableLayout.IndentValue(entry);
    }

    /// <summary>
    /// Removes the entry for <paramref name="key"/> (compared as in <see cref="GetValue"/>), with
    /// its comment lines and its line. Returns <see langword="false"/> if the key was not present.
    /// If this mapping is a list item and the removed key was on the same line as the "-", the
    /// next key moves to that line.
    /// </summary>
    /// <remarks>
    /// Removing the only entry of a mapping that is a value leaves an empty mapping, written as
    /// <c>{}</c> on the line of its key or "-".
    /// </remarks>
    /// <param name="key">The key of the entry to remove.</param>
    /// <returns><see langword="true"/> if the entry was removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public bool RemoveEntry(string key)
    {
        ArgumentGuard.ThrowIfNull(key);
        var index = _entries.FindIndex(entry => HasKey(entry, key));
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
        if (_entries.Count == 0)
        {
            YamlEditableLayout.AfterEmptinessChanged(this, existing);
        }

        return true;
    }

    /// <summary>
    /// Gets or sets whether this mapping is written as "{}" when it is empty and has no parent: it was read from
    /// "{}", unlike the root of an empty document.
    /// </summary>
    internal bool WritesEmptyAsFlow { get; set; }

    /// <summary>
    /// Appends an entry read by the parser, without changing its trivia.
    /// </summary>
    internal void AddParsedEntry(YamlEditableMapEntry entry)
    {
        ThrowIfParented(entry);
        entry.Parent = this;
        _entries.Add(entry);
    }

    private static bool HasKey(YamlEditableMapEntry entry, string key) =>
        entry.Key == key || (IsQuoted(entry.Key) && string.Equals(GetQuotedKeyValue(entry.Key), key, StringComparison.Ordinal));

    private static bool IsQuoted(string rawKey) => rawKey.Length >= 2 && rawKey[0] is '"' or '\'';

    /// <summary>
    /// Returns the value of a quoted key read by the parser: without the quotes, with the escapes
    /// of a double-quoted key and the doubled quotes of a single-quoted key resolved.
    /// </summary>
    private static string GetQuotedKeyValue(string rawKey)
    {
        var scanner = new Scanner<StringLookAheadBuffer>(new StringLookAheadBuffer(rawKey));
        while (scanner.MoveNext())
        {
            if (scanner.Current is Scalar scalar)
            {
                return scalar.Value;
            }
        }

        return rawKey;
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
        if (_entries.Count == 0)
        {
            // An empty map that is the value of an entry has no block form: it is written in flow
            // style. An empty document (the root map) is written as nothing, unless it was "{}".
            if (Parent is not null || WritesEmptyAsFlow)
            {
                writer.Write("{}");
            }

            return;
        }

        foreach (var entry in _entries)
        {
            entry.WriteTo(writer);
        }
    }
}
