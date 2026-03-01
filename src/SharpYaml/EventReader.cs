// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using SharpYaml.Events;
using Event = SharpYaml.Events.ParsingEvent;

namespace SharpYaml;

/// <summary>
/// Reads events from a sequence of <see cref="Event" />.
/// </summary>
public class EventReader
{
    private bool endOfStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventReader"/> class.
    /// </summary>
    /// <param name="parser">The parser that provides the events.</param>
    public EventReader(IParser parser)
    {
        this.Parser = parser;
        MoveNext();
    }

    /// <summary>
    /// Gets the underlying parser.
    /// </summary>
    /// <value>The parser.</value>
    public IParser Parser { get; }

    /// <summary>Gets or sets current Depth.</summary>
    public int CurrentDepth { get; private set; } = 0;

    /// <summary>
    /// Ensures that the current event is of the specified type, returns it and moves to the next event.
    /// </summary>
    /// <typeparam name="T">Type of the <see cref="Event"/>.</typeparam>
    /// <returns>Returns the current event.</returns>
    /// <exception cref="YamlException">If the current event is not of the specified type.</exception>
    public T Expect<T>() where T : Event
    {
        var yamlEvent = Allow<T>();
        if (yamlEvent == null)
        {
            throw new YamlException(
                Parser.Current.Start,
                Parser.Current.End,
                FormattableString.Invariant($"Expected '{typeof(T).Name}', got '{Parser.Current.GetType().Name}'."));
        }
        return yamlEvent;
    }

    /// <summary>
    /// Moves to the next event.
    /// </summary>
    private void MoveNext()
    {
        if (Parser.Current != null)
            CurrentDepth += Parser.Current.NestingIncrease;
        endOfStream = !Parser.MoveNext();
    }

    /// <summary>
    /// Checks whether the current event is of the specified type.
    /// </summary>
    /// <typeparam name="T">Type of the event.</typeparam>
    /// <returns>Returns true if the current event is of type <typeparamref name="T"/>. Otherwise returns false.</returns>
    public bool Accept<T>() where T : Event
    {
        EnsureNotAtEndOfStream();

        return Parser.Current is T;
    }

    /// <summary>
    /// Checks whether the current event is of the specified type.
    /// If the event is of the specified type, returns it and moves to the next event.
    /// Otherwise retruns null.
    /// </summary>
    /// <typeparam name="T">Type of the <see cref="Event"/>.</typeparam>
    /// <returns>Returns the current event if it is of type T; otherwise returns null.</returns>
    public T? Allow<T>() where T : Event
    {
        if (!Accept<T>())
        {
            return null;
        }
        var yamlEvent = (T?)Parser.Current;
        MoveNext();
        return yamlEvent;
    }

    /// <summary>
    /// Gets the next event without consuming it.
    /// </summary>
    /// <typeparam name="T">Type of the <see cref="Event"/>.</typeparam>
    /// <returns>Returns the current event if it is of type T; otherwise returns null.</returns>
    public T? Peek<T>() where T : Event
    {
        if (!Accept<T>())
        {
            return null;
        }
        var yamlEvent = (T?)Parser.Current;
        return yamlEvent;
    }

    /// <summary>
    /// Skips the current event and any "child" event.
    /// </summary>
    public void Skip()
    {
        int depth = 0;

        do
        {
            if (Accept<SequenceStart>() || Accept<MappingStart>() || Accept<StreamStart>() || Accept<DocumentStart>())
            {
                ++depth;
            }
            else if (Accept<SequenceEnd>() || Accept<MappingEnd>() || Accept<StreamEnd>() || Accept<DocumentEnd>())
            {
                --depth;
            }

            MoveNext();
        } while (depth > 0);
    }

    /// <summary>
    /// Skips until we reach the appropriate depth again
    /// </summary>
    public void Skip(int untilDepth)
    {
        do
        {
            MoveNext();
        } while (CurrentDepth > untilDepth);
    }

    /// <summary>
    /// Throws an exception if Ensures the not at end of stream.
    /// </summary>
    private void EnsureNotAtEndOfStream()
    {
        if (endOfStream)
        {
            throw new EndOfStreamException();
        }
    }
}