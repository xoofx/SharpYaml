// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Defines behavior when an unknown derived type discriminator is encountered.
/// </summary>
public enum YamlUnknownDerivedTypeHandling
{
    /// <summary>
    /// Use the serializer options default.
    /// </summary>
    Unspecified = -1,

    /// <summary>
    /// Throw when an unknown discriminator is encountered.
    /// </summary>
    Fail = 0,

    /// <summary>
    /// Fall back to the configured base type.
    /// </summary>
    FallBackToBase = 1,
}

