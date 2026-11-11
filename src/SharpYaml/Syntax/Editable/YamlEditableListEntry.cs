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
    /// Initializes a new instance of the <see cref="YamlEditableListEntry"/> class.
    /// </summary>
    /// <param name="value">The entry's value.</param>
    /// <param name="indent">The indentation text written before the "-" marker.</param>
    public YamlEditableListEntry(YamlEditableNode value, string indent)
        : base(YamlEditableKind.ListEntry)
    {
        Indent = indent ?? throw new ArgumentNullException(nameof(indent));
        Value = value;
    }

    /// <summary>
    /// Gets the indentation text written before the "-" marker.
    /// </summary>
    public string Indent { get; }

    /// <summary>
    /// Gets or sets this entry's value. Assigning a new value detaches the previous one and
    /// reparents the new node to this entry.
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
                YamlEditableLayout.AfterValueChanged(this, oldValue, value, isMapEntry: false);
            }
        }
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
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
