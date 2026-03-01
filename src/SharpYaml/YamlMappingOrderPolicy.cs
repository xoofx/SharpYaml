// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Controls member ordering for emitted mappings.
/// </summary>
public enum YamlMappingOrderPolicy
{
    /// <summary>
    /// Preserve declaration order when no explicit order attribute is provided.
    /// </summary>
    Declaration = 0,

    /// <summary>
    /// Sort mapping entries by final emitted member name.
    /// </summary>
    Sorted = 1,
}

