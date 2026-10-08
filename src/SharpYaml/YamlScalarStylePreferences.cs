// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml;

/// <summary>
/// Provides high-level preferences for scalar style emission.
/// </summary>
public sealed class YamlScalarStylePreferences
{
    /// <summary>
    /// Gets or sets a value indicating whether plain style should be preferred when possible.
    /// </summary>
    public bool PreferPlainStyle { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether quoted style should be preferred for ambiguous scalars.
    /// </summary>
    public bool PreferQuotedForAmbiguousScalars { get; init; } = true;

    /// <summary>
    /// Gets or sets the scalar style used for multiline string values written via
    /// <see cref="Serialization.YamlWriter.WriteString(string?)"/> (the default path for every <see cref="string"/>
    /// property/field during normal serialization).
    /// </summary>
    /// <remarks>
    /// Only <see cref="ScalarStyle.Any"/> (the default; multiline strings are double-quoted with <c>\n</c>
    /// escapes, matching prior behavior), <see cref="ScalarStyle.Literal"/>, and <see cref="ScalarStyle.Folded"/>
    /// are supported here. This setting has no effect on single-line strings, on scalars written via
    /// <see cref="Serialization.YamlWriter.WriteScalar(string?)"/>, or on an explicit style requested through
    /// <see cref="Serialization.YamlWriter.WriteScalar(string?, ScalarStyle)"/> from a custom converter, which
    /// always takes precedence.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Value is not <see cref="ScalarStyle.Any"/>, <see cref="ScalarStyle.Literal"/>, or <see cref="ScalarStyle.Folded"/>.</exception>
    public ScalarStyle MultilineStringStyle
    {
        get;
        init
        {
            if (value is not (ScalarStyle.Any or ScalarStyle.Literal or ScalarStyle.Folded))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"{nameof(MultilineStringStyle)} must be {ScalarStyle.Any}, {ScalarStyle.Literal}, or {ScalarStyle.Folded}."
                );
            }

            field = value;
        }
    } = ScalarStyle.Any;
}

