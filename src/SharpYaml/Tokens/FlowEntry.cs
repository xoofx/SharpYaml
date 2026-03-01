// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Tokens;

/// <summary>
/// Represents a flow entry event.
/// </summary>
public class FlowEntry : Token
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlowEntry"/> class.
    /// </summary>
    public FlowEntry()
        : this(Mark.Empty, Mark.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowEntry"/> class.
    /// </summary>
    /// <param name="start">The start position of the token.</param>
    /// <param name="end">The end position of the token.</param>
    public FlowEntry(Mark start, Mark end)
        : base(start, end)
    {
    }
}