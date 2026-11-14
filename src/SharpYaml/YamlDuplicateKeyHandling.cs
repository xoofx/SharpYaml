// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Defines how repeated keys in a YAML mapping are handled when reading YAML.
/// </summary>
/// <remarks>
/// Existing entries in a dictionary being populated are initial values, not duplicate keys. The first occurrence of a
/// key in the YAML mapping can replace its existing value; this policy applies to subsequent occurrences in that mapping.
/// Custom dictionaries whose comparer is not supported retain their own key-existence checks, so pre-existing keys also
/// count as seen for those implementations.
/// </remarks>
public enum YamlDuplicateKeyHandling
{
    /// <summary>
    /// Throw an exception when a duplicate key is found.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Keep the last value for a duplicate key.
    /// </summary>
    LastWins = 1,

    /// <summary>
    /// Keep the first value for a duplicate key.
    /// </summary>
    FirstWins = 2,
}

