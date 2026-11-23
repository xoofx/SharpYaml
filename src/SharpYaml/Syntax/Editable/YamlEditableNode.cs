// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// Base class of a node of an editable YAML tree: a mapping, a sequence, an entry or a scalar. Comments and blank lines are
/// kept as trivia attached to the nodes, children are set through typed properties that reparent them, and
/// <see cref="WriteTo(TextWriter)"/> writes the text the node was read from unless it was edited.
/// </summary>
public abstract class YamlEditableNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEditableNode"/> class. Only the node types of this namespace derive
    /// from it.
    /// </summary>
    /// <param name="kind">The kind of this node.</param>
    private protected YamlEditableNode(YamlEditableKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets the kind of this node.
    /// </summary>
    public YamlEditableKind Kind { get; }

    /// <summary>
    /// Gets the parent of this node, or <see langword="null"/> for the root.
    /// </summary>
    public YamlEditableNode? Parent { get; internal set; }

    /// <summary>
    /// Gets or sets whether this node is the root of a <see cref="YamlEditableTree"/>, which cannot become a value.
    /// </summary>
    internal bool IsTreeRoot => Tree is not null;

    /// <summary>
    /// Gets or sets the tree whose root this node is, or <see langword="null"/>.
    /// </summary>
    internal YamlEditableTree? Tree { get; set; }

    /// <summary>
    /// Gets or sets the first line break of the text that a root was read from, for the line breaks of new entries.
    /// </summary>
    internal string? DocumentNewLine { get; set; }

    /// <summary>
    /// Gets or sets the trivia that an entry had before its value became a block, or that a root had before its first entry
    /// was added, to restore when the value is written on its line again or the root is empty again.
    /// </summary>
    internal YamlEditableLayout.Memo? LayoutMemo { get; set; }

    /// <summary>
    /// Gets or sets the spaces after a root "{}" or "[]" that its first entry took at the end of its line, which are the
    /// root's again when it is empty again.
    /// </summary>
    internal List<YamlEditableTrivia>? MovedSpaces { get; set; }

    /// <summary>
    /// Gets or sets the comment lines and blank lines before this node, in source order, with the line break of each line:
    /// those above an entry, or above the first entry or the scalar of a document. <see langword="null"/> if there are none.
    /// </summary>
    public List<YamlEditableTrivia>? LeadingTrivia { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        var writer = new StringWriter();
        WriteTo(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Writes this node back to YAML text.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void WriteTo(TextWriter writer)
    {
        ArgumentGuard.ThrowIfNull(writer);
        WriteLeadingTrivia(writer);
        WriteCore(writer);
    }

    /// <summary>
    /// Writes the node-specific content (without leading trivia).
    /// </summary>
    private protected abstract void WriteCore(TextWriter writer);

    private void WriteLeadingTrivia(TextWriter writer) => WriteTrivia(writer, LeadingTrivia);

    /// <summary>
    /// Writes the text of each element of <paramref name="trivia"/>, if any.
    /// </summary>
    private protected static void WriteTrivia(TextWriter writer, List<YamlEditableTrivia>? trivia)
    {
        if (trivia is null)
        {
            return;
        }

        foreach (var item in trivia)
        {
            writer.Write(item.Text);
        }
    }

    /// <summary>
    /// Throws if <paramref name="node"/> is <paramref name="owner"/> or one of its ancestors: attaching it under
    /// <paramref name="owner"/> would make the tree a cycle.
    /// </summary>
    private protected static void ThrowIfSelfOrAncestor(YamlEditableNode node, YamlEditableNode owner)
    {
        for (var current = owner; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, node))
            {
                throw new InvalidOperationException("A node cannot be attached under itself or one of its descendants.");
            }
        }
    }

    /// <summary>
    /// Reparents <paramref name="node"/> to this instance, detaching it from any previous
    /// parent slot and throwing if it is already parented elsewhere. Mirrors
    /// Tomlyn.Syntax.SyntaxNode.ParentToThis.
    /// </summary>
    private protected static void ParentToThis<TNode>(ref TNode? slot, TNode? node, YamlEditableNode owner)
        where TNode : YamlEditableNode
    {
        if (node?.Parent != null)
        {
            throw new InvalidOperationException("The node is already parented to another node.");
        }

        if (node is not null)
        {
            if (node.IsTreeRoot)
            {
                throw new InvalidOperationException("The root of a YamlEditableTree cannot be the value of an entry.");
            }

            ThrowIfSelfOrAncestor(node, owner);
        }

        if (slot != null)
        {
            slot.Parent = null;
        }

        if (node != null)
        {
            node.Parent = owner;
        }

        slot = node;
    }
}