// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace SharpYaml.Serialization;

/// <summary>
/// Specifies a custom <see cref="YamlConverter"/> to use when serializing or deserializing a member or type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class YamlConverterAttribute : YamlAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlConverterAttribute"/> class.
    /// </summary>
    /// <param name="converterType">The converter type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="converterType"/> is <see langword="null"/>.</exception>
#if NETSTANDARD2_0
    public YamlConverterAttribute(Type converterType)
#else
    public YamlConverterAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type converterType)
#endif
    {
        ArgumentGuard.ThrowIfNull(converterType);
        ConverterType = converterType;
    }

    /// <summary>
    /// Gets the converter type.
    /// </summary>
#if !NETSTANDARD2_0
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
    public Type ConverterType { get; }
}
