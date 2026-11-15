// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Events;

/// <summary>
/// What a parser offers to the one consumer that owns it, as <see cref="Serialization.YamlReader"/>: the same event
/// objects again instead of one per event, for a consumer that reads an event before it moves the parser and keeps
/// none of them, and its pooled input back when the consumer is done.
/// </summary>
internal interface IReusedEventSource
{
    /// <summary>
    /// Gets or sets a value indicating whether the scalar and collection events that have no anchor and no tag are one
    /// object per type, given again the content of each event. Such an event has <see cref="ParsingEvent.IsReused"/>
    /// set, and a consumer that has to keep one keeps its <see cref="ParsingEvent.Detach"/> copy.
    /// </summary>
    bool ReuseEvents { get; set; }

    /// <summary>
    /// Returns what the parser took from a pool to read its input. The parser does it itself at the end of the
    /// stream; its owner calls this when it stops before that, after an error or after the document it wanted. The
    /// parser must not be moved afterwards.
    /// </summary>
    void ReleaseInput();
}
