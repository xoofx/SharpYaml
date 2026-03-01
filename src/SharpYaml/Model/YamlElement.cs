// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Model;

/// <summary>Represents the Yaml Element.</summary>
public abstract class YamlElement : YamlNode
{
    /// <summary>Gets or sets anchor.</summary>
    public abstract string? Anchor { get; set; }
    /// <summary>Gets or sets tag.</summary>
    public abstract string? Tag { get; set; }
    /// <summary>Gets is Canonical.</summary>
    public abstract bool IsCanonical { get; }
}