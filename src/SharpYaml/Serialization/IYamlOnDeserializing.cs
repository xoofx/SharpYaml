// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies that <see cref="OnDeserializing"/> should be called before deserialization occurs.
/// </summary>
public interface IYamlOnDeserializing
{
    /// <summary>
    /// Called before the instance is populated during deserialization.
    /// </summary>
    void OnDeserializing();
}

