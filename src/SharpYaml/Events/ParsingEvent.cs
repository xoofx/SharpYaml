// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Events;

/// <summary>
/// Base class for parsing events.
/// </summary>
public abstract class ParsingEvent
{
    /// <summary>
    /// Gets a value indicating the variation of depth caused by this event.
    /// The value can be either -1, 0 or 1. For start events, it will be 1,
    /// for end events, it will be -1, and for the remaining events, it will be 0.
    /// </summary>
    public abstract int NestingIncrease { get; }

    /// <summary>
    /// Gets the event type, which allows for simpler type comparisons.
    /// </summary>
    internal abstract EventType Type { get; }

    /// <summary>
    /// Gets the position in the input stream where the event starts.
    /// </summary>
    public Mark Start { get; private set; }

    /// <summary>
    /// Gets the position in the input stream where the event ends.
    /// </summary>
    public Mark End { get; private set; }

    /// <summary>
    /// Whether this is an event a parser returns again for the next event of its type (see <see cref="IReusedEventSource"/>):
    /// its content is only valid until the parser moves, so whoever keeps it must keep the copy made by <see cref="Detach"/>.
    /// </summary>
    internal bool IsReused;

    /// <summary>Gives a reused event the locations of the event it now stands for.</summary>
    internal void Reset(Mark start, Mark end)
    {
        this.Start = start;
        this.End = end;
    }

    /// <summary>Returns this event, or a copy of it that stays valid when it is a reused event.</summary>
    internal ParsingEvent Detach()
    {
        if (!IsReused)
        {
            return this;
        }

        return this switch
        {
            Scalar scalar => new Scalar(scalar.Anchor, scalar.Tag, scalar.Value, scalar.Style, scalar.IsPlainImplicit, scalar.IsQuotedImplicit, Start, End),
            MappingStart mapping => new MappingStart(mapping.Anchor, mapping.Tag, mapping.IsImplicit, mapping.Style, Start, End),
            SequenceStart sequence => new SequenceStart(sequence.Anchor, sequence.Tag, sequence.IsImplicit, sequence.Style, Start, End),
            MappingEnd => new MappingEnd(Start, End),
            SequenceEnd => new SequenceEnd(Start, End),
            _ => throw new System.InvalidOperationException($"Unexpected reused event '{GetType().Name}'."),
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsingEvent"/> class.
    /// </summary>
    /// <param name="start">The start position of the event.</param>
    /// <param name="end">The end position of the event.</param>
    internal ParsingEvent(Mark start, Mark end)
    {
        this.Start = start;
        this.End = end;
    }
}