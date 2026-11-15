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
    public Mark Start { get; private set; }

    /// <summary>
    /// Gets the end of the token in the input stream.
    /// </summary>
    public Mark End { get; private set; }

    /// <summary>
    /// The pool of the scanner this token goes back to once the parser consumed it, or 0 for a token that is never
    /// used again, as every token read through the public API of the scanner.
    /// </summary>
    internal byte PoolSlot;

    /// <summary>The kind of this token; <see cref="TokenKind.None"/> for a token class defined outside this library.</summary>
    internal TokenKind Kind;

    /// <summary>Gives a token of a scanner pool the locations of the token it now stands for.</summary>
    internal void Reset(in Mark start, in Mark end)
    {
        this.Start = start;
        this.End = end;
    }

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