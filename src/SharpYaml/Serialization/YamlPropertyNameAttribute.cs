// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies the serialized YAML property name for a member.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class YamlPropertyNameAttribute : YamlAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlPropertyNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The serialized member name.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public YamlPropertyNameAttribute(string name)
    {
        ArgumentGuard.ThrowIfNull(name);
        if (name.Length == 0)
        {
            throw new ArgumentException("Property name cannot be empty.", nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// Gets the serialized member name.
    /// </summary>
    public string Name { get; }
}

