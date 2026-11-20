// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A piece of source text that carries no semantic value on its own: a comment, a blank line,
/// or the indentation/newline around them. Kept verbatim so that editing a node does not disturb
/// unrelated comments and formatting.
/// </summary>
public sealed class YamlEditableTrivia
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableTrivia"/> class.
    /// </summary>
    /// <param name="kind">The kind of this trivia.</param>
    /// <param name="text">The verbatim source text.</param>
    public YamlEditableTrivia(YamlEditableTriviaKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    /// <summary>
    /// Gets the kind of this trivia.
    /// </summary>
    public YamlEditableTriviaKind Kind { get; }

    /// <summary>
    /// Gets the verbatim source text, including the comment marker ("#") and any trailing
    /// newline.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>
/// Identifies the kind of <see cref="YamlEditableTrivia"/>.
/// </summary>
public enum YamlEditableTriviaKind
{
    /// <summary>
    /// Horizontal whitespace (indentation).
    /// </summary>
    Whitespace,

    /// <summary>
    /// A line break.
    /// </summary>
    NewLine,

    /// <summary>
    /// A "# ..." comment, without the trailing newline.
    /// </summary>
    Comment,
}
