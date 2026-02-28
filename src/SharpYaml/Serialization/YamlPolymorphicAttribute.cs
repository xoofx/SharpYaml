using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Marks a base type as polymorphic for YAML serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class YamlPolymorphicAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the discriminator property name.
    /// </summary>
    public string? TypeDiscriminatorPropertyName { get; set; }

    /// <summary>
    /// Gets or sets the discriminator style.
    /// </summary>
    public YamlTypeDiscriminatorStyle DiscriminatorStyle { get; set; } = YamlTypeDiscriminatorStyle.Unspecified;
}
