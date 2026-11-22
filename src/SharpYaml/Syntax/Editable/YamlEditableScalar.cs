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
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
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
    /// <exception cref="ArgumentNullException">The text is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The scalar is the value of an entry or the root of a tree, and the text is a block scalar with the keep indicator or
    /// an indentation indicator ("|+", "|2"), which the editable tree does not support.
    /// </exception>
    public string Text
    {
        get => _text;
        set
        {
            ArgumentGuard.ThrowIfNull(value);
            if (Parent is null && Tree is null)
            {
                _text = value;
                return;
            }

            YamlEditableLayout.ThrowIfUnsupportedScalarText(value);
            _text = value;

            // A comment on the line of the entry, or after a scalar at the root, would follow the last line of a
            // block scalar.
            if (Parent is not null)
            {
                YamlEditableLayout.KeepCommentsOutOfBlockScalars(Parent);
            }
            else if (Tree!.TrailingTrivia is { } trailing && YamlEditableLayout.IsBlockScalarText(value))
            {
                // The comment is removed from the trivia before the text is set again, which ends the recursion.
                var lineBreak = trailing.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
                var comment = trailing.FindIndex(t => t.Kind == YamlEditableTriviaKind.Comment);
                if (comment >= 0 && (lineBreak < 0 || comment < lineBreak))
                {
                    YamlEditableLayout.MoveCommentToHeader(trailing, this);
                }
            }
        }
    }

    /// <inheritdoc />
    protected override void WriteCore(TextWriter writer)
    {
        writer.Write(_text);
    }
}
