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
        init
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
    /// <exception cref="ArgumentException">Value is <see langword="null"/> or empty.</exception>
    public string TypeDiscriminatorPropertyName
    {
        get => _typeDiscriminatorPropertyName;
        init
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("TypeDiscriminatorPropertyName cannot be null or empty.", nameof(value));
            }

            _typeDiscriminatorPropertyName = value;
        }
    }

    /// <summary>
    /// Gets or sets behavior when an unknown derived type discriminator is encountered.
    /// </summary>
    public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; init; } = YamlUnknownDerivedTypeHandling.Fail;

    private YamlTypeDiscriminatorStyle _discriminatorStyle = YamlTypeDiscriminatorStyle.Property;
    private string _typeDiscriminatorPropertyName = "$type";
}
