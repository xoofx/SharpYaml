// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// Identifies the kind of an editable YAML node.
/// </summary>
public enum YamlEditableKind
{
    /// <summary>
    /// A block mapping: "key: value" entries at the same column (<see cref="YamlEditableMapping"/>).
    /// </summary>
    Mapping,

    /// <summary>
    /// A "key: value" entry of a mapping (<see cref="YamlEditableMappingEntry"/>).
    /// </summary>
    MappingEntry,

    /// <summary>
    /// A block sequence: "- value" entries at the same column (<see cref="YamlEditableSequence"/>).
    /// </summary>
    Sequence,

    /// <summary>
    /// A "- value" entry of a sequence (<see cref="YamlEditableSequenceEntry"/>).
    /// </summary>
    SequenceEntry,

    /// <summary>
    /// A plain, quoted or block scalar (<see cref="YamlEditableScalar"/>).
    /// </summary>
    Scalar,
}
