// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Events;

/// <summary>
/// A parser that can return the same event objects again instead of allocating one per event, for a consumer that
/// reads an event before it moves the parser and keeps none of them, as <see cref="Serialization.YamlReader"/>.
/// </summary>
internal interface IReusedEventSource
{
    /// <summary>
    /// Gets or sets a value indicating whether the scalar and collection events that have no anchor and no tag are one
    /// object per type, given again the content of each event. Such an event has <see cref="ParsingEvent.IsReused"/>
    /// set, and a consumer that has to keep one keeps its <see cref="ParsingEvent.Detach"/> copy.
    /// </summary>
    bool ReuseEvents { get; set; }
}
