// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Defines how object references are represented in YAML.
/// </summary>
public enum YamlReferenceHandling
{
    /// <summary>
    /// Do not emit anchors/aliases for repeated references.
    /// </summary>
    None = 0,

    /// <summary>
    /// Preserve object references using YAML anchors/aliases.
    /// </summary>
    Preserve = 1,
}

