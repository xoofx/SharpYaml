// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A "key: value" entry of a <see cref="YamlEditableMapping"/>. The key is kept as its YAML source,
/// <see cref="KeyText"/>, with its quotes; <see cref="KeyValue"/> is the string it is read as, by which
/// <see cref="YamlEditableMapping.GetValue"/> finds it.
/// </summary>
public sealed class YamlEditableMappingEntry : YamlEditableEntry
{
    private string _keyText;
    private string? _keyValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableMappingEntry"/> class. Added to a mapping, the entry is
    /// written at the column of its other entries, or under the key of the mapping when it is its first entry.
    /// </summary>
    /// <param name="keyText">The YAML source of the key (the part before ": "), with its quotes if it has some.</param>
    /// <param name="value">The value of the entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyText"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is an entry.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, or is the root of a tree, or holds a block scalar with the keep
    /// indicator or an indentation indicator ("|+", "|2").
    /// </exception>
    /// <remarks>The key text is checked when the entry is added to a <see cref="YamlEditableMapping"/>.</remarks>
    public YamlEditableMappingEntry(string keyText, YamlEditableNode value)
        : this(keyText, value, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableMappingEntry"/> class, with the indentation of its key when
    /// it is the first entry of a mapping. Next to other entries, it is written at their column.
    /// </summary>
    /// <param name="keyText">The YAML source of the key (the part before ": "), with its quotes if it has some.</param>
    /// <param name="value">The value of the entry.</param>
    /// <param name="indent">The indentation text written before the key when it is the first entry of a mapping.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyText"/>, <paramref name="value"/> or <paramref name="indent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is an entry.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="value"/> already has a parent, or is the root of a tree, or holds a block scalar with the keep
    /// indicator or an indentation indicator ("|+", "|2").
    /// </exception>
    /// <remarks>The key text is checked when the entry is added to a <see cref="YamlEditableMapping"/>.</remarks>
    public YamlEditableMappingEntry(string keyText, YamlEditableNode value, string indent)
        : base(YamlEditableKind.MappingEntry)
    {
        _keyText = keyText ?? throw new ArgumentNullException(nameof(keyText));
        Indent = indent ?? throw new ArgumentNullException(nameof(indent));
        Value = value;
    }

    /// <summary>
    /// Gets or sets the YAML source of the key (the part before ": "), with its quotes if it has some.
    /// </summary>
    /// <exception cref="ArgumentNullException">The key text is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The entry is in a mapping, and the text is not one plain or quoted scalar on one line, or the mapping has another
    /// entry with the same key.
    /// </exception>
    public string KeyText
    {
        get => _keyText;
        set
        {
            ArgumentGuard.ThrowIfNull(value);
            if (Parent is YamlEditableMapping mapping)
            {
                mapping.ThrowIfInvalidKey(value, this);
            }

            _keyText = value;
            _keyValue = null;
        }
    }

    /// <summary>
    /// Gets the string the key is read as: <see cref="KeyText"/> without its quotes, with its escapes resolved. It is the
    /// string the parser reads, not the value of a schema: the key <c>1</c> is the string <c>1</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="KeyText"/> is not one plain or quoted scalar on one line.</exception>
    public string KeyValue => _keyValue ??= YamlEditableMapping.ReadKeyValue(_keyText);

    /// <inheritdoc />
    private protected override void WriteCore(TextWriter writer)
    {
        writer.Write(Indent);
        (writer as YamlEditableTextWriter)?.RecordKey(this);
        writer.Write(KeyText);
        writer.Write(':');

        if (SeparatorTrivia != null)
        {
            WriteTrivia(writer, SeparatorTrivia);
        }
        else if (!YamlEditableLayout.IsBlock(Value))
        {
            if (!YamlEditableLayout.IsMissingValue(Value))
            {
                writer.Write(' ');
            }
        }
        else
        {
            // A nested mapping or sequence without explicit separator starts on the next line.
            writer.Write('\n');
        }

        Value.WriteTo(writer);
        WriteTrivia(writer, EndOfLineTrivia);
    }
}
