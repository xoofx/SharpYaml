// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Model;

/// <summary>Represents the Yaml Container.</summary>
public abstract class YamlContainer : YamlElement
{
    /// <summary>Gets or sets style.</summary>
    public abstract YamlStyle Style { get; set; }
    /// <summary>Gets or sets is Implicit.</summary>
    public abstract bool IsImplicit { get; set; }
}