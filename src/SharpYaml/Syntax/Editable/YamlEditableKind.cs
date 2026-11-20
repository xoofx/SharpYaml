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
    /// A block mapping ("key: value" pairs sharing the same indentation).
    /// </summary>
    Map,

    /// <summary>
    /// A single "key: value" entry of a <see cref="YamlEditableMap"/>.
    /// </summary>
    MapEntry,

    /// <summary>
    /// A block sequence ("- item" entries sharing the same indentation).
    /// </summary>
    List,

    /// <summary>
    /// A single "- item" entry of a <see cref="YamlEditableList"/>.
    /// </summary>
    ListEntry,

    /// <summary>
    /// A plain, quoted, or block scalar value.
    /// </summary>
    Scalar,
}
