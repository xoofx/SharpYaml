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
    /// keeps its comment, and the new entry starts on its own line.
    /// </summary>
    public void AddEntry(YamlEditableListEntry entry)
    {
        ThrowIfParented(entry);
        YamlEditableLayout.PrepareAppend(_items.Count > 0 ? _items[_items.Count - 1] : null, entry);
        AddParsedEntry(entry);
    }

    /// <summary>
    /// Inserts an already-constructed entry at <paramref name="index"/>, reparenting it to this
    /// list. The new entry starts on its own line, before the comment lines of the entry that is
    /// at <paramref name="index"/> now.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0 or greater than the number of entries.</exception>
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

        ThrowIfParented(entry);
        YamlEditableLayout.PrepareInsert(_items[index], entry);
        entry.Parent = this;
        _items.Insert(index, entry);
    }

    /// <summary>
    /// Appends an entry read by the parser, without changing its trivia.
    /// </summary>
    internal void AddParsedEntry(YamlEditableListEntry entry)
    {
        ThrowIfParented(entry);
        entry.Parent = this;
        _items.Add(entry);
    }

    private static void ThrowIfParented(YamlEditableListEntry entry)
    {
        ArgumentGuard.ThrowIfNull(entry);
        if (entry.Parent != null)
        {
            throw new InvalidOperationException("The entry is already parented to another node.");
        }
    }

    /// <summary>
    /// Removes the entry at <paramref name="index"/>, with its comment lines and its lines.
    /// </summary>
    public void RemoveAt(int index)
    {
        var entry = _items[index];
        entry.Parent = null;
        _items.RemoveAt(index);
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
    {
        if (_items.Count == 0)
        {
            // An empty list that is the value of an entry has no block form: it is written in flow style.
            if (Parent is not null)
            {
                writer.Write("[]");
            }

            return;
        }

        foreach (var item in _items)
        {
            item.WriteTo(writer);
        }
    }
}
