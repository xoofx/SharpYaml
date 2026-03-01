using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Registers a derived type for polymorphic YAML serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class YamlDerivedTypeAttribute : YamlAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlDerivedTypeAttribute"/> class.
    /// </summary>
    /// <param name="derivedType">The derived CLR type.</param>
    /// <param name="discriminator">The discriminator value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="derivedType"/> or <paramref name="discriminator"/> is <see langword="null"/>.</exception>
    public YamlDerivedTypeAttribute(Type derivedType, string discriminator)
    {
        ArgumentNullException.ThrowIfNull(derivedType);
        ArgumentNullException.ThrowIfNull(discriminator);

        DerivedType = derivedType;
        Discriminator = discriminator;
    }

    /// <summary>
    /// Gets the derived CLR type.
    /// </summary>
    public Type DerivedType { get; }

    /// <summary>
    /// Gets the discriminator value.
    /// </summary>
    public string Discriminator { get; }

    /// <summary>
    /// Gets or sets an optional explicit YAML tag for the derived type.
    /// </summary>
    public string? Tag { get; set; }
}

