// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Specifies the style of a sequence or mapping.
/// </summary>
public enum YamlStyle
{
    /// <summary>
    /// Let the emitter choose the style.
    /// </summary>
    Any,

    /// <summary>
    /// The block style.
    /// </summary>
    Block,

    /// <summary>
    /// The flow style.
    /// </summary>
    Flow
}