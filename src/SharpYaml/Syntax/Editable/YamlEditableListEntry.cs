// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A single "- item" entry of a <see cref="YamlEditableList"/>.
/// </summary>
public sealed class YamlEditableListEntry : YamlEditableNode
{
    private YamlEditableNode? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableListEntry"/> class. Added to a list, the entry is written at
    /// the column of its other entries, or under the key or the "-" of the list when it is its first entry.
    /// </summary>
    /// <param name="value">The entry's value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, or is the root of a tree, or holds a block scalar with the keep
    /// indicator or an indentation indicator ("|+", "|2").
    /// </exception>
    public YamlEditableListEntry(YamlEditableNode value)
        : this(value, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableListEntry"/> class, with the indentation of its "-" when it is
    /// the first entry of a list. Next to other entries, it is written at their column.
    /// </summary>
    /// <param name="value">The entry's value.</param>
    /// <param name="indent">The indentation text written before the "-" when it is the first entry of a list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="indent"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, or is the root of a tree, or holds a block scalar with the keep
    /// indicator or an indentation indicator ("|+", "|2").
    /// </exception>
    public YamlEditableListEntry(YamlEditableNode value, string indent)
        : base(YamlEditableKind.ListEntry)
    {
        Indent = indent ?? throw new ArgumentNullException(nameof(indent));
        Value = value;
    }

    /// <summary>
    /// Gets the indentation text written before the "-" marker: empty when it follows the "-" of an outer list item.
    /// </summary>
    public string Indent { get; internal set; }

    /// <summary>
    /// Gets or sets this entry's value. Assigning a new value detaches the previous one and
    /// reparents the new node to this entry. A nested map or list whose entries are not after the "-"
    /// is indented to the column of its first entry, when this entry is in a list.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The value already has a parent, is this entry or one of its ancestors, or is the root of a tree; or it holds a block
    /// scalar with the keep indicator or an indentation indicator ("|+", "|2"), which the editable tree does not support.
    /// </exception>
    public YamlEditableNode Value
    {
        get => _value!;
        set
        {
            var oldValue = _value;
            YamlEditableLayout.ThrowIfUnsupportedValue(value ?? throw new ArgumentNullException(nameof(value)));
            ParentToThis(ref _value, value, this);
            if (oldValue is not null)
            {
                YamlEditableLayout.AfterValueChanged(this, oldValue);
            }

            // An entry that is not in a map or a list yet is indented when it is added to one; the parser, which
            // builds the entries before their trivia, keeps the layout it read.
            if (Parent is not null)
            {
                YamlEditableLayout.IndentValue(this);
            }
        }
    }

    /// <inheritdoc />
    private protected override void WriteCore(TextWriter writer)
    {
        writer.Write(Indent);
        writer.Write('-');

        if (TrailingTrivia != null)
        {
            WriteTrivia(writer, TrailingTrivia);
        }
        else if (!YamlEditableLayout.IsMissingValue(Value))
        {
            // "- value" and "- key: value": the first line of the item follows the "-".
            writer.Write(' ');
        }

        Value.WriteTo(writer);
        WriteTrivia(writer, EndOfLineTrivia);
    }
}
