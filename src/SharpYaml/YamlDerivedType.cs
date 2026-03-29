// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

namespace SharpYaml;

/// <summary>
/// Represents a derived type mapping for runtime polymorphic configuration.
/// </summary>
public sealed class YamlDerivedType
{
    /// <summary>
    /// Initializes a new instance with no discriminator, marking this derived type
    /// as the default when no discriminator matches.
    /// </summary>
    /// <param name="derivedType">The derived CLR type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="derivedType"/> is <see langword="null"/>.</exception>
    public YamlDerivedType(Type derivedType)
    {
        ArgumentGuard.ThrowIfNull(derivedType);

        DerivedType = derivedType;
        Discriminator = null;
    }

    /// <summary>
    /// Initializes a new instance with a string discriminator.
    /// </summary>
    /// <param name="derivedType">The derived CLR type.</param>
    /// <param name="discriminator">The discriminator value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="derivedType"/> or <paramref name="discriminator"/> is <see langword="null"/>.</exception>
    public YamlDerivedType(Type derivedType, string discriminator)
    {
        ArgumentGuard.ThrowIfNull(derivedType);
        ArgumentGuard.ThrowIfNull(discriminator);

        DerivedType = derivedType;
        Discriminator = discriminator;
    }

    /// <summary>
    /// Initializes a new instance with an integer discriminator.
    /// </summary>
    /// <param name="derivedType">The derived CLR type.</param>
    /// <param name="discriminator">The integer discriminator value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="derivedType"/> is <see langword="null"/>.</exception>
    public YamlDerivedType(Type derivedType, int discriminator)
    {
        ArgumentGuard.ThrowIfNull(derivedType);

        DerivedType = derivedType;
        Discriminator = discriminator.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the derived CLR type.
    /// </summary>
    public Type DerivedType { get; }

    /// <summary>
    /// Gets the discriminator value, or <see langword="null"/> if this is the default derived type.
    /// </summary>
    public string? Discriminator { get; }

    /// <summary>
    /// Gets or sets an optional explicit YAML tag for the derived type.
    /// </summary>
    public string? Tag { get; init; }
}
