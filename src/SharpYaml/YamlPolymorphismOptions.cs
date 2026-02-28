namespace SharpYaml;

/// <summary>
/// Configures polymorphic serialization behavior.
/// </summary>
public sealed class YamlPolymorphismOptions
{
    /// <summary>
    /// Gets or sets how type discriminators are represented.
    /// </summary>
    public YamlTypeDiscriminatorStyle DiscriminatorStyle { get; set; } = YamlTypeDiscriminatorStyle.Property;

    /// <summary>
    /// Gets or sets the property name used for discriminator-based polymorphism.
    /// </summary>
    public string TypeDiscriminatorPropertyName { get; set; } = "$type";

    /// <summary>
    /// Gets or sets behavior when an unknown derived type discriminator is encountered.
    /// </summary>
    public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; set; } = YamlUnknownDerivedTypeHandling.Fail;
}

