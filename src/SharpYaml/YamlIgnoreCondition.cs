// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Specifies when a member should be ignored during YAML serialization or deserialization.
/// </summary>
public enum YamlIgnoreCondition
{
    /// <summary>
    /// Never ignore a member during serialization or deserialization.
    /// </summary>
    Never = 0,

    // Values 0-2 are preserved for compatibility with earlier SharpYaml releases.

    /// <summary>
    /// Always ignore a member during serialization and deserialization.
    /// </summary>
    Always = 3,

    /// <summary>
    /// Ignore a member during serialization when its value is the type default.
    /// </summary>
    WhenWritingDefault = 2,

    /// <summary>
    /// Ignore a member during serialization when its value is <see langword="null"/>.
    /// </summary>
    WhenWritingNull = 1,

    /// <summary>
    /// Ignore a member during serialization.
    /// </summary>
    WhenWriting = 4,

    /// <summary>
    /// Ignore a member during deserialization.
    /// </summary>
    WhenReading = 5,
}
