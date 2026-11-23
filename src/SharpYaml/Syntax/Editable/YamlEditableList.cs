// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A block sequence: an ordered list of "- item" entries sharing the same indentation.
/// </summary>
public sealed class YamlEditableList : YamlEditableNode
{
    private readonly List<YamlEditableListEntry> _items = [];
    private readonly ReadOnlyCollection<YamlEditableListEntry> _itemsView;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="YamlEditableList"/> class.
    /// </summary>
    public YamlEditableList() : base(YamlEditableKind.List)
    {
        _itemsView = _items.AsReadOnly();
    }

    /// <summary>
    /// Gets the entries of this sequence, in source order.
    /// </summary>
    public IReadOnlyList<YamlEditableListEntry> Items => _itemsView;

    /// <summary>
    /// Appends an already-constructed entry, reparenting it to this list. The previous last line
    /// keeps its comment, and the new entry starts on its own line, at the column of the other
    /// items; the lines of its value move with it. An empty list that is a value, written as
    /// <c>[]</c>, becomes a block on the lines after its key.
    /// </summary>
    /// <param name="entry">The entry to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="entry"/> already has a parent or contains this list, or the line before it is the last line of a
    /// block scalar that ends the document without a line break (adding one would change its value).
    /// </exception>
    public void AddEntry(YamlEditableListEntry entry)
    {
        ThrowIfCannotAdd(entry);
        YamlEditableLayout.Insert(this, _items.Count, entry);
    }

    /// <summary>
    /// Inserts an already-constructed entry at <paramref name="index"/>, reparenting it to this
    /// list. The new entry starts on its own line, at the column of the other items, before the
    /// comment lines of the entry that is at <paramref name="index"/> now; the lines of its value
    /// move with it. If this list is a list item whose first item is on the same line as the "-"
    /// ("- - a"), an entry inserted at index 0 takes that place.
    /// </summary>
    /// <param name="index">The index of the new entry.</param>
    /// <param name="entry">The entry to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the number of entries.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="entry"/> already has a parent or contains this list, or <paramref name="index"/> is the number of
    /// entries and the entry cannot be added there (see <see cref="AddEntry"/>).
    /// </exception>
    public void InsertEntry(int index, YamlEditableListEntry entry)
    {
        if (index < 0 || index > _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == _items.Count)
        {
            AddEntry(entry);
            return;
        }

        ThrowIfCannotAdd(entry);
        YamlEditableLayout.Insert(this, index, entry);
    }

    /// <summary>
    /// Gets or sets the text this list is written as when it is empty ("[]" or "[ ]"): the text it was read from, or
    /// "[]" for a root list whose last entry was removed. An empty list that is a value is always written in flow style.
    /// </summary>
    internal string? EmptyFlowText { get; set; }

    /// <summary>
    /// Appends an entry read by the parser, without changing its trivia.
    /// </summary>
    internal void AddParsedEntry(YamlEditableListEntry entry) => InsertParsedEntry(_items.Count, entry);

    /// <summary>
    /// Inserts an entry at <paramref name="index"/>, without changing its trivia.
    /// </summary>
    internal void InsertParsedEntry(int index, YamlEditableListEntry entry)
    {
        entry.Parent = this;
        _items.Insert(index, entry);
    }

    /// <summary>
    /// Removes the entry at <paramref name="index"/>, without changing its trivia.
    /// </summary>
    internal void RemoveParsedEntryAt(int index)
    {
        _items[index].Parent = null;
        _items.RemoveAt(index);
    }

    private void ThrowIfCannotAdd(YamlEditableListEntry entry)
    {
        ArgumentGuard.ThrowIfNull(entry);
        if (entry.Parent != null)
        {
            throw new InvalidOperationException("The entry is already parented to another node.");
        }

        ThrowIfSelfOrAncestor(entry, this);
        YamlEditableLayout.ThrowIfUnsupportedValue(entry);
    }

    /// <summary>
    /// Removes the entry at <paramref name="index"/>, with its comment lines and its lines.
    /// Removing the only entry of a list that is a value leaves an empty list, written as
    /// <c>[]</c> where it was before entries were added to it, or else on the line of its key or
    /// "-". The removed entry can be added again, to this list or another one.
    /// </summary>
    /// <param name="index">The index of the entry to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0, or not less than the number of entries.</exception>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        YamlEditableLayout.Remove(this, index);
    }

    /// <inheritdoc />
    private protected override void WriteCore(TextWriter writer)
    {
        if (_items.Count == 0)
        {
            // An empty list that is the value of an entry has no block form: it is written in flow style.
            if (Parent is not null || EmptyFlowText is not null)
            {
                var text = EmptyFlowText ?? "[]";
                (writer as YamlEditableTextWriter)?.Record(this, text);
                writer.Write(text);
            }

            return;
        }

        foreach (var item in _items)
        {
            item.WriteTo(writer);
        }
    }
}
