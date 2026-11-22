// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using SharpYaml.Events;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// Writes an editable tree and records where the text of each of its scalars and keys is written, so that
/// <see cref="Verify"/> can check that the text reads back as the tree: an edit must never change what an untouched
/// node is read as (a comment or a line that becomes part of a block scalar, a scalar line that becomes a key).
/// </summary>
internal sealed class YamlEditableTextWriter : StringWriter
{
    private readonly List<Span> _spans = [];

    /// <summary>
    /// Records that <paramref name="text"/>, the text of a scalar, a key or an empty flow collection, is written next.
    /// </summary>
    public void Record(string text)
    {
        var lead = 0;
        while (lead < text.Length && text[lead] is ' ' or '\t')
        {
            lead++;
        }

        if (lead == text.Length)
        {
            return;
        }

        var start = GetStringBuilder().Length + lead;
        _spans.Add(new Span(start, GetStringBuilder().Length + text.Length, text[lead] is '|' or '>', text[lead] is '[' or '{'));
    }

    /// <summary>
    /// Parses the text written so far and throws if a scalar or a key of the tree is not read exactly where it was
    /// written, or if the text holds a scalar or a key that the tree did not write.
    /// </summary>
    /// <exception cref="InvalidOperationException">The text does not read back as the tree.</exception>
    public void Verify()
    {
        var text = ToString();
        var byStart = new Dictionary<int, Span>();
        foreach (var span in _spans)
        {
            byStart[span.Start] = span;
        }

        var matched = new HashSet<int>();
        var parser = Parser.CreateParser(new StringReader(text));
        while (parser.MoveNext())
        {
            var current = parser.Current!;
            var isNode = current switch
            {
                Scalar scalar => scalar.End.Index > scalar.Start.Index,
                SequenceStart sequence => sequence.Style == YamlStyle.Flow,
                MappingStart mapping => mapping.Style == YamlStyle.Flow,
                _ => false,
            };

            if (!isNode)
            {
                continue;
            }

            var start = current.Start.Index;
            if (byStart.TryGetValue(start, out var span))
            {
                matched.Add(start);
                if (current is Scalar && !EndsAt(text, span, current.End.Index))
                {
                    throw Mismatch(current.Start);
                }
            }
            else if (!IsInFlowSpan(start))
            {
                throw Mismatch(current.Start);
            }
        }

        foreach (var span in _spans)
        {
            if (!matched.Contains(span.Start) && !IsInFlowSpan(span.Start))
            {
                throw new InvalidOperationException(
                    $"The edits cannot be applied: the text written at index {span.Start} would not be read as the node that wrote it.");
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if a scalar read from <paramref name="span"/> ends where it was written; a block
    /// scalar also takes the line breaks and the spaces after its last line, and nothing else.
    /// </summary>
    private static bool EndsAt(string text, Span span, int end)
    {
        if (!span.IsBlockScalar)
        {
            return end == span.End;
        }

        if (end < span.End)
        {
            return false;
        }

        for (var index = span.End; index < end && index < text.Length; index++)
        {
            if (text[index] is not (' ' or '\t' or '\r' or '\n'))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsInFlowSpan(int index)
    {
        foreach (var span in _spans)
        {
            if (span.IsFlow && index > span.Start && index < span.End)
            {
                return true;
            }
        }

        return false;
    }

    private static InvalidOperationException Mismatch(Mark mark) => new(
        $"The edits cannot be applied: the edited text would be read differently from the edited tree at line {mark.Line + 1}, column {mark.Column + 1} " +
        "(for example, a comment or a line would become part of a block scalar).");

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
