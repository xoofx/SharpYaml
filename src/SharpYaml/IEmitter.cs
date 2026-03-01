// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using SharpYaml.Events;

namespace SharpYaml;

/// <summary>
/// Represents a YAML stream emitter.
/// </summary>
public interface IEmitter
{
    /// <summary>
    /// Emits an event.
    /// </summary>
    void Emit(ParsingEvent @event);
}