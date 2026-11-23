// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using SharpYaml.Events;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// A plain, quoted or block scalar value, e.g. the "value" in "key: value". <see cref="Text"/> is its YAML source, with its
/// quotes; <see cref="Value"/> is the string it is read as.
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
            if (Parent is not null || Tree is not null)
            {
                YamlEditableLayout.ThrowIfUnsupportedScalarText(value);
            }

            if (!string.Equals(value, _text, StringComparison.Ordinal))
            {
                // A new text has a new value, which the tree cannot check.
                ReadValue = null;
            }

            if (Parent is null && Tree is null)
            {
                _text = value;
                return;
            }

            // The comment of the header line of a block scalar goes to the line of its entry, or after the root, unless
            // the new text has the same one.
            if (YamlEditableLayout.GetHeaderComment(_text) is { } headerComment
                && !YamlEditableLayout.HasSameHeaderComment(headerComment, value))
            {
                var target = Parent is YamlEditableEntry owner ? owner.EndOfLineTrivia ??= [] : Tree!.TrailingTrivia ??= [];
                target.InsertRange(0, headerComment);
            }

            _text = value;

            // The lines of the text go under the entry, and a comment on the line of the entry, or after a scalar at the
            // root, would follow the last line of a block scalar.
            if (Parent is YamlEditableEntry entry)
            {
                YamlEditableLayout.IndentValue(entry);
                YamlEditableLayout.KeepCommentsOutOfBlockScalars(entry);
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

                // The spaces at the end of the line would follow the last line of the block scalar.
                YamlEditableLayout.MoveSpacesToHeader(trailing, this);
            }
        }
    }

    /// <summary>
    /// Gets the value of this scalar as YAML reads it: without its quotes, with its escapes and its line folding resolved
    /// (the text <c>"a\tb"</c> is <c>a</c>, a tab and <c>b</c>), and empty for a missing value. It is the string that the
    /// parser reads, not the value of a schema: the text <c>1</c> is the string <c>1</c>. It is the value the scalar had when
    /// the document was parsed or when <see cref="YamlEditableTree.ToSyntaxTree"/> last succeeded, until its text is set,
    /// which ToSyntaxTree checks that the edits keep. A scalar whose text was set since is read where the text of its tree
    /// has it, or, when it is not in a tree, as the value of a sequence entry ("- text").
    /// </summary>
    /// <exception cref="YamlException">The text, or the text of the tree, is not valid YAML.</exception>
    /// <exception cref="InvalidOperationException">
    /// The text is not one scalar without a tag or an anchor, or the text of the tree would not be read as the tree.
    /// </exception>
    public string Value
    {
        get
        {
            if (ReadValue is { } value)
            {
                return value;
            }

            YamlEditableNode root = this;
            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root.Tree is { } tree ? tree.ReadValueOf(this) : ReadText(_text);
        }
    }

    private static string ReadText(string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        // The last line of a block scalar ends with the line break of its entry, which its text does not hold.
        if (YamlEditableLayout.IsBlockScalarText(text) && text[text.Length - 1] is not ('\n' or '\r'))
        {
            text += "\n";
        }

        var parser = Parser.CreateParser(new StringReader("- " + text));
        var events = new List<ParsingEvent>();
        while (parser.MoveNext())
        {
            events.Add(parser.Current!);
        }

        if (events is not [StreamStart, DocumentStart, SequenceStart, Events.Scalar { Tag: null, Anchor: null } scalar, SequenceEnd, DocumentEnd, StreamEnd])
        {
            throw new InvalidOperationException("The text of the scalar is not one scalar without a tag or an anchor.");
        }

        return scalar.Value;
    }

    /// <summary>
    /// Sets the text without the checks and the moves of comments of <see cref="Text"/>, for the layout code that
    /// moves a comment to the header line.
    /// </summary>
    internal void SetTextUnchecked(string text) => _text = text;

    /// <summary>
    /// Gets or sets the value this scalar was read as when the document was parsed or when the edits were last applied by
    /// <see cref="YamlEditableTree.ToSyntaxTree"/>, or <see langword="null"/> once its text is set: ToSyntaxTree checks that
    /// a scalar whose text was not set keeps it, wherever it is moved and whatever changes around it.
    /// </summary>
    internal string? ReadValue { get; set; }

    /// <inheritdoc />
    private protected override void WriteCore(TextWriter writer)
    {
        (writer as YamlEditableTextWriter)?.Record(this, _text);
        writer.Write(_text);
    }
}