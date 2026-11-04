// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies which code the SharpYaml source generator emits for a <see cref="YamlSerializerContext"/>.
/// </summary>
/// <remarks>
/// This is inspired by <c>System.Text.Json</c>'s <c>JsonSourceGenerationMode</c>. Setting it on
/// <see cref="YamlSourceGenerationOptionsAttribute.GenerationMode"/> lets a context that only reads YAML skip the writers, or the reverse,
/// which keeps the generated code and the compile time down.
/// </remarks>
[Flags]
public enum YamlSourceGenerationMode
{
    /// <summary>
    /// Emits both the deserialization and the serialization code.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Emits the serialization code. Deserializing a type from the context throws <see cref="NotSupportedException"/>
    /// unless <see cref="Deserialization"/> is also set.
    /// </summary>
    Serialization = 1,

    /// <summary>
    /// Emits the deserialization code. Serializing a type from the context throws <see cref="NotSupportedException"/>
    /// unless <see cref="Serialization"/> is also set.
    /// </summary>
    Deserialization = 2,
}
