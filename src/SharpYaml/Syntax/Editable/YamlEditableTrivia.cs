// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

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
    /// <param name="text">
    /// The verbatim source text: spaces and tabs for <see cref="YamlEditableTriviaKind.Whitespace"/>, "\n", "\r\n" or "\r"
    /// for <see cref="YamlEditableTriviaKind.NewLine"/>, and a "#" followed by the rest of a line for
    /// <see cref="YamlEditableTriviaKind.Comment"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a kind of trivia.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not a text of that kind.</exception>
    public YamlEditableTrivia(YamlEditableTriviaKind kind, string text)
    {
        ArgumentGuard.ThrowIfNull(text);
        var valid = kind switch
        {
            YamlEditableTriviaKind.Whitespace => text.Trim(' ', '\t').Length == 0,
            YamlEditableTriviaKind.NewLine => text is "\n" or "\r\n" or "\r",
            YamlEditableTriviaKind.Comment => text.Length > 0 && text[0] == '#' && text.IndexOfAny(['\r', '\n']) < 0,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        if (!valid)
        {
            throw new ArgumentException($"The text is not the text of a {kind} trivia.", nameof(text));
        }

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
