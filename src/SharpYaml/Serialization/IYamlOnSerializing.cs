// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies that <see cref="OnSerializing"/> should be called before serialization occurs.
/// </summary>
public interface IYamlOnSerializing
{
    /// <summary>
    /// Called before the instance is serialized.
    /// </summary>
    void OnSerializing();
}

