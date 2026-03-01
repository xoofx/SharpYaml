// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

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
}

