// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A single "key: value" entry of a <see cref="YamlEditableMap"/>. Mirrors
/// Tomlyn.Syntax.KeyValueSyntax: the key is kept as its raw source text, with any quotes
/// (<see cref="YamlEditableMap.GetValue"/> compares by the key's value), and the value is reparented through
/// <see cref="Value"/> like any other <see cref="YamlEditableNode"/> child.
/// </summary>
public sealed class YamlEditableMapEntry : YamlEditableNode
{
    private YamlEditableNode? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableMapEntry"/> class.
    /// </summary>
    /// <param name="key">The entry's key text (the part before ": ").</param>
    /// <param name="value">The entry's value.</param>
    /// <param name="indent">The indentation text written before the key on its own line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/>, <paramref name="value"/> or <paramref name="indent"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="value"/> already has a parent, or is the root of a tree.</exception>
    /// <remarks>The key text is checked when the entry is added to a <see cref="YamlEditableMap"/>.</remarks>
    public YamlEditableMapEntry(string key, YamlEditableNode value, string indent)
        : base(YamlEditableKind.MapEntry)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        Indent = indent ?? throw new ArgumentNullException(nameof(indent));
        Value = value;
    }

    private string _key;

    /// <summary>
    /// Gets or sets the entry's raw key text (the part before ": "), including any quotes.
    /// </summary>
    /// <exception cref="ArgumentNullException">The key text is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The entry is in a map, and the text is not one plain or quoted scalar on one line, or the map has another entry
    /// with the same key.
    /// </exception>
    public string Key
    {
        get => _key;
        set
        {
            ArgumentGuard.ThrowIfNull(value);
            if (Parent is YamlEditableMap map)
            {
                map.ThrowIfInvalidKey(value, this);
            }

            _key = value;
        }
    }

    /// <summary>
    /// Gets the indentation text written before the key on its own line.
    /// </summary>
    public string Indent { get; internal set; }

    /// <summary>
    /// Gets or sets the entry's value. Assigning a new value detaches the previous one and
    /// reparents the new node to this entry, following the same convention as
    /// Tomlyn.Syntax.KeyValueSyntax.Value. The line break and the same-line comment of the entry
    /// are kept: a nested map or list goes on the next line, after "key:" and the comment. A nested
    /// map or list whose entries are not indented past the key is indented by two spaces, when the
    /// entry is in a map.
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
    protected override void WriteCore(TextWriter writer)
    {
        writer.Write(Indent);
        writer.Write(Key);
        writer.Write(':');

        if (TrailingTrivia != null)
        {
            WriteTrivia(writer, TrailingTrivia);
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
            // A nested map or list without explicit separator starts on the next line.
            writer.Write('\n');
        }

        Value.WriteTo(writer);
        WriteTrivia(writer, EndOfLineTrivia);
    }
}
