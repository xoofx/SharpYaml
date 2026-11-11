// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A single "key: value" entry of a <see cref="YamlEditableMap"/>. Mirrors
/// Tomlyn.Syntax.KeyValueSyntax: the key is kept as plain text (bare keys only, matching the
/// bounded scope of this editable tree), and the value is reparented through
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
    public YamlEditableMapEntry(string key, YamlEditableNode value, string indent)
        : base(YamlEditableKind.MapEntry)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Indent = indent ?? throw new ArgumentNullException(nameof(indent));
        Value = value;
    }

    /// <summary>
    /// Gets or sets the entry's key text (the part before ": ").
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets the indentation text written before the key on its own line.
    /// </summary>
    public string Indent { get; internal set; }

    /// <summary>
    /// Gets or sets the entry's value. Assigning a new value detaches the previous one and
    /// reparents the new node to this entry, following the same convention as
    /// Tomlyn.Syntax.KeyValueSyntax.Value. The line break and the same-line comment of the entry
    /// are kept: a nested map or list goes on the next line, after "key:" and the comment.
    /// </summary>
    public YamlEditableNode Value
    {
        get => _value!;
        set
        {
            var oldValue = _value;
            ParentToThis(ref _value, value ?? throw new ArgumentNullException(nameof(value)), this);
            if (oldValue is not null)
            {
                YamlEditableLayout.AfterValueChanged(this, oldValue, value, isMapEntry: true);
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
