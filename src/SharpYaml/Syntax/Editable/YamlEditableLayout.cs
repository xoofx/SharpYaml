// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
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
            // lines of the document, and ends with a line break if the document did. A root read from "{}" or "[]"
            // is replaced where it was, and the line break after it stays.
            if (collection.LeadingTrivia is { Count: > 0 } leading && GetEmptyFlowText(collection) is null)
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
            else if (GetEmptyFlowText(collection) is not null && collection.LeadingTrivia is { Count: > 0 } flowLeading
                && flowLeading[flowLeading.Count - 1] is { Kind: YamlEditableTriviaKind.Whitespace } flowIndent)
            {
                // A root read from an indented "{}" or "[]": its first entry takes that indentation, which its next
                // entries then share.
                flowLeading.RemoveAt(flowLeading.Count - 1);
                SetIndent(entry, flowIndent.Text + GetIndent(entry));
            }
            else if (collection.Parent is null && GetEmptyFlowText(collection) is { } flowText && GetEndLineBreak(flowText) is { } flowLineBreak)
            {
                // A root whose entries were all removed was written "{}" with the line break of its last line.
                EnsureEndsWithNewLine(entry, flowLineBreak);
                UseNewLine(entry, flowLineBreak);
            }

            return;
        }

        // The last line ends in the end of line of its owner: a line break in its trailing trivia comes before its
        // value ("key:\n  value").
        var owner = GetLastLineOwner(previous);
        var newLine = FindNewLine(owner.EndOfLineTrivia);
        if (newLine is null)
        {
            if (GetValue(owner) is YamlEditableScalar scalar && KeepsFinalLineBreak(scalar.Text))
            {
                throw new InvalidOperationException(
                    "An entry cannot be added after a block scalar that ends the document without a line break: the line break would become part of its value.");
            }

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
        else if (collection.Parent is null && removedEntry is not null && GetEmptyFlowText(collection) is null)
        {
            // The root of the document stays a mapping or a list: empty, it is written as "{}" or "[]" on the line
            // of the removed entry.
            var lineBreak = FindNewLine(GetLastLineOwner(removedEntry).EndOfLineTrivia)?.Text;
            switch (collection)
            {
                case YamlEditableMap map:
                    map.EmptyFlowText = "{}" + lineBreak;
                    break;
                case YamlEditableList list:
                    list.EmptyFlowText = "[]" + lineBreak;
                    break;
            }
        }
    }

    private static string? GetEndLineBreak(string text) =>
        text.EndsWith("\r\n", System.StringComparison.Ordinal) ? "\r\n"
        : text.Length > 0 && text[text.Length - 1] is '\n' or '\r' ? text.Substring(text.Length - 1)
        : null;

    private static string? GetEmptyFlowText(YamlEditableNode collection) => collection switch
    {
        YamlEditableMap map => map.EmptyFlowText,
        YamlEditableList list => list.EmptyFlowText,
        _ => null,
    };

    /// <summary>
    /// Moves the line break of <paramref name="entry"/> after its value changed from a block, whose last entry was
    /// <paramref name="oldLastEntry"/>, or from a value on its line, to its current value.
    /// </summary>
    private static void AfterLayoutChanged(YamlEditableNode entry, bool wasBlock, YamlEditableNode? oldLastEntry)
    {
        var value = GetValue(entry);
        var isBlock = IsBlock(value);
        var oldOwner = oldLastEntry is null ? entry : GetLastLineOwner(oldLastEntry);
        var newLine = FindNewLine(oldOwner.EndOfLineTrivia);

        if (entry is YamlEditableMapEntry && !wasBlock && isBlock)
        {
            var oldEndOfLine = entry.EndOfLineTrivia;
            entry.EndOfLineTrivia = null;
            var separatorBreak = entry.TrailingTrivia?.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) ?? -1;
            if (separatorBreak >= 0)
            {
                // "key: # comment\n  {}" already ends the line of the key: the block replaces the rest, and a comment
                // after the old value goes to the last line of the block.
                entry.TrailingTrivia = entry.TrailingTrivia!.GetRange(0, separatorBreak + 1);
                var lastOwner = GetLastLineOwner(entry);
                if (HasComment(oldEndOfLine) && !HasComment(lastOwner.EndOfLineTrivia))
                {
                    lastOwner.EndOfLineTrivia = oldEndOfLine;
                }
            }
            else
            {
                // "key: value # comment\n" becomes "key: # comment\n" followed by the nested block.
                var separator = oldEndOfLine ?? [];
                if (FindNewLine(separator) is null)
                {
                    separator.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine?.Text ?? GetDocumentNewLine(entry)));
                }

                entry.TrailingTrivia = separator;
            }
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
            // Only what is new takes the line break of the document: the trivia of the entry keeps its text.
            EnsureEndsWithNewLine(entry, newLine);
            if (value is not null)
            {
                UseNewLine(value, newLine.Text);
            }
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
    public static string GetKeyColumn(YamlEditableMapEntry entry) => GetColumn(entry);

    /// <summary>
    /// Returns the indentation of <paramref name="entry"/>, a map or a list entry, as a column, also when it shares its
    /// line with the "-" of a list entry ("- key: value", "- - item") and so has no indentation text of its own.
    /// </summary>
    private static string GetColumn(YamlEditableNode entry)
    {
        var indent = GetIndent(entry);
        if (indent.Length > 0 || entry.Parent?.Parent is not YamlEditableListEntry listEntry)
        {
            return indent;
        }

        var width = GetColumn(listEntry).Length + 1;
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
        map.Entries.Count > 0 && map.Entries[0] == entry && SharesLineWithDash(map);

    /// <summary>
    /// Prepares <paramref name="entry"/> to be inserted at the start of <paramref name="collection"/>, before
    /// <paramref name="next"/>: if <paramref name="next"/> follows the "-" of a list item ("- key: value",
    /// "- - item"), <paramref name="entry"/> takes its place there and <paramref name="next"/> goes to the column it had.
    /// </summary>
    public static void PrepareInsertFirst(YamlEditableNode collection, YamlEditableNode next, YamlEditableNode entry)
    {
        if (!SharesLineWithDash(collection))
        {
            return;
        }

        var column = GetColumn(next);
        switch (next)
        {
            case YamlEditableMapEntry mapEntry:
                mapEntry.Indent = column;
                ((YamlEditableMapEntry)entry).Indent = string.Empty;
                break;
            case YamlEditableListEntry listEntry:
                listEntry.Indent = column;
                ((YamlEditableListEntry)entry).Indent = string.Empty;
                break;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the first entry of <paramref name="collection"/>, a map or a list that is a
    /// list item, follows the "-" of that item, without indentation of its own.
    /// </summary>
    private static bool SharesLineWithDash(YamlEditableNode collection) =>
        collection.Parent is YamlEditableListEntry && GetEntries(collection) is { Count: > 0 } entries && GetIndent(entries[0]).Length == 0;

    /// <summary>
    /// Indents the nested map or list of <paramref name="entry"/> if its entries are not at a column
    /// that belongs to <paramref name="entry"/>, then does the same for each nested entry. A map or a
    /// list built in memory does not know its depth: without this, "c:" followed by a new map with
    /// "x: 1" at column 0 would make "x" a key of the outer map. A valid layout is never changed.
    /// </summary>
    public static void IndentValue(YamlEditableNode entry)
    {
        var value = GetValue(entry);
        if (!IsBlock(value))
        {
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
            if (entry is YamlEditableListEntry)
            {
                // The keys or the items of a list item are at the column of its first one, after the "-".
                target = GetColumn(GetEntries(value!)[0]).Length;
            }
            else
            {
                var column = GetColumn(entry).Length;

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
    /// the first key or item of a list item, which is on the line of the "-".
    /// </summary>
    private static List<YamlEditableNode> GetOwnLineEntries(YamlEditableNode value)
    {
        var entries = GetEntries(value);
        if (SharesLineWithDash(value))
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

    private static void SetIndent(YamlEditableNode entry, string indent)
    {
        switch (entry)
        {
            case YamlEditableMapEntry mapEntry:
                mapEntry.Indent = indent;
                break;
            case YamlEditableListEntry listEntry:
                listEntry.Indent = indent;
                break;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="text"/> is a block scalar whose value ends with the line break
    /// of its last line ("|", "|+", ">"), unlike the strip indicator ("|-").
    /// </summary>
    private static bool KeepsFinalLineBreak(string text)
    {
        if (text.Length == 0 || text[0] is not ('|' or '>'))
        {
            return false;
        }

        for (var index = 1; index < text.Length && text[index] is not (' ' or '\t' or '\r' or '\n' or '#'); index++)
        {
            if (text[index] == '-')
            {
                return false;
            }
        }

        return true;
    }

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
        if (newLine is null && node is YamlEditableScalar scalar && scalar.Text.IndexOfAny(['\r', '\n']) is var index and >= 0)
        {
            // A quoted or block scalar on several lines holds its line breaks in its text.
            var text = scalar.Text;
            var length = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
            newLine = new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, text.Substring(index, length));
        }

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
