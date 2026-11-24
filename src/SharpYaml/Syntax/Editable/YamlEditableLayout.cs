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
/// Each line ends in the <see cref="YamlEditableEntry.EndOfLineTrivia"/> of the entry whose scalar
/// value is on that line, or in the <see cref="YamlEditableEntry.SeparatorTrivia"/> of an entry
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
        node is YamlEditableMapping { Entries.Count: > 0 } or YamlEditableSequence { Items.Count: > 0 };

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="node"/> is a missing value ("key:" or "-" with
    /// nothing after it), which is written without the space after the ":" or the "-".
    /// </summary>
    public static bool IsMissingValue(YamlEditableNode? node) =>
        node is YamlEditableScalar { Text.Length: 0 };

    /// <summary>
    /// Inserts <paramref name="entry"/>, checked by the caller, at <paramref name="index"/> of <paramref name="collection"/>,
    /// a map or a list. The entry goes on lines of its own, at the column of the other entries, and the lines of its value
    /// move with it; an empty map or list that is a value becomes a block.
    /// </summary>
    public static void Insert(YamlEditableNode collection, int index, YamlEditableEntry entry)
    {
        var entries = GetEntries(collection);
        var oldColumn = entry.DetachedColumn ?? GetIndent(entry).Length;
        if (entry.DetachedRoot is not null && !ReferenceEquals(entry.DetachedRoot, GetRoot(collection)))
        {
            // The line break that the entry had in another document does not apply in this one.
            GetLastLineOwner(entry).RemovedLineBreak = null;
        }

        var rootBefore = entries.Count == 0 && collection.Parent is null ? CaptureRoot(collection) : null;
        string? column = null;
        if (index == entries.Count)
        {
            var previous = entries.Count > 0 ? entries[entries.Count - 1] : null;
            if (previous is not null)
            {
                column = GetColumn(previous);
            }

            PrepareAppend(collection, previous, entry);
        }
        else
        {
            var next = entries[index];
            if (index != 0 || !PrepareInsertFirst(collection, next, entry))
            {
                column = GetColumn(next);
            }

            PrepareInsert(next, entry);
        }

        // The entries of a map or a list share their column: an entry added next to others takes it.
        if (column is not null)
        {
            SetIndent(entry, column);
        }

        switch (collection)
        {
            case YamlEditableMapping map:
                map.InsertParsedEntry(index, (YamlEditableMappingEntry)entry);
                break;
            case YamlEditableSequence list:
                list.InsertParsedEntry(index, (YamlEditableSequenceEntry)entry);
                break;
        }

        // The lines of the value (of a nested block, of a scalar on several lines) keep their indentation relative to
        // the entry.
        entry.DetachedColumn = null;
        var delta = GetColumn(entry).Length - oldColumn;
        if (delta < 0)
        {
            // Every line moves by the same number of columns, so that the lines keep their indentation relative to each
            // other: a value whose lines are not indented as much as the column the entry had is moved less.
            delta = Math.Max(delta, -GetSmallestIndent(entry));
        }

        if (delta != 0)
        {
            ShiftValue(entry, delta);
        }

        IndentValue(entry);
        if (entries.Count == 0)
        {
            AfterEmptinessChanged(collection, removedEntry: null);
        }

        // A document read without a final line break keeps ending without one.
        if (index == entries.Count && IsAtEndOfDocument(collection) && GetRoot(collection).Tree is { EndsWithoutLineBreak: true })
        {
            RemoveFinalLineBreak(entry);
        }

        entry.DetachedRoot = null;

        if (rootBefore is not null)
        {
            // Removing the entries again gives back the text of the empty root.
            rootBefore.Produced = Fingerprint(collection.LeadingTrivia, collection.Tree?.TrailingTrivia);
            collection.LayoutMemo = rootBefore;
        }
    }

    /// <summary>
    /// Removes the entry at <paramref name="index"/> of <paramref name="collection"/>, a map or a list, with its comment lines
    /// and its lines. A map or a list that is a value and loses its last entry is written as "{}" or "[]" on the line of its
    /// key or "-"; a document whose last line had no line break still has none.
    /// </summary>
    public static void Remove(YamlEditableNode collection, int index)
    {
        var entries = GetEntries(collection);
        var entry = entries[index];
        var endsDocument = index == entries.Count - 1 && IsAtEndOfDocument(collection)
            && (FindNewLine(GetLastLineOwner(entry).EndOfLineTrivia) is null || GetRoot(collection).Tree is { EndsWithoutLineBreak: true });

        // An entry added again elsewhere moves the lines of its value from this column, and keeps its line breaks in the
        // same document.
        entry.DetachedColumn = GetColumn(entry).Length;
        entry.DetachedRoot = GetRoot(collection);
        if (index == 0 && entries.Count > 1)
        {
            PrepareRemoveFirst(collection, entries[1]);
        }

        switch (collection)
        {
            case YamlEditableMapping map:
                map.RemoveParsedEntryAt(index);
                break;
            case YamlEditableSequence list:
                list.RemoveParsedEntryAt(index);
                break;
        }

        if (entries.Count == 1)
        {
            AfterEmptinessChanged(collection, entry);
        }

        if (endsDocument)
        {
            EndDocumentWithoutLineBreak(collection);

            // The layout to restore when an entry is added again is the one without the line break.
            if (entries.Count == 1 && collection.Parent is YamlEditableEntry owner && owner.LayoutMemo is { } memo)
            {
                memo.Produced = Fingerprint(owner.SeparatorTrivia, owner.EndOfLineTrivia);
            }
        }
    }

    /// <summary>
    /// Removes the line break that ends the last line of <paramref name="entry"/>, which becomes the last line of a document
    /// without a final line break, unless it is the last line of a block scalar that keeps its final line break ("|", ">"),
    /// whose value it is part of.
    /// </summary>
    private static void RemoveFinalLineBreak(YamlEditableEntry entry)
    {
        var owner = GetLastLineOwner(entry);
        if (owner.EndOfLineTrivia is { Count: > 0 } endOfLine && endOfLine[endOfLine.Count - 1].Kind == YamlEditableTriviaKind.NewLine
            && !(owner.Value is YamlEditableScalar scalar && KeepsFinalLineBreak(scalar.Text)))
        {
            owner.RemovedLineBreak = endOfLine[endOfLine.Count - 1].Text;
            endOfLine.RemoveAt(endOfLine.Count - 1);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the last entry of <paramref name="collection"/> is on the last line of its document:
    /// the collection is the value of the last entry of each map or list around it, and only spaces follow the root.
    /// </summary>
    private static bool IsAtEndOfDocument(YamlEditableNode collection)
    {
        var node = collection;
        while (node.Parent is { } owner)
        {
            if (owner.Parent is not { } outer || !ReferenceEquals(GetLastEntry(outer), owner))
            {
                return false;
            }

            node = outer;
        }

        return node.Tree is { } tree && (tree.TrailingTrivia is null || tree.TrailingTrivia.TrueForAll(t => t.Kind == YamlEditableTriviaKind.Whitespace));
    }

    /// <summary>
    /// Removes the line break of the last line of the document of <paramref name="collection"/>, whose last line, just removed,
    /// had none: the document keeps ending without a line break. The line break of a block scalar that keeps its final line
    /// break ("|", ">") stays, as its value.
    /// </summary>
    private static void EndDocumentWithoutLineBreak(YamlEditableNode collection)
    {
        var root = GetRoot(collection);
        if (root.Tree is not { } tree || FindNewLine(tree.TrailingTrivia) is not null || GetLastEntry(root) is not { } last)
        {
            return;
        }

        var owner = GetLastLineOwner(last);
        if (owner.EndOfLineTrivia is not { Count: > 0 } endOfLine
            || endOfLine[endOfLine.Count - 1].Kind != YamlEditableTriviaKind.NewLine
            || GetValue(owner) is YamlEditableScalar scalar && KeepsFinalLineBreak(scalar.Text))
        {
            return;
        }

        owner.RemovedLineBreak = endOfLine[endOfLine.Count - 1].Text;
        endOfLine.RemoveAt(endOfLine.Count - 1);
    }

    /// <summary>
    /// Prepares <paramref name="entry"/> to be appended to <paramref name="collection"/>, after
    /// <paramref name="previous"/>, its last entry if it has one.
    /// </summary>
    public static void PrepareAppend(YamlEditableNode collection, YamlEditableEntry? previous, YamlEditableEntry entry)
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

                UseNewLineOf(collection, entry, lineBreak);
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

            if (collection.Parent is null && GetEmptyFlowText(collection) is { } flowText && GetEndLineBreak(flowText) is { } flowLineBreak)
            {
                // A root whose entries were all removed was written "{}" with the line break of its last line.
                EnsureEndsWithNewLine(entry, flowLineBreak);
            }

            if (GetEmptyFlowText(collection) is not null)
            {
                UseNewLineOf(collection, entry, GetDocumentNewLine(collection));

                // The comment on the line of a root "{} # comment" stays with the root: a comment line above its entries.
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

                    var comment = rootTrailing[rootComment];
                    rootTrailing.RemoveRange(start, rootComment - start + 1);
                    var rootLeading = collection.LeadingTrivia ??= [];
                    rootLeading.Add(comment);
                    rootLeading.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, GetDocumentNewLine(collection)));
                }

                // The spaces after "{}" or "[]" are left out, and its line break ends the line of the entry, which keeps its
                // own line break if it has one. The root gets them back when it is empty again.
                if (collection.Tree?.TrailingTrivia is { } flowTrailing)
                {
                    var spaces = 0;
                    while (spaces < flowTrailing.Count && flowTrailing[spaces].Kind == YamlEditableTriviaKind.Whitespace)
                    {
                        spaces++;
                    }

                    var flowEnd = spaces < flowTrailing.Count && flowTrailing[spaces].Kind == YamlEditableTriviaKind.NewLine ? flowTrailing[spaces] : null;
                    flowTrailing.RemoveRange(0, flowEnd is null ? spaces : spaces + 1);
                    if (flowEnd is not null)
                    {
                        EnsureEndsWithNewLine(entry, flowEnd);
                    }
                }
            }

            // The comment lines at the start of the document stay its own, not the first entry's: a blank line separates
            // them from it.
            if (collection.Parent is null && collection.LeadingTrivia is { } header && HasComment(header))
            {
                EndWithBlankLine(header, GetDocumentNewLine(collection));
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
            // The line ends with the line break it had before it became the last line, if it had one.
            var documentNewLine = owner.RemovedLineBreak ?? GetDocumentNewLine(previous);
            owner.RemovedLineBreak = null;
            (owner.EndOfLineTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, documentNewLine));
            UseNewLineOf(collection, entry, documentNewLine);
            RemoveFinalLineBreak(entry);
        }
        else
        {
            // A document read without a final line break keeps ending without one.
            if (IsAtEndOfDocument(collection) && GetRoot(collection).Tree is { EndsWithoutLineBreak: true })
            {
                RemoveFinalLineBreak(entry);
            }
            else
            {
                EnsureEndsWithNewLine(entry, newLine);
            }

            UseNewLineOf(collection, entry, newLine.Text);
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
    public static void PrepareInsert(YamlEditableEntry next, YamlEditableEntry entry)
    {
        var newLine = FindNewLine(GetLastLineOwner(next).EndOfLineTrivia)?.Text ?? GetDocumentNewLine(next);
        EnsureEndsWithNewLine(entry, newLine);
        UseNewLineOf(next, entry, newLine);
        KeepCommentsOutOfBlockScalars(entry);
    }

    /// <summary>
    /// Moves the line break of an entry after its value changed from <paramref name="oldValue"/>
    /// to <paramref name="entry"/>'s current value.
    /// </summary>
    public static void AfterValueChanged(YamlEditableEntry entry, YamlEditableNode oldValue)
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
    public static void AfterEmptinessChanged(YamlEditableNode collection, YamlEditableEntry? removedEntry)
    {
        if (collection.Parent is YamlEditableEntry owner)
        {
            AfterLayoutChanged(owner, wasBlock: removedEntry is not null, removedEntry);
            IndentValue(owner);
        }
        else if (collection.Parent is null && removedEntry is not null && collection.LayoutMemo is { } memo
            && memo.Matches(collection.LeadingTrivia, collection.Tree?.TrailingTrivia))
        {
            // The root is empty again, as it was before its entries were added.
            collection.LeadingTrivia = Clone(memo.First);
            if (collection.Tree is { } tree)
            {
                tree.TrailingTrivia = Clone(memo.Second);
            }

            switch (collection)
            {
                case YamlEditableMapping map:
                    map.EmptyFlowText = memo.Text;
                    break;
                case YamlEditableSequence list:
                    list.EmptyFlowText = memo.Text;
                    break;
            }

            collection.LayoutMemo = null;
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
                case YamlEditableMapping { EmptyFlowText: null } map:
                    map.EmptyFlowText = "{}" + lineBreak;
                    break;
                case YamlEditableSequence { EmptyFlowText: null } list:
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
        YamlEditableMapping map => map.EmptyFlowText,
        YamlEditableSequence list => list.EmptyFlowText,
        _ => null,
    };

    /// <summary>
    /// Moves the line break of <paramref name="entry"/> after its value changed from a block, whose last entry was
    /// <paramref name="oldLastEntry"/>, or from a value on its line, to its current value. A comment on the line of the entry
    /// stays on that line, the line of its key or its "-": a comment line above a block would belong to its first entry once
    /// the text is read again. A value that is written on the line of the entry again, after it was a block, gets back the
    /// trivia it had there.
    /// </summary>
    private static void AfterLayoutChanged(YamlEditableEntry entry, bool wasBlock, YamlEditableEntry? oldLastEntry)
    {
        var value = GetValue(entry);
        var isBlock = IsBlock(value);
        var oldOwner = oldLastEntry is null ? entry : GetLastLineOwner(oldLastEntry);
        var newLine = FindNewLine(oldOwner.EndOfLineTrivia);
        var before = wasBlock != isBlock ? new Memo(Clone(entry.SeparatorTrivia), Clone(entry.EndOfLineTrivia), null) { RestoresBlock = wasBlock } : null;

        if (wasBlock != isBlock && entry.LayoutMemo is { } memo && memo.RestoresBlock == isBlock
            && memo.Matches(entry.SeparatorTrivia, entry.EndOfLineTrivia))
        {
            // The value is written as it was before the last change of layout of the entry: its trivia come back.
            entry.SeparatorTrivia = Clone(memo.First);
            entry.EndOfLineTrivia = Clone(memo.Second);
        }
        else if (entry is YamlEditableMappingEntry && !wasBlock && isBlock)
        {
            var oldEndOfLine = entry.EndOfLineTrivia;
            entry.EndOfLineTrivia = null;
            if (FindNewLine(entry.SeparatorTrivia) is not null)
            {
                // "key: # comment\n  # more\n  {} # last": the block starts after the line of the key.
                entry.SeparatorTrivia = KeepFirstLine(entry.SeparatorTrivia!, oldEndOfLine);
            }
            else
            {
                // "key: value # comment\n" becomes "key: # comment\n" followed by the nested block.
                var separator = oldEndOfLine ?? [];
                if (FindNewLine(separator) is null)
                {
                    separator.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine?.Text ?? GetDocumentNewLine(entry)));
                }

                entry.SeparatorTrivia = separator;
            }
        }
        else if (entry is YamlEditableMappingEntry && wasBlock && !isBlock)
        {
            // "key: # comment\n" followed by a nested block becomes "key: value # comment\n".
            entry.EndOfLineTrivia = entry.SeparatorTrivia is { } separator && FindNewLine(separator) is not null ? separator : null;
            entry.SeparatorTrivia = null;
        }
        else if (entry is YamlEditableSequenceEntry && wasBlock && !isBlock && entry.SeparatorTrivia is { } dashSeparator
            && FindNewLine(dashSeparator) is not null)
        {
            if (dashSeparator.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) == dashSeparator.Count - 1)
            {
                // "- # comment\n" followed by a block becomes "- value # comment\n".
                entry.EndOfLineTrivia = dashSeparator;
                entry.SeparatorTrivia = null;
            }
            else if (dashSeparator[dashSeparator.Count - 1].Kind == YamlEditableTriviaKind.NewLine)
            {
                // The comment lines and blank lines after the "-" stay: the value goes on the next line, indented past the
                // "-".
                dashSeparator.Add(new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, new string(' ', GetColumn(entry).Length + 2)));
            }
        }
        else if (entry is YamlEditableSequenceEntry && !wasBlock && isBlock)
        {
            var first = GetEntries(value!)[0];
            if (FindNewLine(entry.SeparatorTrivia) is not null || HasComment(entry.EndOfLineTrivia) || first.LeadingTrivia is { Count: > 0 })
            {
                // "- {} # comment", "- value # comment" or "-\n  # comment\n  {}": the block starts on the line after the
                // "-", which keeps the comments of the item; so it does when its first entry has comment lines above it.
                entry.SeparatorTrivia = FindNewLine(entry.SeparatorTrivia) is not null
                    ? KeepFirstLine(entry.SeparatorTrivia!, entry.EndOfLineTrivia)
                    : KeepFirstLine([new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine?.Text ?? GetDocumentNewLine(entry))], entry.EndOfLineTrivia);
                entry.EndOfLineTrivia = null;
            }
            else if (entry.EndOfLineTrivia is not null)
            {
                // "- value\n" becomes "- key: value\n" followed by the other lines of the block: the line break of the
                // "-" ends the first line of the block.
                if (!IsBlock(GetValue(first)) && FindNewLine(first.EndOfLineTrivia) is null && FindNewLine(entry.EndOfLineTrivia) is not null)
                {
                    first.EndOfLineTrivia = [.. first.EndOfLineTrivia ?? [], .. entry.EndOfLineTrivia];
                }

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

        if (before is not null)
        {
            before.Produced = Fingerprint(entry.SeparatorTrivia, entry.EndOfLineTrivia);
            entry.LayoutMemo = before;
        }
    }

    /// <summary>
    /// Returns the first line of <paramref name="separator"/>, the trivia after "key:" or "-" up to and including its first
    /// line break, with the comments of its other lines and of <paramref name="extra"/> added to that line, so that they stay
    /// with the entry when a block starts after it.
    /// </summary>
    private static List<YamlEditableTrivia> KeepFirstLine(List<YamlEditableTrivia> separator, List<YamlEditableTrivia>? extra)
    {
        var lineBreak = separator.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
        var kept = separator.GetRange(0, lineBreak + 1);

        // A comment line becomes part of the first line; a blank line stays, without its spaces.
        var lineStart = lineBreak + 1;
        for (var index = lineBreak + 1; index < separator.Count; index++)
        {
            if (separator[index].Kind != YamlEditableTriviaKind.NewLine)
            {
                continue;
            }

            var comments = separator.GetRange(lineStart, index - lineStart).FindAll(t => t.Kind == YamlEditableTriviaKind.Comment);
            if (comments.Count == 0)
            {
                kept.Add(separator[index]);
            }

            foreach (var comment in comments)
            {
                AddCommentToLine(kept, comment);
            }

            lineStart = index + 1;
        }

        for (var index = lineStart; index < separator.Count; index++)
        {
            if (separator[index].Kind == YamlEditableTriviaKind.Comment)
            {
                AddCommentToLine(kept, separator[index]);
            }
        }

        foreach (var trivia in extra ?? [])
        {
            if (trivia.Kind == YamlEditableTriviaKind.Comment)
            {
                AddCommentToLine(kept, trivia);
            }
        }

        return kept;
    }

    /// <summary>
    /// Writes the line breaks of <paramref name="entry"/>, added to <paramref name="target"/> or next to it, as
    /// <paramref name="newLine"/>, unless the entry was removed from the same document, whose line breaks it keeps.
    /// </summary>
    private static void UseNewLineOf(YamlEditableNode target, YamlEditableEntry entry, string newLine)
    {
        if (entry.DetachedRoot is null || !ReferenceEquals(entry.DetachedRoot, GetRoot(target)))
        {
            UseNewLine(entry, newLine);
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
        if (node is YamlEditableEntry entry)
        {
            UseNewLine(entry.SeparatorTrivia, newLine);
            UseNewLine(entry.EndOfLineTrivia, newLine);
        }

        if (node is YamlEditableMappingEntry { SeparatorTrivia: null } mapEntry && IsBlock(mapEntry.Value))
        {
            mapEntry.SeparatorTrivia = [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine)];
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
    /// Returns the indentation of <paramref name="entry"/>, a map or a list entry, as a column, also when it shares its
    /// line with the "-" of a list entry ("- key: value", "- - item") and so has no indentation text of its own.
    /// </summary>
    private static string GetColumn(YamlEditableEntry entry)
    {
        var indent = GetIndent(entry);
        if (indent.Length > 0 || entry.Parent?.Parent is not YamlEditableSequenceEntry listEntry)
        {
            return indent;
        }

        var width = GetColumn(listEntry).Length + 1;
        if (listEntry.SeparatorTrivia is null)
        {
            width++;
        }
        else
        {
            // The key follows the "-" on its line, or starts the line after a comment ("- # comment\n  key: value").
            foreach (var trivia in listEntry.SeparatorTrivia)
            {
                width = trivia.Kind == YamlEditableTriviaKind.NewLine ? 0 : width + trivia.Text.Length;
            }
        }

        return new string(' ', width);
    }

    /// <summary>
    /// Prepares the removal of the first entry of <paramref name="collection"/>, a map or a list that is a list item, when
    /// that entry follows the "-" of the item: the next entry, <paramref name="next"/>, takes its place on the line of the
    /// "-", or, when comment lines or blank lines are above it, the "-" stays alone on its line and they stay with it.
    /// </summary>
    public static void PrepareRemoveFirst(YamlEditableNode collection, YamlEditableEntry next)
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

        var dashEntry = (YamlEditableEntry)collection.Parent!;
        var separator = dashEntry.SeparatorTrivia;
        var lineBreak = separator?.FindLastIndex(t => t.Kind == YamlEditableTriviaKind.NewLine) ?? -1;
        dashEntry.SeparatorTrivia = lineBreak >= 0
            ? separator!.GetRange(0, lineBreak + 1)
            : [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, GetDocumentNewLine(collection))];

        // An entry inserted first again takes the line of the "-" back.
        dashEntry.DashMemo = new Memo(Clone(separator), null, null) { Produced = Fingerprint(dashEntry.SeparatorTrivia, null) };
    }

    /// <summary>
    /// Prepares <paramref name="entry"/> to be inserted at the start of <paramref name="collection"/>, before
    /// <paramref name="next"/>: if <paramref name="next"/> follows the "-" of a list item ("- key: value",
    /// "- - item"), <paramref name="entry"/> takes its place there and <paramref name="next"/> goes to the column it had.
    /// </summary>
    public static bool PrepareInsertFirst(YamlEditableNode collection, YamlEditableEntry next, YamlEditableEntry entry)
    {
        if (SharesLineWithDash(collection) && entry.LeadingTrivia is { Count: > 0 })
        {
            // The comment lines above the entry cannot be between the "-" and its first line: the "-" goes alone on its
            // line, and the entries below it, at their column.
            var dashColumn = GetColumn(next);
            ((YamlEditableEntry)collection.Parent!).SeparatorTrivia = [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, GetDocumentNewLine(collection))];
            SetIndent(next, dashColumn);
            SetIndent(entry, dashColumn);
            return true;
        }

        if (!SharesLineWithDash(collection))
        {
            if (collection.Parent is not YamlEditableSequenceEntry dashEntry || dashEntry.DashMemo is not { } memo
                || !memo.Matches(dashEntry.SeparatorTrivia, null) || entry.LeadingTrivia is { Count: > 0 })
            {
                return false;
            }

            // The first entry was removed and the "-" was left alone on its line: the new first entry takes it back.
            dashEntry.SeparatorTrivia = Clone(memo.First);
            dashEntry.DashMemo = null;
            SetIndent(entry, string.Empty);
            return true;
        }

        var column = GetColumn(next);
        switch (next)
        {
            case YamlEditableMappingEntry mapEntry:
                mapEntry.Indent = column;
                ((YamlEditableMappingEntry)entry).Indent = string.Empty;
                break;
            case YamlEditableSequenceEntry listEntry:
                listEntry.Indent = column;
                ((YamlEditableSequenceEntry)entry).Indent = string.Empty;
                break;
        }

        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the first entry of <paramref name="collection"/>, a map or a list that is a
    /// list item, follows the "-" of that item, without indentation of its own.
    /// </summary>
    private static bool SharesLineWithDash(YamlEditableNode collection) =>
        collection.Parent is YamlEditableSequenceEntry dashEntry && FindNewLine(dashEntry.SeparatorTrivia) is null
        && GetEntries(collection) is { Count: > 0 } entries && GetIndent(entries[0]).Length == 0;

    /// <summary>
    /// Indents the nested map or list of <paramref name="entry"/> if its entries are not at a column
    /// that belongs to <paramref name="entry"/>, then does the same for each nested entry. A map or a
    /// list built in memory does not know its depth: without this, "c:" followed by a new map with
    /// "x: 1" at column 0 would make "x" a key of the outer map. A valid layout is never changed.
    /// </summary>
    public static void IndentValue(YamlEditableEntry entry)
    {
        var value = GetValue(entry);
        if (value is YamlEditableScalar scalar)
        {
            IndentScalar(entry, scalar);
            return;
        }

        if (!IsBlock(value))
        {
            return;
        }

        if (entry is YamlEditableSequenceEntry && FindNewLine(entry.SeparatorTrivia) is null && GetEntries(value!)[0].LeadingTrivia is { Count: > 0 })
        {
            // The comment lines above the first entry cannot be between the "-" and its first line: the block starts on the
            // line after the "-".
            entry.SeparatorTrivia = KeepFirstLine([new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, GetDocumentNewLine(entry))], entry.EndOfLineTrivia);
            entry.EndOfLineTrivia = null;
        }

        if (entry is YamlEditableSequenceEntry && FindNewLine(entry.SeparatorTrivia) is null)
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
            var onDashLine = SharesLineWithDash(value!);
            if (onDashLine)
            {
                // The keys or the items of a list item are at the column of its first one, after the "-".
                target = GetColumn(GetEntries(value!)[0]).Length;
            }
            else
            {
                var column = GetColumn(entry).Length;

                // A nested block is indented past its key or its "-"; a sequence may also be at the column of a key.
                var isValid = entry is YamlEditableMappingEntry && value is YamlEditableSequence ? current >= column : current > column;
                target = isValid ? current : column + 2;
            }

            // A list item aligns its entries with its first one; elsewhere a valid layout is not changed.
            if (current < target || (onDashLine && current > target))
            {
                Shift(value!, target - current);
            }
        }

        foreach (var child in GetEntries(value!))
        {
            IndentValue(child);
        }
    }

    /// <summary>
    /// Indents the lines of <paramref name="scalar"/>, the value of <paramref name="entry"/>, after its first one past the column
    /// of the entry if they are not, and leaves the line breaks at the end of its text to the line of the entry: a scalar
    /// built in memory does not know its depth. Indenting more every line of a supported scalar never changes its value.
    /// </summary>
    private static void IndentScalar(YamlEditableEntry entry, YamlEditableScalar scalar)
    {
        var text = scalar.Text;
        if (text.Length == 0 && entry.SeparatorTrivia is { } separator)
        {
            // A missing value is written right after "key:" or "-": the spaces and the line break that led to the old value
            // would leave a blank line, or a comment of the entry on a line of its own. The comments go on the line of the
            // entry.
            if (HasComment(separator) && FindNewLine(separator) is not null)
            {
                var endsLine = FindNewLine(entry.EndOfLineTrivia) is not null;
                var line = KeepFirstLine(separator, entry.EndOfLineTrivia);
                if (!endsLine)
                {
                    line.RemoveAll(t => t.Kind == YamlEditableTriviaKind.NewLine);
                }

                entry.EndOfLineTrivia = line;
            }

            entry.SeparatorTrivia = null;
            return;
        }

        var end = GetContentEnd(text);
        if (end < text.Length)
        {
            scalar.SetTextUnchecked(text.Substring(0, end));
        }

        if (scalar.ReadValue is null)
        {
            // A new text takes the line break of the document, which does not change its value, and the value of "|" or ">"
            // ends with the line break of its last line, which the line of the entry gives.
            var newLine = GetDocumentNewLine(entry);
            scalar.SetTextUnchecked(UseNewLine(scalar.Text, newLine));
            if (KeepsFinalLineBreak(scalar.Text))
            {
                EnsureEndsWithNewLine(entry, newLine);
            }
        }

        var column = GetColumn(entry).Length;
        var indent = GetContinuationIndent(scalar.Text);
        if (indent >= 0 && indent <= column)
        {
            scalar.SetTextUnchecked(ReindentLines(scalar.Text, column + 2 - indent));
        }
    }

    /// <summary>
    /// Returns <paramref name="text"/> with each of its line breaks written as <paramref name="newLine"/>.
    /// </summary>
    private static string UseNewLine(string text, string newLine)
    {
        if (text.IndexOfAny(['\r', '\n']) < 0)
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length + 8);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is '\r' or '\n')
            {
                builder.Append(newLine);
                if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns the length of <paramref name="text"/> without the line breaks at its end. A line of spaces or tabs is kept: in
    /// a block scalar, it can be content.
    /// </summary>
    private static int GetContentEnd(string text)
    {
        var end = text.Length;
        while (end > 0 && text[end - 1] is '\r' or '\n')
        {
            end--;
        }

        return end;
    }

    /// <summary>
    /// Returns the smallest indentation of the lines of <paramref name="text"/> after its first one that are not blank, or -1.
    /// </summary>
    private static int GetContinuationIndent(string text)
    {
        var result = -1;
        var index = text.IndexOfAny(['\r', '\n']);
        while (index >= 0 && index < text.Length)
        {
            while (index < text.Length && text[index] is '\r' or '\n')
            {
                index++;
            }

            var start = index;
            while (index < text.Length && text[index] == ' ')
            {
                index++;
            }

            if (index < text.Length && text[index] is not ('\r' or '\n'))
            {
                result = result < 0 ? index - start : Math.Min(result, index - start);
            }

            index = text.IndexOfAny(['\r', '\n'], index);
        }

        return result;
    }

    /// <summary>
    /// Moves the lines of the value of <paramref name="entry"/> after its first line by <paramref name="delta"/> columns: a
    /// value on the line after its key or its "-", the entries of its nested block, at every depth, and the lines of its
    /// scalars on several lines. The layouts that the entry remembers no longer apply at the new column.
    /// </summary>
    private static void ShiftValue(YamlEditableEntry entry, int delta)
    {
        entry.LayoutMemo = null;
        entry.DashMemo = null;
        if (entry.SeparatorTrivia is { } separator)
        {
            for (var index = 1; index < separator.Count; index++)
            {
                if (separator[index - 1].Kind == YamlEditableTriviaKind.NewLine && separator[index].Kind == YamlEditableTriviaKind.Whitespace)
                {
                    separator[index] = new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, Reindent(separator[index].Text, delta));
                }
            }
        }

        switch (GetValue(entry))
        {
            case YamlEditableScalar scalar:
                scalar.SetTextUnchecked(ReindentLines(scalar.Text, delta));
                break;
            case { } value when IsBlock(value):
                Shift(value, delta);
                break;
        }
    }

    /// <summary>
    /// Returns the smallest indentation of the lines of the value of <paramref name="entry"/> after its first line, at every
    /// depth, or <see cref="int.MaxValue"/> if it has none.
    /// </summary>
    private static int GetSmallestIndent(YamlEditableEntry entry)
    {
        var smallest = int.MaxValue;
        if (entry.SeparatorTrivia is { } separator)
        {
            for (var index = 1; index < separator.Count; index++)
            {
                if (separator[index - 1].Kind == YamlEditableTriviaKind.NewLine && separator[index].Kind == YamlEditableTriviaKind.Whitespace)
                {
                    smallest = Math.Min(smallest, separator[index].Text.Length);
                }
            }
        }

        switch (entry.Value)
        {
            case YamlEditableScalar scalar when GetContinuationIndent(scalar.Text) is var indent and >= 0:
                smallest = Math.Min(smallest, indent);
                break;
            case { } value when IsBlock(value):
                foreach (var child in GetOwnLineEntries(value))
                {
                    smallest = Math.Min(smallest, child.Indent.Length);
                }

                foreach (var child in GetEntries(value))
                {
                    smallest = Math.Min(smallest, GetSmallestIndent(child));
                }

                break;
        }

        return smallest;
    }

    /// <summary>
    /// Captures the trivia of the root <paramref name="root"/> before its first entry is added, for <see cref="AfterEmptinessChanged"/>
    /// to restore them when its entries are removed again.
    /// </summary>
    private static Memo CaptureRoot(YamlEditableNode root) =>
        new(Clone(root.LeadingTrivia), Clone(root.Tree?.TrailingTrivia), GetEmptyFlowText(root));

    /// <summary>
    /// Ends <paramref name="header"/>, the comment lines at the start of a document, with a blank line.
    /// </summary>
    private static void EndWithBlankLine(List<YamlEditableTrivia> header, string newLine)
    {
        if (header[header.Count - 1].Kind != YamlEditableTriviaKind.NewLine)
        {
            header.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine));
        }

        // The last line is blank when nothing but spaces is between its line break and the line break before it.
        for (var index = header.Count - 2; index >= 0; index--)
        {
            switch (header[index].Kind)
            {
                case YamlEditableTriviaKind.NewLine:
                    return;
                case YamlEditableTriviaKind.Comment:
                    header.Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine));
                    return;
            }
        }
    }

    private static List<YamlEditableTrivia>? Clone(List<YamlEditableTrivia>? trivia) => trivia is null ? null : [.. trivia];

    /// <summary>
    /// Returns a text that tells two states of two lists of trivia apart.
    /// </summary>
    private static string Fingerprint(List<YamlEditableTrivia>? first, List<YamlEditableTrivia>? second)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var list in new[] { first, second })
        {
            if (list is null)
            {
                builder.Append('\u0002');
                continue;
            }

            foreach (var trivia in list)
            {
                builder.Append((char)('0' + (int)trivia.Kind)).Append(trivia.Text).Append('\u0001');
            }

            builder.Append('\u0003');
        }

        return builder.ToString();
    }

    /// <summary>
    /// The trivia of an entry, or of the root, before a change of its layout, which the inverse change restores when the
    /// trivia are still the ones the change produced.
    /// </summary>
    internal sealed class Memo
    {
        public Memo(List<YamlEditableTrivia>? first, List<YamlEditableTrivia>? second, string? text)
        {
            First = first;
            Second = second;
            Text = text;
        }

        public List<YamlEditableTrivia>? First { get; }

        public List<YamlEditableTrivia>? Second { get; }

        public string? Text { get; }

        public string? Produced { get; set; }

        /// <summary>
        /// Gets or sets whether the memo restores the layout of an entry whose value is a block, rather than a value on its
        /// line.
        /// </summary>
        public bool RestoresBlock { get; set; }

        public bool Matches(List<YamlEditableTrivia>? first, List<YamlEditableTrivia>? second) =>
            Produced is not null && string.Equals(Produced, Fingerprint(first, second), StringComparison.Ordinal);
    }

    private static YamlEditableNode? GetValue(YamlEditableNode node) => node is YamlEditableEntry entry ? entry.Value : null;

    private static List<YamlEditableEntry> GetEntries(YamlEditableNode value) => value switch
    {
        YamlEditableMapping map => new List<YamlEditableEntry>(map.Entries),
        YamlEditableSequence list => new List<YamlEditableEntry>(list.Items),
        _ => [],
    };

    /// <summary>
    /// Returns the entries of <paramref name="value"/> that start their own line: all of them, except
    /// the first key or item of a list item, which is on the line of the "-".
    /// </summary>
    private static List<YamlEditableEntry> GetOwnLineEntries(YamlEditableNode value)
    {
        var entries = GetEntries(value);
        if (SharesLineWithDash(value))
        {
            entries.RemoveAt(0);
        }

        return entries;
    }

    private static string GetIndent(YamlEditableEntry entry) => entry switch
    {
        YamlEditableMappingEntry mapEntry => mapEntry.Indent,
        YamlEditableSequenceEntry listEntry => listEntry.Indent,
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
            // The lines of a block or multi-line scalar move with its entry, so that their indentation relative to it, and
            // so the value, stay the same.
            ShiftValue(entry, delta);
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

    private static void EnsureEndsWithNewLine(YamlEditableEntry entry, YamlEditableTrivia newLine)
    {
        // A line that lost its line break when it became the last line of the document gets it back.
        var owner = GetLastLineOwner(entry);
        if (FindNewLine(owner.EndOfLineTrivia) is null)
        {
            (owner.EndOfLineTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, owner.RemovedLineBreak ?? newLine.Text));
        }

        owner.RemovedLineBreak = null;
    }

    private static void EnsureEndsWithNewLine(YamlEditableEntry entry, string newLine) =>
        EnsureEndsWithNewLine(entry, new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine));

    private static YamlEditableTrivia? FindNewLine(List<YamlEditableTrivia>? trivia) =>
        trivia?.Find(t => t.Kind == YamlEditableTriviaKind.NewLine);

    private static void SetIndent(YamlEditableEntry entry, string indent)
    {
        switch (entry)
        {
            case YamlEditableMappingEntry mapEntry:
                mapEntry.Indent = indent;
                break;
            case YamlEditableSequenceEntry listEntry:
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
    public static void KeepCommentsOutOfBlockScalars(YamlEditableEntry entry)
    {
        var owner = GetLastLineOwner(entry);
        if (GetValue(owner) is YamlEditableScalar scalar && IsBlockScalar(scalar.Text))
        {
            MoveCommentToHeader(owner.EndOfLineTrivia, scalar);
            MoveSpacesToHeader(owner.EndOfLineTrivia, scalar);
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
    /// Moves the spaces before the line break of <paramref name="trivia"/>, which would be written after the last line of the
    /// block scalar <paramref name="scalar"/> and could be part of its value, to the end of its header line.
    /// </summary>
    internal static void MoveSpacesToHeader(List<YamlEditableTrivia>? trivia, YamlEditableScalar scalar)
    {
        var count = 0;
        while (trivia is not null && count < trivia.Count && trivia[count].Kind == YamlEditableTriviaKind.Whitespace)
        {
            count++;
        }

        if (count == 0)
        {
            return;
        }

        var spaces = string.Concat(trivia!.GetRange(0, count).Select(t => t.Text));
        trivia.RemoveRange(0, count);
        var headerEnd = scalar.Text.IndexOfAny(['\r', '\n'], Math.Max(0, GetIndicatorIndex(scalar.Text)));
        scalar.SetTextUnchecked(scalar.Text.Insert(headerEnd < 0 ? scalar.Text.Length : headerEnd, spaces));
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
    private static void MoveHeaderComment(YamlEditableEntry entry, YamlEditableNode oldValue)
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

    /// <summary>
    /// Adds <paramref name="comment"/> to the first line of <paramref name="separator"/>, before its line break: after the
    /// comment already there, in the same comment, or after a space.
    /// </summary>
    private static void AddCommentToLine(List<YamlEditableTrivia> separator, YamlEditableTrivia comment)
    {
        var lineBreak = separator.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
        lineBreak = lineBreak < 0 ? separator.Count : lineBreak;
        var existing = separator.FindIndex(0, lineBreak, t => t.Kind == YamlEditableTriviaKind.Comment);
        if (existing >= 0)
        {
            separator[existing] = new YamlEditableTrivia(YamlEditableTriviaKind.Comment, separator[existing].Text + " " + comment.Text);
            return;
        }

        separator.InsertRange(lineBreak, [new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, " "), comment]);
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
        var newLine = FindNewLine(node.LeadingTrivia)
            ?? (node is YamlEditableEntry entry ? FindNewLine(entry.SeparatorTrivia) ?? FindNewLine(entry.EndOfLineTrivia) : null);
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

    private static YamlEditableEntry? GetLastEntry(YamlEditableNode? value) => value switch
    {
        YamlEditableMapping map when map.Entries.Count > 0 => map.Entries[map.Entries.Count - 1],
        YamlEditableSequence list when list.Items.Count > 0 => list.Items[list.Items.Count - 1],
        _ => null,
    };

    /// <summary>
    /// Returns the entry whose <see cref="YamlEditableEntry.EndOfLineTrivia"/> ends the last line
    /// of <paramref name="entry"/>: the entry itself, or the last entry of its nested blocks.
    /// </summary>
    private static YamlEditableEntry GetLastLineOwner(YamlEditableEntry entry)
    {
        while (true)
        {
            var value = entry switch
            {
                YamlEditableMappingEntry mapEntry => mapEntry.Value,
                YamlEditableSequenceEntry listEntry => listEntry.Value,
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