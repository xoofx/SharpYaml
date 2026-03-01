// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Instructs the YamlSerializer not to serialize the public field or public read/write property value.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class YamlIgnoreAttribute : YamlAttribute
{
}