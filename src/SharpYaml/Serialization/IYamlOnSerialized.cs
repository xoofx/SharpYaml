// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies that <see cref="OnSerialized"/> should be called after serialization occurs.
/// </summary>
public interface IYamlOnSerialized
{
    /// <summary>
    /// Called after the instance has been serialized.
    /// </summary>
    void OnSerialized();
}

