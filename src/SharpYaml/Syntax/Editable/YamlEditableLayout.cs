// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// Keeps line breaks consistent when entries are added, inserted, removed or given a new value.
/// </summary>
/// <remarks>
/// Each line ends in the <see cref="YamlEditableNode.EndOfLineTrivia"/> of the entry whose scalar
/// value is on that line, or in the <see cref="YamlEditableNode.TrailingTrivia"/> of an entry
/// whose value is a nested block ("key:" or "-" followed by a line break). The leading trivia of
/// an entry only holds whole lines (comments, blank lines) above it.
/// </remarks>
internal static class YamlEditableLayout
{
    private const string DefaultNewLine = "\n";

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="node"/> is written as a block on the lines after
    /// "key:" or "-". An empty map or list is written on the same line, as "{}" or "[]".
    /// </summary>
    public static bool IsBlock(YamlEditableNode? node) =>
        node is YamlEditableMap { Entries.Count: > 0 } or YamlEditableList { Items.Count: > 0 };

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="node"/> is a missing value ("key:" or "-" with
    /// nothing after it), which is written without the space after the ":" or the "-".
    /// </summary>
    public static bool IsMissingValue(YamlEditableNode? node) =>
        node is YamlEditableScalar { Text.Length: 0 };

    /// <summary>
    /// Prepares <paramref name="entry"/> to be appended to <paramref name="collection"/>, after
    /// <paramref name="previous"/>, its last entry if it has one.
    /// </summary>
    public static void PrepareAppend(YamlEditableNode collection, YamlEditableNode? previous, YamlEditableNode entry)
    {
        if (previous is null)
        {
            // The first entry of a document without content goes on a line of its own after the comments and blank
            // lines of the document, and ends with a line break if the document did.
            if (collection.LeadingTrivia is { Count: > 0 } leading)
            {
                var lineBreak = FindNewLine(leading)?.Text ?? DefaultNewLine;
                if (leading[leading.Count - 1].Kind != YamlEditableTriviaKind.NewLine)
                {
                    leading.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, lineBreak));
                }
                else
                {
                    EnsureEndsWithNewLine(entry, lineBreak);
                }

                UseNewLine(entry, lineBreak);
            }

            return;
        }

