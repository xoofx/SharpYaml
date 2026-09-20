// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Defines how object references are represented in YAML.
/// </summary>
/// <remarks>
/// Reference preservation tracks CLR object identity, not original YAML source syntax.
/// Deserialization expands merge keys; serializing the result does not reconstruct merge keys
/// or original anchor names. Use <see cref="Syntax.YamlSyntaxTree"/> to preserve source text.
/// </remarks>
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

    /// <summary>
    /// Preserve object references using YAML anchors/aliases, emitting anchors only for objects that are referenced more than once.
    /// </summary>
    /// <remarks>This mode performs a pre-serialization pass to identify shared and cyclic references.</remarks>
    PreserveMinimal = 2,
}

