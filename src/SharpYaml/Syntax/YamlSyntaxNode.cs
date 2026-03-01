// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Syntax;

/// <summary>
/// Represents a node in the YAML syntax tree.
/// </summary>
public abstract class YamlSyntaxNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlSyntaxNode"/> class.
    /// </summary>
    /// <param name="span">The node span excluding trivia.</param>
    /// <param name="fullSpan">The node span including trivia.</param>
    protected YamlSyntaxNode(YamlSourceSpan span, YamlSourceSpan fullSpan)
    {
        Span = span;
        FullSpan = fullSpan;
    }

    /// <summary>
    /// Gets the node span excluding trivia.
    /// </summary>
    public YamlSourceSpan Span { get; }

    /// <summary>
    /// Gets the node span including trivia.
    /// </summary>
    public YamlSourceSpan FullSpan { get; }
}
