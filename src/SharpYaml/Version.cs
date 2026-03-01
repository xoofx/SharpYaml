// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Specifies the version of the YAML language.
/// </summary>
public class Version
{
    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Version"/> class.
    /// </summary>
    /// <param name="major">The the major version number.</param>
    /// <param name="minor">The the minor version number.</param>
    public Version(int major, int minor)
    {
        this.Major = major;
        this.Minor = minor;
    }

    /// <summary>
    /// Determines whether the specified System.Object is equal to the current System.Object.
    /// </summary>
    /// <param name="obj">The System.Object to compare with the current System.Object.</param>
    /// <returns>
    /// true if the specified System.Object is equal to the current System.Object; otherwise, false.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return obj is Version other && Major == other.Major && Minor == other.Minor;
    }

    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>
    /// A hash code for the current <see cref="T:System.Object"/>.
    /// </returns>
    public override int GetHashCode()
    {
        return Major.GetHashCode() ^ Minor.GetHashCode();
    }
}