// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A plain or quoted scalar value, e.g. the "value" in "key: value".
/// </summary>
public sealed class YamlEditableScalar : YamlEditableNode
{
    private string _text;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableScalar"/> class.
    /// </summary>
    /// <param name="text">The verbatim scalar text, including any quotes.</param>
    public YamlEditableScalar(string text) : base(YamlEditableKind.Scalar)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>
    /// Gets or sets the verbatim scalar text as it appears in YAML, including any quotes
    /// (e.g. <c>"line\n"</c> for a double-quoted scalar, or <c>value</c> for a plain one).
    /// Setting this value edits the node; reserializing the owning tree reflects the change
    /// while keeping every other node's original text untouched.
    /// </summary>
    public string Text
    {
        get => _text;
        set => _text = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
    {
        writer.Write(_text);
    }
}
