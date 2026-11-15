// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Represents a simple key.
/// </summary>
/// <remarks>A struct, kept in the scanner's stack and changed in place: the scanner saves one for most tokens.</remarks>
internal struct SimpleKey
{
    /// <summary>
    /// Gets or sets a value indicating whether this instance is possible.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is possible; otherwise, <c>false</c>.
    /// </value>
    public bool IsPossible;

    /// <summary>
    /// Gets a value indicating whether this instance is required.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance is required; otherwise, <c>false</c>.
    /// </value>
    public readonly bool IsRequired;

    /// <summary>
    /// Gets the token number.
    /// </summary>
    /// <value>The token number.</value>
    public readonly int TokenNumber;

    /// <summary>
    /// Gets the mark that indicates the location of the simple key.
    /// </summary>
    /// <value>The mark.</value>
    public readonly Mark Mark;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleKey"/> struct.
    /// </summary>
    public SimpleKey(bool isPossible, bool isRequired, int tokenNumber, Mark mark)
    {
        this.IsPossible = isPossible;
        this.IsRequired = isRequired;
        this.TokenNumber = tokenNumber;
        this.Mark = mark;
    }
}
