// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Tokens;

/// <summary>
/// Represents a tag token.
/// </summary>
public class Tag : Token
{

    /// <summary>
    /// Gets the handle.
    /// </summary>
    /// <value>The handle.</value>
    public string Handle { get; }

    /// <summary>
    /// Gets the suffix.
    /// </summary>
    /// <value>The suffix.</value>
    public string Suffix { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag"/> class.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="suffix">The suffix.</param>
    public Tag(string handle, string suffix)
        : this(handle, suffix, Mark.Empty, Mark.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag"/> class.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="suffix">The suffix.</param>
    /// <param name="start">The start position of the token.</param>
    /// <param name="end">The end position of the token.</param>
    public Tag(string handle, string suffix, Mark start, Mark end)
        : base(start, end)
    {
        this.Handle = handle;
        this.Suffix = suffix;
    }
}