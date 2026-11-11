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
    /// Prepares <paramref name="entry"/> to be appended after <paramref name="previous"/>.
    /// </summary>
    public static void PrepareAppend(YamlEditableNode? previous, YamlEditableNode entry)
    {
        if (previous is null)
        {
            return;
        }

        var owner = GetLastLineOwner(previous);
        var newLine = FindNewLine(owner.EndOfLineTrivia) ?? FindNewLine(owner.TrailingTrivia);
        if (newLine is null)
        {
            // The document has no final line break: end the previous line, and leave the new last
            // line without one, as the document was.
            (owner.EndOfLineTrivia ??= []).Add(new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, DefaultNewLine));
            return;
        }

        EnsureEndsWithNewLine(entry, newLine);
    }

    /// <summary>
    /// Prepares <paramref name="entry"/> to be inserted before <paramref name="next"/>.
    /// </summary>
    public static void PrepareInsert(YamlEditableNode next, YamlEditableNode entry) =>
        EnsureEndsWithNewLine(entry, FindNewLine(GetLastLineOwner(next).EndOfLineTrivia)?.Text ?? DefaultNewLine);

    /// <summary>
    /// Moves the line break of an entry after its value changed from <paramref name="oldValue"/>
    /// to <paramref name="entry"/>'s current value. <paramref name="isMapEntry"/> is
    /// <see langword="true"/> for a map entry, whose nested block goes on the next line.
    /// </summary>
    public static void AfterValueChanged(YamlEditableNode entry, YamlEditableNode oldValue, YamlEditableNode newValue, bool isMapEntry)
    {
        var oldLastEntry = GetLastEntry(oldValue);
        var oldOwner = oldLastEntry is null ? entry : GetLastLineOwner(oldLastEntry);
        var newLine = FindNewLine(oldOwner.EndOfLineTrivia) ?? (oldLastEntry is null ? null : FindNewLine(oldOwner.TrailingTrivia));

        if (isMapEntry && !IsBlock(oldValue) && IsBlock(newValue))
        {
            // "key: value # comment\n" becomes "key: # comment\n" followed by the nested block.
            entry.TrailingTrivia = FindNewLine(entry.EndOfLineTrivia) is not null
                ? entry.EndOfLineTrivia
                : [new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, newLine?.Text ?? DefaultNewLine)];
            entry.EndOfLineTrivia = null;
        }
        else if (isMapEntry && IsBlock(oldValue) && !IsBlock(newValue))
        {
            // "key: # comment\n" followed by a nested block becomes "key: value # comment\n".
            entry.EndOfLineTrivia = entry.TrailingTrivia is { } separator && FindNewLine(separator) is not null ? separator : null;
            entry.TrailingTrivia = null;
        }

        if (newLine is not null)
        {
            EnsureEndsWithNewLine(entry, newLine);
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
            foreach (var trivia in listEntry.TrailingTrivia)
            {
                if (trivia.Kind != YamlEditableTriviaKind.Whitespace)
                {
                    return entry.Indent;
                }

                width += trivia.Text.Length;
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
