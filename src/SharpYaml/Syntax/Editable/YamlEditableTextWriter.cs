// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using SharpYaml.Events;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// Writes an editable tree and records where the text of each of its scalars, keys and empty flow collections is
/// written, so that <see cref="Verify"/> can check that the text reads back as the tree: an edit must never change what
/// an untouched node is read as (a comment or a line that becomes part of a block scalar, a scalar line that becomes a
/// key, an entry read at another depth).
/// </summary>
internal sealed class YamlEditableTextWriter : StringWriter
{
    // The nodes do not override Equals: they are compared by reference.
    private readonly Dictionary<YamlEditableNode, Span> _spans = [];
    private readonly Dictionary<YamlEditableMapEntry, Span> _keys = [];

    /// <summary>
    /// Records that <paramref name="text"/>, the text of <paramref name="node"/> (a scalar, or an empty map or list in
    /// flow style), is written next.
    /// </summary>
    public void Record(YamlEditableNode node, string text)
    {
        if (CreateSpan(text) is { } span)
        {
            _spans[node] = span;
        }
    }

    /// <summary>
    /// Records that the key of <paramref name="entry"/> is written next.
    /// </summary>
    public void RecordKey(YamlEditableMapEntry entry)
    {
        if (CreateSpan(entry.Key) is { } span)
        {
            _keys[entry] = span;
        }
    }

    private Span? CreateSpan(string text)
    {
        // The spaces and line breaks around a scalar separate it from what is before and after it; the lines of a
        // block scalar are all its own.
        var lead = 0;
        while (lead < text.Length && text[lead] is ' ' or '\t' or '\r' or '\n')
        {
            lead++;
        }

        if (lead == text.Length)
        {
            return null;
        }

        var isBlockScalar = text[lead] is '|' or '>';
        var end = text.Length;
        while (!isBlockScalar && end > lead && text[end - 1] is ' ' or '\t' or '\r' or '\n')
        {
            end--;
        }

        var position = GetStringBuilder().Length;
        return new Span(position + lead, position + end, isBlockScalar, text[lead] is '[' or '{');
    }

    /// <summary>
    /// Parses the text written for <paramref name="root"/> and throws unless it reads back as the same tree: the same
    /// maps, lists and scalars, nested the same way, each scalar and key read exactly from the text written for it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The text does not read back as the tree.</exception>
    public void Verify(YamlEditableNode root)
    {
        var events = new List<ParsingEvent>();
        var parser = Parser.CreateParser(new StringReader(ToString()));
        while (parser.MoveNext())
        {
            events.Add(parser.Current!);
        }

        var reader = new EventReader(events, ToString());
        reader.Expect<StreamStart>();
        if (root is YamlEditableMap { Entries.Count: 0, EmptyFlowText: null })
        {
            // A document without content.
            reader.Expect<StreamEnd>();
            return;
        }

        reader.Expect<DocumentStart>();
        ExpectNode(reader, root);
        reader.Expect<DocumentEnd>();
        reader.Expect<StreamEnd>();
    }

    private void ExpectNode(EventReader reader, YamlEditableNode node)
    {
        switch (node)
        {
            case YamlEditableScalar scalar when !_spans.ContainsKey(scalar):
                // A key or a list item without a value is read as an empty scalar.
                reader.ExpectEmptyScalar();
                break;
            case YamlEditableScalar scalar:
                // A scalar must be read as a scalar: raw text with a flow collection is a sequence or a mapping.
                reader.ExpectWritten(_spans[scalar], asScalar: true);
                break;
            case YamlEditableMap { Entries.Count: 0 } map:
                reader.ExpectWritten(_spans[map], asScalar: false);
                break;
            case YamlEditableList { Items.Count: 0 } list:
                reader.ExpectWritten(_spans[list], asScalar: false);
                break;
            case YamlEditableMap map:
                reader.Expect<MappingStart>();
                foreach (var entry in map.Entries)
                {
                    if (_keys.TryGetValue(entry, out var key))
                    {
                        reader.ExpectWritten(key, asScalar: true);
                    }
                    else
                    {
                        reader.ExpectEmptyScalar();
                    }

                    ExpectNode(reader, entry.Value);
                }

                reader.Expect<MappingEnd>();
                break;
            case YamlEditableList list:
                reader.Expect<SequenceStart>();
                foreach (var item in list.Items)
                {
                    ExpectNode(reader, item.Value);
                }

                reader.Expect<SequenceEnd>();
                break;
        }
    }

    private sealed class EventReader
    {
        private readonly List<ParsingEvent> _events;
        private readonly string _text;
        private int _index;

        public EventReader(List<ParsingEvent> events, string text)
        {
            _events = events;
            _text = text;
        }

        public void Expect<TEvent>() where TEvent : ParsingEvent
        {
            if (Next() is not TEvent)
            {
                throw Mismatch(_events[_index - 1].Start);
            }
        }

        public void ExpectEmptyScalar()
        {
            if (Next() is not Scalar { Value.Length: 0 })
            {
                throw Mismatch(_events[_index - 1].Start);
            }
        }

        /// <summary>
        /// Reads the node written as <paramref name="span"/>: a scalar read from exactly that text (a block scalar also
        /// takes the line breaks after its last line, and nothing else), or an empty flow collection that starts and ends
        /// there.
        /// </summary>
        public void ExpectWritten(Span span, bool asScalar)
        {
            var current = Next();
            if (current.Start.Index != span.Start)
            {
                throw Mismatch(current.Start);
            }

            if (!asScalar && current is SequenceStart or MappingStart)
            {
                var depth = 1;
                while (depth > 0)
                {
                    var inner = Next();
                    depth += inner.NestingIncrease;
                    current = inner;
                }

                // The end of a flow collection is read at its closing bracket.
                if (current.End.Index != span.End && current.End.Index != span.End - 1)
                {
                    throw Mismatch(current.Start);
                }

                return;
            }

            if (current is not Scalar scalar || !EndsAt(scalar.End.Index, span))
            {
                throw Mismatch(current.Start);
            }
        }

        private bool EndsAt(int end, Span span)
        {
            if (!span.IsBlockScalar)
            {
                return end == span.End;
            }

            if (end < span.End)
            {
                return false;
            }

            // A line with spaces after the last line of a block scalar can be part of its value.
            for (var index = span.End; index < end && index < _text.Length; index++)
            {
                if (_text[index] is not ('\r' or '\n'))
                {
                    return false;
                }
            }

            return true;
        }

        private ParsingEvent Next()
        {
            if (_index >= _events.Count)
            {
                throw new InvalidOperationException("The edits cannot be applied: the edited text would be read differently from the edited tree.");
            }

            return _events[_index++];
        }
    }

    private static InvalidOperationException Mismatch(Mark mark) => new(
        $"The edits cannot be applied: the edited text would be read differently from the edited tree at line {mark.Line + 1}, column {mark.Column + 1} " +
        "(for example, a comment or a line would become part of a block scalar, or an entry would be read at another depth).");

    private readonly struct Span
    {
        public Span(int start, int end, bool isBlockScalar, bool isFlow)
        {
            Start = start;
            End = end;
            IsBlockScalar = isBlockScalar;
            IsFlow = isFlow;
        }

        public int Start { get; }

        public int End { get; }

        public bool IsBlockScalar { get; }

        public bool IsFlow { get; }
    }
}
