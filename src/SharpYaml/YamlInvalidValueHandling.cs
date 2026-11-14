// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml;

/// <summary>
/// Defines what happens when a converter rejects a value during deserialization: the value of a member or constructor
/// argument, a collection element or the value of a dictionary entry.
/// </summary>
/// <remarks>
/// A value is recoverable when the converter throws a <see cref="YamlException"/> before consuming any of it, for example
/// through <see cref="Serialization.YamlReader.CreateException"/>. The value is then skipped: the member or argument
/// keeps its default, and the collection element or dictionary entry is dropped. Any other exception, and any exception
/// thrown after part of the value was read, fails the deserialization whatever the handling.
/// </remarks>
public enum YamlInvalidValueHandling
{
    /// <summary>
    /// Throw at the first invalid value.
    /// </summary>
    FailFast = 0,

    /// <summary>
    /// Recover from every recoverable invalid value, finish reading, then throw a <see cref="YamlInvalidValuesException"/>
    /// listing all of them.
    /// </summary>
    FailAtEnd = 1,

    /// <summary>
    /// Recover from every recoverable invalid value and let the deserialization succeed.
    /// </summary>
    Skip = 2,
}