        var owner = GetLastLineOwner(previous);
        var newLine = FindNewLine(owner.EndOfLineTrivia) ?? FindNewLine(owner.TrailingTrivia);
        if (newLine is null)
        {
            // The document has no final line break: end the previous line, and leave the new last
            // line without one, as the document was.
            var documentNewLine = GetDocumentNewLine(previous);
            (owner.EndOfLineTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, documentNewLine));
            UseNewLine(entry, documentNewLine);
            return;
        }

        EnsureEndsWithNewLine(entry, newLine);
        UseNewLine(entry, newLine.Text);
    }

    /// <summary>
    /// Prepares <paramref name="entry"/> to be inserted before <paramref name="next"/>.
    /// </summary>
    public static void PrepareInsert(YamlEditableNode next, YamlEditableNode entry)
    {
        var newLine = FindNewLine(GetLastLineOwner(next).EndOfLineTrivia)?.Text ?? GetDocumentNewLine(next);
        EnsureEndsWithNewLine(entry, newLine);
        UseNewLine(entry, newLine);
    }

    /// <summary>
    /// Moves the line break of an entry after its value changed from <paramref name="oldValue"/>
    /// to <paramref name="entry"/>'s current value.
    /// </summary>
    public static void AfterValueChanged(YamlEditableNode entry, YamlEditableNode oldValue) =>
        AfterLayoutChanged(entry, IsBlock(oldValue), GetLastEntry(oldValue));

    /// <summary>
    /// Moves the line break of the entry whose value is <paramref name="collection"/>, a map or a list that got its
    /// first entry or lost its last one, <paramref name="removedEntry"/>: an empty map or list is written as "{}" or
    /// "[]" on the line of "key:" or "-", and a map or list with entries as a block.
    /// </summary>
    public static void AfterEmptinessChanged(YamlEditableNode collection, YamlEditableNode? removedEntry)
    {
        if (collection.Parent is YamlEditableMapEntry or YamlEditableListEntry)
        {
            AfterLayoutChanged(collection.Parent, wasBlock: removedEntry is not null, removedEntry);
            IndentValue(collection.Parent);
        }
    }

    /// <summary>
    /// Moves the line break of <paramref name="entry"/> after its value changed from a block, whose last entry was
    /// <paramref name="oldLastEntry"/>, or from a value on its line, to its current value.
    /// </summary>
    private static void AfterLayoutChanged(YamlEditableNode entry, bool wasBlock, YamlEditableNode? oldLastEntry)
    {
        var value = GetValue(entry);
        var isBlock = IsBlock(value);
        var oldOwner = oldLastEntry is null ? entry : GetLastLineOwner(oldLastEntry);
        var newLine = FindNewLine(oldOwner.EndOfLineTrivia) ?? (oldLastEntry is null ? null : FindNewLine(oldOwner.TrailingTrivia));

        if (entry is YamlEditableMapEntry && !wasBlock && isBlock)
        {
            // "key: value # comment\n" becomes "key: # comment\n" followed by the nested block.
            entry.TrailingTrivia = FindNewLine(entry.EndOfLineTrivia) is not null
                ? entry.EndOfLineTrivia
                : [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine?.Text ?? GetDocumentNewLine(entry))];
            entry.EndOfLineTrivia = null;
        }
        else if (entry is YamlEditableMapEntry && wasBlock && !isBlock)
        {
            // "key: # comment\n" followed by a nested block becomes "key: value # comment\n".
            entry.EndOfLineTrivia = entry.TrailingTrivia is { } separator && FindNewLine(separator) is not null ? separator : null;
            entry.TrailingTrivia = null;
        }
        else if (entry is YamlEditableListEntry && !wasBlock && isBlock && FindNewLine(entry.EndOfLineTrivia) is not null)
        {
            // "- value # comment\n" becomes "- key: value # comment\n" followed by the other lines of the block: the
            // first line of the block is the line of the "-".
            var first = GetEntries(value!)[0];
            if (!IsBlock(GetValue(first)) && !HasComment(first.EndOfLineTrivia))
            {
                first.EndOfLineTrivia = entry.EndOfLineTrivia;
                entry.EndOfLineTrivia = null;
            }
        }

        if (newLine is not null)
        {
            EnsureEndsWithNewLine(entry, newLine);
            UseNewLine(entry, newLine.Text);
        }
    }

    /// <summary>
    /// Writes every line break of <paramref name="node"/> and of its nested entries as
    /// <paramref name="newLine"/>, the line break of the document. A map or a list built in memory
    /// gets "\n" between its entries, and "key:" before a nested block gets an explicit line break.
    /// </summary>
    private static void UseNewLine(YamlEditableNode node, string newLine)
    {
        UseNewLine(node.LeadingTrivia, newLine);
        UseNewLine(node.TrailingTrivia, newLine);
        UseNewLine(node.EndOfLineTrivia, newLine);

        if (node is YamlEditableMapEntry { TrailingTrivia: null } mapEntry && IsBlock(mapEntry.Value))
        {
            mapEntry.TrailingTrivia = [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine)];
        }

        if (GetValue(node) is { } value)
        {
            UseNewLine(value, newLine);
        }

        foreach (var child in GetEntries(node))
        {
            UseNewLine(child, newLine);
        }
    }

    private static void UseNewLine(List<YamlEditableTrivia>? trivia, string newLine)
    {
        if (trivia is null)
        {
            return;
        }

        for (var index = 0; index < trivia.Count; index++)
        {
            if (trivia[index].Kind == YamlEditableTriviaKind.NewLine && trivia[index].Text != newLine)
            {
                trivia[index] = new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine);
            }
        }
    }

    /// <summary>
    /// Returns the indentation of <paramref name="entry"/>'s key as a column, also when the key
    /// shares its line with the "-" of a list entry and so has no indentation text of its own.
    /// </summary>
    public static string GetKeyColumn(YamlEditableMapEntry entry)
    {
        if (entry.Indent.Length > 0 || entry.Parent?.Parent is not YamlEditableListEntry listEntry)
        {
            return entry.Indent;
        }

        var width = listEntry.Indent.Length + 1;
        if (listEntry.TrailingTrivia is null)
        {
            width++;
        }
        else
        {
            // The key follows the "-" on its line, or starts the line after a comment ("- # comment\n  key: value").
            foreach (var trivia in listEntry.TrailingTrivia)
            {
                width = trivia.Kind == YamlEditableTriviaKind.NewLine ? 0 : width + trivia.Text.Length;
            }
        }

        return new string(' ', width);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="entry"/> is the first key of a map that
    /// is a list item, written on the same line as the "-".
    /// </summary>
    public static bool SharesLineWithDash(YamlEditableMapEntry entry, YamlEditableMap map) =>
        entry.Indent.Length == 0 && map.Parent is YamlEditableListEntry && map.Entries.Count > 0 && map.Entries[0] == entry;

    /// <summary>
    /// Indents the nested map or list of <paramref name="entry"/> if its entries are not at a column
    /// that belongs to <paramref name="entry"/>, then does the same for each nested entry. A map or a
    /// list built in memory does not know its depth: without this, "c:" followed by a new map with
    /// "x: 1" at column 0 would make "x" a key of the outer map. A valid layout is never changed.
    /// </summary>
    public static void IndentValue(YamlEditableNode entry)
    {
        var value = GetValue(entry);
        if (!IsBlock(value) || (entry is YamlEditableListEntry && value is YamlEditableList))
        {
            // A list directly in a list item ("- - a") keeps the layout it was given.
            return;
        }

        var ownLineIndents = GetOwnLineEntries(value!).ConvertAll(GetIndent);
        if (ownLineIndents.Count > 0)
        {
            var current = int.MaxValue;
            foreach (var indent in ownLineIndents)
            {
                current = System.Math.Min(current, indent.Length);
            }

            int target;
            if (entry is YamlEditableListEntry && value is YamlEditableMap itemMap)
            {
                // The keys of a list item are at the column of its first key, after the "-".
                target = GetKeyColumn(itemMap.Entries[0]).Length;
            }
            else
            {
                var column = entry is YamlEditableMapEntry mapEntry ? GetKeyColumn(mapEntry).Length : ((YamlEditableListEntry)entry).Indent.Length;

                // A nested map is indented past its key; a sequence may also be at the key's column.
                var isValid = value is YamlEditableList ? current >= column : current > column;
                target = isValid ? current : column + 2;
            }

            if (current < target)
            {
                Shift(value!, new string(' ', target - current));
            }
        }

        foreach (var child in GetEntries(value!))
        {
            IndentValue(child);
        }
    }

    private static YamlEditableNode? GetValue(YamlEditableNode entry) => entry switch
    {
        YamlEditableMapEntry mapEntry => mapEntry.Value,
        YamlEditableListEntry listEntry => listEntry.Value,
        _ => null,
    };

    private static List<YamlEditableNode> GetEntries(YamlEditableNode value) => value switch
    {
        YamlEditableMap map => new List<YamlEditableNode>(map.Entries),
        YamlEditableList list => new List<YamlEditableNode>(list.Items),
        _ => [],
    };

    /// <summary>
    /// Returns the entries of <paramref name="value"/> that start their own line: all of them, except
    /// the first key of a list item, which is on the line of the "-".
    /// </summary>
    private static List<YamlEditableNode> GetOwnLineEntries(YamlEditableNode value)
    {
        var entries = GetEntries(value);
        if (value is YamlEditableMap map && entries.Count > 0 && SharesLineWithDash(map.Entries[0], map))
        {
            entries.RemoveAt(0);
        }

        return entries;
    }

    private static string GetIndent(YamlEditableNode entry) => entry switch
    {
        YamlEditableMapEntry mapEntry => mapEntry.Indent,
        YamlEditableListEntry listEntry => listEntry.Indent,
        _ => string.Empty,
    };

    /// <summary>
    /// Adds <paramref name="padding"/> before every line of <paramref name="value"/> that starts with
    /// an entry, at every depth.
    /// </summary>
    private static void Shift(YamlEditableNode value, string padding)
    {
        foreach (var entry in GetOwnLineEntries(value))
        {
            switch (entry)
            {
                case YamlEditableMapEntry mapEntry:
                    mapEntry.Indent = padding + mapEntry.Indent;
                    break;
                case YamlEditableListEntry listEntry:
                    listEntry.Indent = padding + listEntry.Indent;
                    break;
            }
        }

        foreach (var entry in GetEntries(value))
        {
            if (GetValue(entry) is { } nested && IsBlock(nested))
            {
                Shift(nested, padding);
            }
        }
    }

    private static void EnsureEndsWithNewLine(YamlEditableNode entry, YamlEditableTrivia newLine)
    {
        var owner = GetLastLineOwner(entry);
        if (FindNewLine(owner.EndOfLineTrivia) is null)
        {
            (owner.EndOfLineTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine.Text));
        }
    }

    private static void EnsureEndsWithNewLine(YamlEditableNode entry, string newLine) =>
        EnsureEndsWithNewLine(entry, new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine));

    private static YamlEditableTrivia? FindNewLine(List<YamlEditableTrivia>? trivia) =>
        trivia?.Find(t => t.Kind == YamlEditableTriviaKind.NewLine);

    private static bool HasComment(List<YamlEditableTrivia>? trivia) =>
        trivia is not null && trivia.Exists(t => t.Kind == YamlEditableTriviaKind.Comment);

    /// <summary>
    /// Returns the line break of the document of <paramref name="node"/>: the first one of its tree, or "\n" if it has none.
    /// </summary>
    private static string GetDocumentNewLine(YamlEditableNode node)
    {
        var root = node;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        return FindNewLineInTree(root)?.Text ?? DefaultNewLine;
    }

    private static YamlEditableTrivia? FindNewLineInTree(YamlEditableNode node)
    {
        var newLine = FindNewLine(node.LeadingTrivia) ?? FindNewLine(node.TrailingTrivia) ?? FindNewLine(node.EndOfLineTrivia);
        if (newLine is null && GetValue(node) is { } value)
        {
            newLine = FindNewLineInTree(value);
        }

        foreach (var child in GetEntries(node))
        {
            newLine ??= FindNewLineInTree(child);
        }

        return newLine;
    }

    private static YamlEditableNode? GetLastEntry(YamlEditableNode? value) => value switch
    {
        YamlEditableMap map when map.Entries.Count > 0 => map.Entries[map.Entries.Count - 1],
        YamlEditableList list when list.Items.Count > 0 => list.Items[list.Items.Count - 1],
        _ => null,
    };

    /// <summary>
    /// Returns the entry whose <see cref="YamlEditableNode.EndOfLineTrivia"/> ends the last line
    /// of <paramref name="entry"/>: the entry itself, or the last entry of its nested blocks.
    /// </summary>
    private static YamlEditableNode GetLastLineOwner(YamlEditableNode entry)
    {
        while (true)
        {
            var value = entry switch
            {
                YamlEditableMapEntry mapEntry => mapEntry.Value,
                YamlEditableListEntry listEntry => listEntry.Value,
                _ => null,
            };

            var last = GetLastEntry(value);
            if (last is null)
            {
                return entry;
            }

            entry = last;
        }
    }
}
