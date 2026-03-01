// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies which constructor should be used during YAML deserialization.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false)]
public sealed class YamlConstructorAttribute : YamlAttribute
{
}

