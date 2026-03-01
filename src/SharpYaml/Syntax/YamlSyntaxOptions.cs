// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Syntax;

/// <summary>
/// Options used by <see cref="YamlSyntaxTree"/> parsing.
/// </summary>
public sealed class YamlSyntaxOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether trivia (whitespace/newline/comments) should be included.
    /// </summary>
    public bool IncludeTrivia { get; set; } = true;
}
