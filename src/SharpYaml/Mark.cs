// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Represents a location inside a file
/// </summary>
public readonly struct Mark
{
    /// <summary>Initializes a new mark at the specified source location.</summary>
    public Mark(int index, int line, int column)
    {
        this.Index = index;
        this.Line = line;
        this.Column = column;
    }

    /// <summary>
    /// Gets / sets the absolute offset in the file
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets / sets the number of the line
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets / sets the index of the column
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets a <see cref="Mark"/> with empty values.
    /// </summary>
    public static readonly Mark Empty;

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="System.String"/> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"Lin: {Line}, Col: {Column}, Chr: {Index}";
    }
}