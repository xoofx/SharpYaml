// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A "- value" entry of a <see cref="YamlEditableSequence"/>.
/// </summary>
public sealed class YamlEditableSequenceEntry : YamlEditableEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableSequenceEntry"/> class. Added to a sequence, the entry is
    /// written at the column of its other entries, or under the key or the "-" of the sequence when it is its first entry.
    /// </summary>
    /// <param name="value">The value of the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is an entry.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, or is the root of a tree, or holds a block scalar with the keep
    /// indicator or an indentation indicator ("|+", "|2").
    /// </exception>
    public YamlEditableSequenceEntry(YamlEditableNode value)
        : this(value, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableSequenceEntry"/> class, with the indentation of its "-" when
    /// it is the first entry of a sequence. Next to other entries, it is written at their column.
    /// </summary>
    /// <param name="value">The value of the entry.</param>
    /// <param name="indent">The indentation text written before the "-" when it is the first entry of a sequence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="indent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is an entry.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, or is the root of a tree, or holds a block scalar with the keep
    /// indicator or an indentation indicator ("|+", "|2").
    /// </exception>
    public YamlEditableSequenceEntry(YamlEditableNode value, string indent)
        : base(YamlEditableKind.SequenceEntry)
    {
        Indent = indent ?? throw new ArgumentNullException(nameof(indent));
        Value = value;
    }

    /// <inheritdoc />
    private protected override void WriteCore(TextWriter writer)
    {
        writer.Write(Indent);
        writer.Write('-');

        if (SeparatorTrivia != null)
        {
            WriteTrivia(writer, SeparatorTrivia);
        }
        else if (!YamlEditableLayout.IsMissingValue(Value))
        {
            // "- value" and "- key: value": the first line of the entry follows the "-".
            writer.Write(' ');
        }

        Value.WriteTo(writer);
        WriteTrivia(writer, EndOfLineTrivia);
    }
}
