// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

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
                // A root read from an indented "{}" or "[]": its first entry takes that indentation, unless it has its
                // own, and its next entries then share it.
                flowLeading.RemoveAt(flowLeading.Count - 1);
                if (GetIndent(entry).Length == 0)
                {
                    SetIndent(entry, flowIndent.Text);
                }
            }
            else if (collection.Parent is null && GetEmptyFlowText(collection) is { } flowText && GetEndLineBreak(flowText) is { } flowLineBreak)
            {
                // A root whose entries were all removed was written "{}" with the line break of its last line.
                EnsureEndsWithNewLine(entry, flowLineBreak);
            }

            if (GetEmptyFlowText(collection) is not null)
            {
                UseNewLine(entry, GetDocumentNewLine(collection));

                // The comment on the line of a root "{} # comment" goes to the line of its first entry.
                if (collection.Tree?.TrailingTrivia is { } rootTrailing
                    && rootTrailing.FindIndex(t => t.Kind == YamlEditableTriviaKind.Comment) is var rootComment and >= 0
                    && rootTrailing.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) is var rootBreak
                    && (rootBreak < 0 || rootComment < rootBreak))
                {
                    var start = rootComment;
                    while (start > 0 && rootTrailing[start - 1].Kind == YamlEditableTriviaKind.Whitespace)
                    {
                        start--;
                    }

                    var comment = rootTrailing.GetRange(start, rootComment - start + 1);
                    rootTrailing.RemoveRange(start, rootComment - start + 1);
                    var endOfLine = GetLastLineOwner(entry).EndOfLineTrivia ??= [];
                    var lineBreak = endOfLine.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
                    endOfLine.InsertRange(lineBreak < 0 ? endOfLine.Count : lineBreak, comment);
                }
            }

            KeepCommentsOutOfBlockScalars(entry);
            return;
        }

        // The last line ends in the end of line of its owner: a line break in its trailing trivia comes before its
        // value ("key:\n  value").
        ThrowIfCannotAppend(collection);
        var owner = GetLastLineOwner(previous);
        var newLine = FindNewLine(owner.EndOfLineTrivia);
        if (newLine is null)
        {
            // The document has no final line break: end the previous line, and leave the new last
            // line without one, as the document was.
            var documentNewLine = GetDocumentNewLine(previous);
            (owner.EndOfLineTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, documentNewLine));
            UseNewLine(entry, documentNewLine);
        }
        else
        {
            EnsureEndsWithNewLine(entry, newLine);
            UseNewLine(entry, newLine.Text);
        }

        KeepCommentsOutOfBlockScalars(entry);
    }

    /// <summary>
    /// Throws if an entry cannot be appended to <paramref name="collection"/>: its last line is the last line of a
    /// block scalar ("|", ">") that ends the document without a line break, and the line break that the new
    /// entry needs would become part of the value of that scalar.
    /// </summary>
    public static void ThrowIfCannotAppend(YamlEditableNode collection)
    {
        if (GetLastEntry(collection) is not { } last)
        {
            return;
        }

        var owner = GetLastLineOwner(last);
        if (FindNewLine(owner.EndOfLineTrivia) is null && GetValue(owner) is YamlEditableScalar scalar
            && KeepsFinalLineBreak(scalar.Text) && GetRoot(owner).IsTreeRoot)
        {
            throw new InvalidOperationException(
                "An entry cannot be added after a block scalar that ends the document without a line break: the line break would become part of its value.");
        }
    }

    private static YamlEditableNode GetRoot(YamlEditableNode node)
    {
        while (node.Parent is not null)
        {
            node = node.Parent;
        }

        return node;
    }

    /// <summary>
    /// Prepares <paramref name="entry"/> to be inserted before <paramref name="next"/>.
    /// </summary>
    public static void PrepareInsert(YamlEditableNode next, YamlEditableNode entry)
    {
        var newLine = FindNewLine(GetLastLineOwner(next).EndOfLineTrivia)?.Text ?? GetDocumentNewLine(next);
        EnsureEndsWithNewLine(entry, newLine);
        UseNewLine(entry, newLine);
        KeepCommentsOutOfBlockScalars(entry);
    }

    /// <summary>
    /// Moves the line break of an entry after its value changed from <paramref name="oldValue"/>
    /// to <paramref name="entry"/>'s current value.
    /// </summary>
    public static void AfterValueChanged(YamlEditableNode entry, YamlEditableNode oldValue)
    {
        MoveHeaderComment(entry, oldValue);
        AfterLayoutChanged(entry, IsBlock(oldValue), GetLastEntry(oldValue));
        KeepCommentsOutOfBlockScalars(entry);
    }

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
        else if (collection.Parent is null && removedEntry is not null)
        {
            // The root of the document stays a mapping or a list: empty, it is written as "{}" or "[]" on the line
            // of the removed entry, at its indentation.
            if (GetIndent(removedEntry) is { Length: > 0 } indent)
            {
                (collection.LeadingTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, indent));
            }

            var lineBreak = FindNewLine(GetLastLineOwner(removedEntry).EndOfLineTrivia)?.Text;
            switch (collection)
            {
                case YamlEditableMap { EmptyFlowText: null } map:
                    map.EmptyFlowText = "{}" + lineBreak;
                    break;
                case YamlEditableList { EmptyFlowText: null } list:
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
            var separatorBreak = entry.TrailingTrivia?.FindLastIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) ?? -1;
            if (separatorBreak >= 0)
            {
                // "key: # comment\n  # more\n  {}" already ends the line of the key and keeps the comment lines
                // below it: the block replaces the indentation of the old value, and a comment after the old value
                // goes to the last line of the block.
                var separator = entry.TrailingTrivia!;
                var kept = separator.GetRange(0, separatorBreak + 1);
                var lastOwner = GetLastLineOwner(entry);
                if (HasComment(oldEndOfLine) && !HasComment(lastOwner.EndOfLineTrivia))
                {
                    lastOwner.EndOfLineTrivia = oldEndOfLine;
                }
                else if (HasComment(oldEndOfLine))
                {
                    // The last line of the block has its own comment: the comment of the old value becomes a comment
                    // line above the block, at the indentation of the old value.
                    kept.AddRange(separator.GetRange(separatorBreak + 1, separator.Count - separatorBreak - 1));
                    kept.AddRange(oldEndOfLine!.SkipWhile(t => t.Kind == YamlEditableTriviaKind.Whitespace));
                    if (kept[kept.Count - 1].Kind != YamlEditableTriviaKind.NewLine)
                    {
                        kept.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine?.Text ?? GetDocumentNewLine(entry)));
                    }
                }

                entry.TrailingTrivia = kept;
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
        else if (entry is YamlEditableListEntry && wasBlock && !isBlock && entry.TrailingTrivia is { } dashSeparator
            && dashSeparator.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) is var dashBreak and >= 0)
        {
            if (!HasComment(dashSeparator))
            {
                // "-\n  key: value" becomes "- {}": the line break of the "-" ends the line.
                entry.EndOfLineTrivia = dashSeparator.GetRange(dashBreak, 1);
                entry.TrailingTrivia = null;
            }
            else if (dashSeparator[dashSeparator.Count - 1].Kind == YamlEditableTriviaKind.NewLine)
            {
                // The comment lines after the "-" stay: "{}" goes on the next line, indented past the "-".
                dashSeparator.Add(new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, new string(' ', GetColumn(entry).Length + 2)));
            }
        }
        else if (entry is YamlEditableListEntry && !wasBlock && isBlock && entry.EndOfLineTrivia is not null)
        {
            // "- value # comment\n" becomes "- key: value # comment\n" followed by the other lines of the block: the
            // first line of the block is the line of the "-".
            var first = GetEntries(value!)[0];
            if (!IsBlock(GetValue(first)) && !HasComment(first.EndOfLineTrivia) && FindNewLine(entry.EndOfLineTrivia) is not null)
            {
                first.EndOfLineTrivia = entry.EndOfLineTrivia;
            }
            else
            {
                // The first line of the block ends deeper ("- key:" followed by a nested block) or has a comment: only
                // the comment goes there.
                MoveCommentToFirstLine(entry.EndOfLineTrivia!, first, newLine?.Text ?? GetDocumentNewLine(entry));
            }

            entry.EndOfLineTrivia = null;
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
    /// Prepares the removal of the first entry of <paramref name="collection"/>, a map or a list that is a list item, when
    /// that entry follows the "-" of the item: the next entry, <paramref name="next"/>, takes its place on the line of the
    /// "-", or, when comment lines or blank lines are above it, the "-" stays alone on its line and they stay with it.
    /// </summary>
    public static void PrepareRemoveFirst(YamlEditableNode collection, YamlEditableNode next)
    {
        if (!SharesLineWithDash(collection))
        {
            return;
        }

        if (next.LeadingTrivia is null)
        {
            SetIndent(next, string.Empty);
            return;
        }

        var dashEntry = collection.Parent!;
        var separator = dashEntry.TrailingTrivia;
        var lineBreak = separator?.FindLastIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) ?? -1;
        dashEntry.TrailingTrivia = lineBreak >= 0
            ? separator!.GetRange(0, lineBreak + 1)
            : [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, GetDocumentNewLine(collection))];
    }

    /// <summary>
    /// Prepares <paramref name="entry"/> to be inserted at the start of <paramref name="collection"/>, before
    /// <paramref name="next"/>: if <paramref name="next"/> follows the "-" of a list item ("- key: value",
    /// "- - item"), <paramref name="entry"/> takes its place there and <paramref name="next"/> goes to the column it had.
    /// </summary>
    public static bool PrepareInsertFirst(YamlEditableNode collection, YamlEditableNode next, YamlEditableNode entry)
    {
        if (!SharesLineWithDash(collection))
        {
            return false;
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

        return true;
    }

    /// <summary>
    /// Gives <paramref name="entry"/>, inserted before <paramref name="next"/> without indentation of its own, the column
    /// of <paramref name="next"/>: the entries of a map or a list share their column.
    /// </summary>
    public static void AlignWith(YamlEditableNode entry, YamlEditableNode next)
    {
        if (GetIndent(entry).Length == 0)
        {
            SetIndent(entry, GetColumn(next));
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

        if (entry is YamlEditableListEntry && FindNewLine(entry.TrailingTrivia) is null)
        {
            // The first entry of a list item is written right after the "-" and its trivia, which give its column:
            // indentation of its own would move it past the column of the other entries.
            var first = GetEntries(value!)[0];
            var firstIndent = GetIndent(first).Length;
            SetIndent(first, string.Empty);
            if (GetOwnLineEntries(value!).Count == 0)
            {
                // A single entry: its lines (of a multi-line scalar, of a nested block) move from the column it had to
                // the column after the "-". Indenting more the lines of a supported scalar never changes its value.
                var delta = GetColumn(first).Length - firstIndent;
                if (delta > 0)
                {
                    Shift(value!, delta);
                }
            }
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

            // A list item aligns its entries with its first one; elsewhere a valid layout is not changed.
            if (current < target || (entry is YamlEditableListEntry && current > target))
            {
                Shift(value!, target - current);
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
    /// Moves every line of <paramref name="value"/> that starts with an entry by <paramref name="delta"/> columns, at
    /// every depth, with the lines of its multi-line scalars.
    /// </summary>
    private static void Shift(YamlEditableNode value, int delta)
    {
        foreach (var entry in GetOwnLineEntries(value))
        {
            SetIndent(entry, Reindent(GetIndent(entry), delta));
        }

        foreach (var entry in GetEntries(value))
        {
            switch (GetValue(entry))
            {
                case YamlEditableScalar scalar:
                    // The lines of a block or multi-line scalar move with its entry, so that their indentation
                    // relative to it, and so the value, stay the same.
                    scalar.SetTextUnchecked(ReindentLines(scalar.Text, delta));
                    break;
                case { } nested when IsBlock(nested):
                    Shift(nested, delta);
                    break;
            }
        }
    }

    /// <summary>
    /// Adds <paramref name="delta"/> spaces before <paramref name="text"/>, or removes as many spaces from its start.
    /// </summary>
    private static string Reindent(string text, int delta)
    {
        if (delta >= 0)
        {
            return new string(' ', delta) + text;
        }

        var remove = 0;
        while (remove < -delta && remove < text.Length && text[remove] == ' ')
        {
            remove++;
        }

        return text.Substring(remove);
    }

    /// <summary>
    /// Reindents every line of <paramref name="text"/> after the first one by <paramref name="delta"/>, except the
    /// empty ones.
    /// </summary>
    private static string ReindentLines(string text, int delta)
    {
        if (text.IndexOfAny(['\r', '\n']) < 0)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length + 16);
        var lineStart = 0;
        for (var index = 0; index <= text.Length; index++)
        {
            if (index < text.Length && text[index] is not ('\r' or '\n'))
            {
                continue;
            }

            var line = text.Substring(lineStart, index - lineStart);
            builder.Append(lineStart == 0 || line.Length == 0 ? line : Reindent(line, delta));
            if (index < text.Length)
            {
                var length = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
                builder.Append(text, index, length);
                index += length - 1;
                lineStart = index + 1;
            }
        }

        return builder.ToString();
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
    /// of its last line ("|", ">"), unlike the strip indicator ("|-").
    /// </summary>
    private static bool KeepsFinalLineBreak(string text) => IsBlockScalar(text) && GetHeaderIndicators(text).IndexOf('-') < 0;

    /// <summary>
    /// Returns <see langword="true"/> if the header of the block scalar <paramref name="text"/> has the keep indicator
    /// ("|+") or an indentation indicator ("|2"). The value of such a scalar depends on the lines after it or on the
    /// column of its parent, so the editable tree does not support it.
    /// </summary>
    internal static bool HasKeepOrIndentationIndicator(string text)
    {
        if (!IsBlockScalar(text))
        {
            return false;
        }

        foreach (var c in GetHeaderIndicators(text))
        {
            if (c == '+' || c is >= '0' and <= '9')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBlockScalar(string text) => GetIndicatorIndex(text) >= 0;

    /// <summary>
    /// Returns the index of the "|" or ">" that starts the block scalar <paramref name="text"/>, after spaces and line
    /// breaks, or -1.
    /// </summary>
    private static int GetIndicatorIndex(string text)
    {
        var index = 0;
        while (index < text.Length && text[index] is ' ' or '\t' or '\r' or '\n')
        {
            index++;
        }

        return index < text.Length && text[index] is '|' or '>' ? index : -1;
    }

    internal static bool IsBlockScalarText(string text) => IsBlockScalar(text);

    /// <summary>
    /// Returns the indicators of the header of a block scalar: the characters after "|" or ">" up to a space, a
    /// comment or the end of the line.
    /// </summary>
    private static string GetHeaderIndicators(string text)
    {
        var start = GetIndicatorIndex(text) + 1;
        var end = start;
        while (end < text.Length && text[end] is not (' ' or '\t' or '\r' or '\n' or '#'))
        {
            end++;
        }

        return text.Substring(start, end - start);
    }

    /// <summary>
    /// Throws if <paramref name="node"/>, a value or an entry to attach, holds a block scalar that the editable tree does
    /// not support (see <see cref="HasKeepOrIndentationIndicator"/>). Called before anything is changed.
    /// </summary>
    public static void ThrowIfUnsupportedValue(YamlEditableNode node)
    {
        if (node is YamlEditableScalar scalar)
        {
            ThrowIfUnsupportedScalarText(scalar.Text);
            return;
        }

        if (GetValue(node) is { } value)
        {
            ThrowIfUnsupportedValue(value);
        }

        foreach (var child in GetEntries(node))
        {
            ThrowIfUnsupportedValue(child);
        }
    }

    /// <summary>
    /// Throws if <paramref name="text"/> is a block scalar that the editable tree does not support.
    /// </summary>
    public static void ThrowIfUnsupportedScalarText(string text)
    {
        if (HasKeepOrIndentationIndicator(text))
        {
            throw new InvalidOperationException(
                "The editable tree does not support block scalars with the keep indicator ('|+') or an indentation indicator ('|2').");
        }
    }

    /// <summary>
    /// Moves a comment that would be written right after the last line of a block scalar, where it would be part of
    /// the scalar's value, to the header line of the scalar ("| # comment"), for <paramref name="entry"/> and the entries
    /// below it.
    /// </summary>
    public static void KeepCommentsOutOfBlockScalars(YamlEditableNode entry)
    {
        var owner = GetLastLineOwner(entry);
        if (GetValue(owner) is YamlEditableScalar scalar && IsBlockScalar(scalar.Text))
        {
            MoveCommentToHeader(owner.EndOfLineTrivia, scalar);
            if (!ReferenceEquals(owner, entry) && FindNewLine(owner.EndOfLineTrivia) is null)
            {
                MoveCommentToHeader(entry.EndOfLineTrivia, scalar);
            }
        }

        if (GetValue(entry) is { } value)
        {
            foreach (var child in GetEntries(value))
            {
                KeepCommentsOutOfBlockScalars(child);
            }
        }
    }

    /// <summary>
    /// Moves the comment of <paramref name="trivia"/>, with the spaces before it, to the end of the first line of
    /// <paramref name="entry"/>: after the value of its first entry, or after the "key:" of a nested block.
    /// </summary>
    private static void MoveCommentToFirstLine(List<YamlEditableTrivia> trivia, YamlEditableNode entry, string newLine)
    {
        var commentIndex = trivia.FindIndex(t => t.Kind == YamlEditableTriviaKind.Comment);
        if (commentIndex < 0)
        {
            return;
        }

        var start = commentIndex;
        while (start > 0 && trivia[start - 1].Kind == YamlEditableTriviaKind.Whitespace)
        {
            start--;
        }

        var comment = trivia.GetRange(start, commentIndex - start + 1);
        if (start == commentIndex)
        {
            comment.Insert(0, new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, " "));
        }

        while (entry is YamlEditableListEntry { Value: var nested } && IsBlock(nested))
        {
            entry = GetEntries(nested)[0];
        }

        if (entry is YamlEditableMapEntry { Value: var block } mapEntry && IsBlock(block))
        {
            var separator = mapEntry.TrailingTrivia ??= [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine)];
            var lineBreak = separator.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
            separator.InsertRange(lineBreak < 0 ? separator.Count : lineBreak, comment);
            return;
        }

        var endOfLine = entry.EndOfLineTrivia ??= [];
        var end = endOfLine.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
        endOfLine.InsertRange(end < 0 ? endOfLine.Count : end, comment);
    }

    internal static void MoveCommentToHeader(List<YamlEditableTrivia>? trivia, YamlEditableScalar scalar)
    {
        var commentIndex = trivia?.FindIndex(t => t.Kind == YamlEditableTriviaKind.Comment) ?? -1;
        if (commentIndex < 0)
        {
            return;
        }

        var start = commentIndex;
        while (start > 0 && trivia![start - 1].Kind == YamlEditableTriviaKind.Whitespace)
        {
            start--;
        }

        var comment = string.Concat(trivia!.GetRange(start, commentIndex - start + 1).Select(t => t.Text));
        trivia.RemoveRange(start, commentIndex - start + 1);
        var headerEnd = scalar.Text.IndexOfAny(['\r', '\n'], Math.Max(0, GetIndicatorIndex(scalar.Text)));
        scalar.SetTextUnchecked(scalar.Text.Insert(headerEnd < 0 ? scalar.Text.Length : headerEnd, start == commentIndex ? " " + comment : comment));
    }

    /// <summary>
    /// Moves the comment of the header line of <paramref name="oldValue"/>, a block scalar replaced as the value of
    /// <paramref name="entry"/>, to the end of the line of the entry ("key: | # comment" becomes "key: new # comment").
    /// </summary>
    private static void MoveHeaderComment(YamlEditableNode entry, YamlEditableNode oldValue)
    {
        if (oldValue is YamlEditableScalar scalar && GetHeaderComment(scalar.Text) is { } comment
            && !(GetValue(entry) is YamlEditableScalar newScalar && HasSameHeaderComment(comment, newScalar.Text)))
        {
            (entry.EndOfLineTrivia ??= []).InsertRange(0, comment);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the block scalar <paramref name="text"/> has <paramref name="comment"/> as the comment
    /// of its header line.
    /// </summary>
    internal static bool HasSameHeaderComment(List<YamlEditableTrivia> comment, string text) =>
        GetHeaderComment(text) is { } other
        && string.Equals(comment[comment.Count - 1].Text, other[other.Count - 1].Text, StringComparison.Ordinal);

    /// <summary>
    /// Returns the comment of the header line of the block scalar <paramref name="text"/>, with the spaces before it, as
    /// trivia, or <see langword="null"/>.
    /// </summary>
    internal static List<YamlEditableTrivia>? GetHeaderComment(string text)
    {
        var indicator = GetIndicatorIndex(text);
        if (indicator < 0)
        {
            return null;
        }

        var headerEnd = text.IndexOfAny(['\r', '\n'], indicator);
        headerEnd = headerEnd < 0 ? text.Length : headerEnd;
        var hash = text.IndexOf('#', indicator + 1, headerEnd - indicator - 1);
        if (hash < 0)
        {
            return null;
        }

        var start = hash;
        while (start > indicator + 1 && text[start - 1] is ' ' or '\t')
        {
            start--;
        }

        var comment = new List<YamlEditableTrivia>();
        if (start < hash)
        {
            comment.Add(new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, text.Substring(start, hash - start)));
        }

        comment.Add(new YamlEditableTrivia(YamlEditableTriviaKind.Comment, text.Substring(hash, headerEnd - hash)));
        return comment;
    }

    private static bool HasComment(List<YamlEditableTrivia>? trivia) =>
        trivia is not null && trivia.Exists(t => t.Kind == YamlEditableTriviaKind.Comment);

    /// <summary>
    /// Returns the line break of the document of <paramref name="node"/>: the first one of the text it was read from, or
    /// of its tree, or "\n" if it has none.
    /// </summary>
    private static string GetDocumentNewLine(YamlEditableNode node)
    {
        var root = GetRoot(node);
        return root.DocumentNewLine ?? FindNewLineInTree(root)?.Text ?? DefaultNewLine;
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
