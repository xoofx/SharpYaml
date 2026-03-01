// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Tokens;

/// <summary>
/// Base class for YAML tokens.
/// </summary>
public abstract class Token
{
    /// <summary>
    /// Gets the start of the token in the input stream.
    /// </summary>
    public Mark Start { get; }

    /// <summary>
    /// Gets the end of the token in the input stream.
    /// </summary>
    public Mark End { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> class.
    /// </summary>
    /// <param name="start">The start position of the token.</param>
    /// <param name="end">The end position of the token.</param>
    protected Token(Mark start, Mark end)
    {
        this.Start = start;
        this.End = end;
    }
}