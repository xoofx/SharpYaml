// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A block sequence: an ordered list of "- item" entries sharing the same indentation.
/// </summary>
public sealed class YamlEditableList : YamlEditableNode
{
    private readonly List<YamlEditableListEntry> _items = [];

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="YamlEditableList"/> class.
    /// </summary>
    public YamlEditableList() : base(YamlEditableKind.List)
    {
    }

    /// <summary>
    /// Gets the entries of this sequence, in source order.
    /// </summary>
    public IReadOnlyList<YamlEditableListEntry> Items => _items;

    /// <summary>
    /// Appends an already-constructed entry, reparenting it to this list. The previous last line
    /// keeps its comment, and the new entry starts on its own line. An empty list that is a value,
    /// written as <c>[]</c>, becomes a block on the lines after its key.
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
        var previous = _items.Count > 0 ? _items[_items.Count - 1] : null;
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
    /// list. The new entry starts on its own line, before the comment lines of the entry that is
    /// at <paramref name="index"/> now. If this list is a list item whose first item is on the same
    /// line as the "-" ("- - a"), an entry inserted at index 0 takes that place.
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
        if (index == 0)
        {
            YamlEditableLayout.PrepareInsertFirst(this, _items[0], entry);
        }

        YamlEditableLayout.PrepareInsert(_items[index], entry);
        entry.Parent = this;
        _items.Insert(index, entry);
        YamlEditableLayout.IndentValue(entry);
    }

    /// <summary>
    /// Gets or sets the text this list is written as when it is empty ("[]" or "[ ]"): the text it was read from, or
    /// "[]" for a root list whose last entry was removed. An empty list that is a value is always written in flow style.
    /// </summary>
    internal string? EmptyFlowText { get; set; }

    /// <summary>
    /// Appends an entry read by the parser, without changing its trivia.
    /// </summary>
    internal void AddParsedEntry(YamlEditableListEntry entry)
    {
        ArgumentGuard.ThrowIfNull(entry);
        entry.Parent = this;
        _items.Add(entry);
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
    /// <c>[]</c> on the line of its key or "-".
    /// </summary>
    /// <param name="index">The index of the entry to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0, or not less than the number of entries.</exception>
    public void RemoveAt(int index)
    {
        var entry = _items[index];

        // The first item of a list that is a list item follows the "-" of that item ("- - a"): the next item takes
        // that place, unless comment lines are above it.
        if (index == 0 && entry.Indent.Length == 0 && Parent is YamlEditableListEntry
            && _items.Count > 1 && _items[1].LeadingTrivia is null)
        {
            _items[1].Indent = string.Empty;
        }

        entry.Parent = null;
        _items.RemoveAt(index);
        if (_items.Count == 0)
        {
            YamlEditableLayout.AfterEmptinessChanged(this, entry);
        }
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
    {
        if (_items.Count == 0)
        {
            // An empty list that is the value of an entry has no block form: it is written in flow style.
            if (Parent is not null || EmptyFlowText is not null)
            {
                writer.Write(EmptyFlowText ?? "[]");
            }

            return;
        }

        foreach (var item in _items)
        {
            item.WriteTo(writer);
        }
    }
}
