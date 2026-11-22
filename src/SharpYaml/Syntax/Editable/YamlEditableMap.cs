// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
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
        return FindEntry(key)?.Value;
    }

    /// <summary>
    /// Sets the value for <paramref name="key"/>. If the key already exists (compared as in
    /// <see cref="GetValue"/>), its value node is replaced in place, keeping the entry's original
    /// key text, indentation and surrounding comments. If the key does not exist yet, a new entry
    /// is appended with <paramref name="key"/> written as a plain scalar, at the column of the last
    /// existing key (no indentation for the first key of the root).
    /// </summary>
    /// <param name="key">The value of the key.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The key does not exist and cannot be written as a plain scalar (for example an empty key, or one that needs
    /// quotes): add a <see cref="YamlEditableMapEntry"/> with the quoted text of the key instead.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, is this mapping or one of its ancestors, or is the root of a tree;
    /// or the new entry would follow a block scalar that ends the document without a line break.
    /// </exception>
    public void SetValue(string key, YamlEditableNode value) => SetValue(key, value, null);

    /// <summary>
    /// Sets the value for <paramref name="key"/>, as <see cref="SetValue(string, YamlEditableNode)"/> does, and
    /// writes a new key after <paramref name="indent"/>.
    /// </summary>
    /// <param name="key">The value of the key.</param>
    /// <param name="value">The new value.</param>
    /// <param name="indent">
    /// The indentation text of a new key, or <see langword="null"/> for the column of the last existing key.
    /// Ignored when the key exists.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The key does not exist and cannot be written as a plain scalar (for example an empty key, or one that needs
    /// quotes): add a <see cref="YamlEditableMapEntry"/> with the quoted text of the key instead.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, is this mapping or one of its ancestors, or is the root of a tree;
    /// or the new entry would follow a block scalar that ends the document without a line break.
    /// </exception>
    public void SetValue(string key, YamlEditableNode value, string? indent)
    {
        ArgumentGuard.ThrowIfNull(key);
        ArgumentGuard.ThrowIfNull(value);
        ThrowIfSelfOrAncestor(value, this);

        var existing = FindEntry(key);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        if (!IsPlainKey(key))
        {
            throw new ArgumentException(
                "A new key is written as a plain scalar, and this key cannot be one: add a YamlEditableMapEntry with the quoted text of the key instead.",
                nameof(key));
        }

        // Checked before the entry is built, which parents the value.
        YamlEditableLayout.ThrowIfCannotAppend(this);
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
    /// <exception cref="ArgumentException">
    /// The key text of <paramref name="entry"/> is not one plain or quoted scalar on one line, or the mapping already has an
    /// entry with that key.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="entry"/> already has a parent or contains this mapping, or the line before it is the last line of
    /// a block scalar that ends the document without a line break (adding one would change its value).
    /// </exception>
    public void AddEntry(YamlEditableMapEntry entry)
    {
        ThrowIfCannotAdd(entry);
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
    /// <exception cref="ArgumentException">
    /// The key text of <paramref name="entry"/> is not one plain or quoted scalar on one line, or the mapping already has an
    /// entry with that key.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="entry"/> already has a parent or contains this mapping, or <paramref name="index"/> is the number of
    /// entries and the entry cannot be added there (see <see cref="AddEntry"/>).
    /// </exception>
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

        ThrowIfCannotAdd(entry);
        var next = _entries[index];
        if (index == 0)
        {
            YamlEditableLayout.PrepareInsertFirst(this, next, entry);
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
    /// Gets or sets the text this mapping is written as when it is empty ("{}" or "{ }"): the text it was read from,
    /// or "{}" for a root mapping whose last entry was removed. <see langword="null"/> for the root of a document
    /// without content, which is written as nothing; an empty mapping that is a value is always written in flow style.
    /// </summary>
    internal string? EmptyFlowText { get; set; }

    /// <summary>
    /// Appends an entry read by the parser, without changing its trivia.
    /// </summary>
    internal void AddParsedEntry(YamlEditableMapEntry entry)
    {
        ArgumentGuard.ThrowIfNull(entry);
        entry.Parent = this;
        _entries.Add(entry);
    }

    private YamlEditableMapEntry? FindEntry(string key) => _entries.Find(entry => HasKey(entry, key));

    /// <summary>
    /// Returns <see langword="true"/> if the value of the key of <paramref name="entry"/> is <paramref name="key"/>.
    /// The value of a plain key is its text; a quoted key is compared without its quotes and escapes, so that one
    /// key is never found under two spellings.
    /// </summary>
    private static bool HasKey(YamlEditableMapEntry entry, string key) =>
        string.Equals(GetKeyValue(entry.Key), key, StringComparison.Ordinal);

    private static string GetKeyValue(string rawKey) => IsQuoted(rawKey) ? GetQuotedKeyValue(rawKey) : rawKey;

    private static bool IsQuoted(string rawKey) => rawKey.Length >= 2 && rawKey[0] is '"' or '\'';

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="key"/> can be written as a plain key: written before ": ",
    /// it reads back as one plain scalar whose value is <paramref name="key"/>.
    /// </summary>
    private static bool IsPlainKey(string key) =>
        TryReadKey(key, out var value, out var style) && style == ScalarStyle.Plain && string.Equals(value, key, StringComparison.Ordinal);

    /// <summary>
    /// Reads <paramref name="rawKey"/> as the text of an implicit key: one plain or quoted scalar on one line, of at
    /// most 1024 characters (YAML 1.2.2, 7.4, counted in UTF-16 code units as the scanner does), that is all of the text.
    /// </summary>
    private static bool TryReadKey(string rawKey, out string value, out ScalarStyle style)
    {
        value = string.Empty;
        style = ScalarStyle.Any;
        if (rawKey.Length is 0 or > 1024 || rawKey.IndexOfAny(['\r', '\n']) >= 0)
        {
            return false;
        }

        try
        {
            var scanner = new Scanner<StringLookAheadBuffer>(new StringLookAheadBuffer(rawKey + ": x"));
            while (scanner.MoveNext())
            {
                switch (scanner.Current)
                {
                    case StreamStart or BlockMappingStart:
                        continue;
                    case Key { Start.Index: 0, End.Index: 0 }:
                        continue;
                    case Scalar scalar when scalar.Start.Index == 0 && scalar.End.Index == rawKey.Length
                        && scalar.Style is ScalarStyle.Plain or ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted:
                        // The scanner reads the ":" after the scalar as the indicator of an implicit key.
                        value = scalar.Value;
                        style = scalar.Style;
                        return scanner.MoveNext() && scanner.Current is Value;
                    default:
                        return false;
                }
            }
        }
        catch (YamlException)
        {
            // The text is not valid YAML as a key.
        }

        return false;
    }

    /// <summary>
    /// Throws if <paramref name="rawKey"/> cannot be the key text of <paramref name="entry"/> in this map: it must be
    /// one scalar key, and no other entry may have the same key.
    /// </summary>
    internal void ThrowIfInvalidKey(string rawKey, YamlEditableMapEntry entry)
    {
        if (!TryReadKey(rawKey, out var value, out _))
        {
            throw new ArgumentException($"The key text '{rawKey}' is not one plain or quoted scalar on one line.", nameof(entry));
        }

        if (_entries.Find(other => !ReferenceEquals(other, entry) && HasKey(other, value)) is not null)
        {
            throw new ArgumentException($"The mapping already has an entry with the key '{value}'.", nameof(entry));
        }
    }

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

    private void ThrowIfCannotAdd(YamlEditableMapEntry entry)
    {
        ArgumentGuard.ThrowIfNull(entry);
        if (entry.Parent != null)
        {
            throw new InvalidOperationException("The entry is already parented to another node.");
        }

        ThrowIfSelfOrAncestor(entry, this);
        ThrowIfInvalidKey(entry.Key, entry);
        YamlEditableLayout.ThrowIfUnsupportedValue(entry);
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
    {
        if (_entries.Count == 0)
        {
            // An empty map that is the value of an entry has no block form: it is written in flow
            // style. The root of a document without content is written as nothing.
            if (Parent is not null || EmptyFlowText is not null)
            {
                var text = EmptyFlowText ?? "{}";
                (writer as YamlEditableTextWriter)?.Record(text);
                writer.Write(text);
            }

            return;
        }

        foreach (var entry in _entries)
        {
            entry.WriteTo(writer);
        }
    }
}
