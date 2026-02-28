using System;

namespace SharpYaml;

/// <summary>
/// Configures polymorphic serialization behavior.
/// </summary>
public sealed class YamlPolymorphismOptions
{
    /// <summary>
    /// Gets or sets how type discriminators are represented.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is <see cref="YamlTypeDiscriminatorStyle.Unspecified"/>.</exception>
    public YamlTypeDiscriminatorStyle DiscriminatorStyle
    {
        get => _discriminatorStyle;
        set
        {
            if (value == YamlTypeDiscriminatorStyle.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "DiscriminatorStyle cannot be Unspecified on options.");
            }

            _discriminatorStyle = value;
        }
    }

    /// <summary>
    /// Gets or sets the property name used for discriminator-based polymorphism.
    /// </summary>
    public string TypeDiscriminatorPropertyName { get; set; } = "$type";

    /// <summary>
    /// Gets or sets behavior when an unknown derived type discriminator is encountered.
    /// </summary>
    public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; set; } = YamlUnknownDerivedTypeHandling.Fail;

    private YamlTypeDiscriminatorStyle _discriminatorStyle = YamlTypeDiscriminatorStyle.Property;
}
