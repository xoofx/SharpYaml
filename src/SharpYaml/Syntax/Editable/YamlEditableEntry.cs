// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// Base class of an entry: a "key: value" entry of a <see cref="YamlEditableMapping"/> or a "- value" entry of a
/// <see cref="YamlEditableSequence"/>, with its value, its indentation and the trivia of its lines.
/// </summary>
public abstract class YamlEditableEntry : YamlEditableNode
{
    private YamlEditableNode? _value;

    private protected YamlEditableEntry(YamlEditableKind kind) : base(kind)
    {
        Indent = string.Empty;
    }

    /// <summary>
    /// Gets the indentation text written before the key or the "-" on its own line: empty when the entry follows the "-"
    /// of a sequence entry ("- key: value", "- - value").
    /// </summary>
    public string Indent { get; internal set; }

    /// <summary>
    /// Gets or sets the value of this entry. Assigning a new value detaches the previous one and reparents the new node to
    /// this entry. The comment on the line of the entry stays on that line: a mapping or a sequence goes on the lines after
    /// the key or the "-". When the entry is in a mapping or a sequence, a new mapping, sequence or scalar on several lines
    /// whose lines are not indented under the entry is indented.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is an entry: an entry is in a mapping or a sequence.</exception>
    /// <exception cref="InvalidOperationException">
    /// The value already has a parent, is this entry or one of its ancestors, or is the root of a tree; or it holds a block
    /// scalar with the keep indicator or an indentation indicator ("|+", "|2"), which the editable tree does not support.
    /// </exception>
    public YamlEditableNode Value
    {
        get => _value!;
        set
        {
            ArgumentGuard.ThrowIfNull(value);
            if (value is YamlEditableEntry)
            {
                throw new ArgumentException("An entry cannot be the value of an entry: add it to a YamlEditableMapping or a YamlEditableSequence.", nameof(value));
            }

            var oldValue = _value;
            YamlEditableLayout.ThrowIfUnsupportedValue(value);
            ParentToThis(ref _value, value, this);
            if (oldValue is not null)
            {
                YamlEditableLayout.AfterValueChanged(this, oldValue);
            }

            // An entry that is not in a mapping or a sequence yet is indented when it is added to one; the parser, which
            // builds the entries before their trivia, keeps the layout it read.
            if (Parent is not null)
            {
                YamlEditableLayout.IndentValue(this);
            }
        }
    }

    /// <summary>
    /// Gets or sets the trivia between the ":" of the key or the "-" and the value: the spaces before a value on the line of
    /// the entry, or the comment and the line break of that line when the value starts on a later line (a mapping, a
    /// sequence, or a value on the line after its key). <see langword="null"/> writes a space before a value on the line of
    /// the entry, including the first entry of the mapping or sequence of a sequence entry ("- key: value"), and a line break
    /// before the mapping or sequence with entries of a mapping entry (an empty one is written "{}" or "[]" on the line).
    /// </summary>
    public List<YamlEditableTrivia>? SeparatorTrivia { get; set; }

    /// <summary>
    /// Gets or sets the trivia after a value on the line of the entry, up to and including the line break: a comment and the
    /// line break that ends the line. <see langword="null"/> on the last line of a document without a final line break, and
    /// when the value is a mapping or a sequence, whose entries end their own lines.
    /// </summary>
    public List<YamlEditableTrivia>? EndOfLineTrivia { get; set; }

    /// <summary>
    /// Gets or sets the trivia after the "-" of a sequence entry before its first entry, on the line of the "-", was removed,
    /// to restore when an entry is inserted first again.
    /// </summary>
    internal YamlEditableLayout.Memo? DashMemo { get; set; }

    /// <summary>
    /// Gets or sets the column this entry was at when it was removed from a mapping or a sequence: the lines of its value
    /// move from there when it is added again.
    /// </summary>
    internal int? DetachedColumn { get; set; }

    /// <summary>
    /// Gets or sets the root of the tree this entry was removed from: added again to the same document, it keeps its line
    /// breaks.
    /// </summary>
    internal YamlEditableNode? DetachedRoot { get; set; }

    /// <summary>
    /// Gets or sets the line break removed from the last line of this entry when it became the last line of a document
    /// without a final line break, to end the line with it again when an entry is added after it.
    /// </summary>
    internal string? RemovedLineBreak { get; set; }
}
