﻿// Copyright (c) 2015 SharpYaml - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// -------------------------------------------------------------------------------
// SharpYaml is a fork of YamlDotNet https://github.com/aaubry/YamlDotNet
// published with the following license:
// -------------------------------------------------------------------------------
// 
// Copyright (c) 2008, 2009, 2010, 2011, 2012 Antoine Aubry
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SharpYaml.Events;
using TagDirective = SharpYaml.Tokens.TagDirective;
using VersionDirective = SharpYaml.Tokens.VersionDirective;

namespace SharpYaml
{
    /// <summary>
    /// Emits YAML streams.
    /// </summary>
    public class Emitter : IEmitter
    {
        private readonly TextWriter output;

        private readonly bool isCanonical;
        private readonly int bestIndent;
        private readonly int bestWidth;
        private EmitterState state;

        private readonly Stack<EmitterState> states = new Stack<EmitterState>();
        private readonly Queue<ParsingEvent> events = new Queue<ParsingEvent>();
        private readonly Stack<int> indents = new Stack<int>();
        private readonly TagDirectiveCollection tagDirectives = new TagDirectiveCollection();
        private int indent;
        private int flowLevel;
        private bool isMappingContext;
        private bool isSimpleKeyContext;
        private bool isRootContext;

        private int column;
        private bool isWhitespace;
        private bool isIndentation;
        private readonly bool emitKeyQuoted;

        private bool isOpenEnded;

        private readonly MutableStringLookAheadBuffer buffer = new MutableStringLookAheadBuffer();


        private class MutableStringLookAheadBuffer : ILookAheadBuffer
        {
            private string? value;

            public string? Value
            {
                get { return value; }
                set
                {
                    this.value = value;
                    Position = 0;
                }
            }

            public int Length { get { return value?.Length ?? 0; } }

            public int Position { get; private set; }

            public bool IsOutside(int index)
            {
                return value == null || index >= value.Length;
            }

            public bool EndOfInput { get { return IsOutside(Position); } }

            public MutableStringLookAheadBuffer() { }

            public char Peek(int offset)
            {
                int index = Position + offset;
                return value[index];
            }

            public void Skip(int length)
            {
                if (length < 0)
                {
                    throw new ArgumentOutOfRangeException("length", "The length must be positive.");
                }
                Position += length;
            }

            public void Cache(int length) { }
        }

        private struct AnchorData
        {
            public string? anchor;
            public bool isAlias;
        }

        private AnchorData anchorData;

        private struct TagData
        {
            public string? handle;
            public string? suffix;
        }

        private TagData tagData;

        private struct ScalarData
        {
            public string value;
            public bool isMultiline;
            public bool isFlowPlainAllowed;
            public bool isBlockPlainAllowed;
            public bool isSingleQuotedAllowed;
            public bool isBlockAllowed;
            public ScalarStyle style;
        }

        private readonly bool isUnicode;

        private ScalarData scalarData;

        internal const int MinBestIndent = 2;
        internal const int MaxBestIndent = 9;

        /// <summary>
        /// Initializes a new instance of the <see cref="IEmitter" /> class.
        /// </summary>
        /// <param name="output">The <see cref="TextWriter" /> where the emitter will write.</param>
        /// <param name="bestIndent">The preferred indentation.</param>
        /// <param name="bestWidth">The preferred text width.</param>
        /// <param name="isCanonical">If true, write the output in canonical form.</param>
        /// <param name="forceIndentLess">if set to <c>true</c> [always indent].</param>
        /// <param name="emitKeyQuoted">if set to <c>true</c> always emit keys double quoted.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// bestIndent
        /// or
        /// bestWidth;The bestWidth parameter must be greater than bestIndent * 2.
        /// </exception>
        public Emitter(TextWriter output, int bestIndent = MinBestIndent, int bestWidth = int.MaxValue, bool isCanonical = false, bool forceIndentLess = false, bool emitKeyQuoted = false)
        {
            if (bestIndent < MinBestIndent || bestIndent > MaxBestIndent)
            {
                throw new ArgumentOutOfRangeException("bestIndent", FormattableString.Invariant($"The bestIndent parameter must be between {MinBestIndent} and {MaxBestIndent}."));
            }

            this.bestIndent = bestIndent;

            if (bestWidth <= bestIndent * 2)
            {
                throw new ArgumentOutOfRangeException("bestWidth", "The bestWidth parameter must be greater than bestIndent * 2.");
            }

            this.bestWidth = bestWidth;

            this.isCanonical = isCanonical;
            this.ForceIndentLess = forceIndentLess;
            this.emitKeyQuoted = emitKeyQuoted;

            this.output = output;
            this.isUnicode = output.Encoding.WebName.StartsWith("utf", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [always indent].
        /// </summary>
        /// <value><c>true</c> if [always indent]; otherwise, <c>false</c>.</value>
        public bool ForceIndentLess { get; set; }


        private void Write(char value)
        {
            output.Write(value);
            ++column;
        }

        private void Write(string value)
        {
            output.Write(value);
            column += value.Length;
        }

        private void WriteBreak()
        {
            output.WriteLine();
            column = 0;
        }

        /// <summary>
        /// Emit an evt.
        /// </summary>
        public void Emit(ParsingEvent @event)
        {
            events.Enqueue(@event);

            while (!NeedMoreEvents())
            {
                var current = events.Peek();
                AnalyzeEvent(current);
                StateMachine(current);

                // Only dequeue after calling state_machine because it checks how many events are in the queue.
                events.Dequeue();
            }
        }

        /// <summary>
        /// Check if we need to accumulate more events before emitting.
        /// 
        /// We accumulate extra
        ///  - 1 event for DOCUMENT-START
        ///  - 2 events for SEQUENCE-START
        ///  - 3 events for MAPPING-START
        /// </summary>
        private bool NeedMoreEvents()
        {
            if (events.Count == 0)
            {
                return true;
            }

            int accumulate;
            switch (events.Peek().Type)
            {
                case EventType.YAML_DOCUMENT_START_EVENT:
                    accumulate = 1;
                    break;

                case EventType.YAML_SEQUENCE_START_EVENT:
                    accumulate = 2;
                    break;

                case EventType.YAML_MAPPING_START_EVENT:
                    accumulate = 3;
                    break;

                default:
                    return false;
            }

            if (events.Count > accumulate)
            {
                return false;
            }

            int level = 0;
            foreach (var evt in events)
            {
                switch (evt.Type)
                {
                    case EventType.YAML_DOCUMENT_START_EVENT:
                    case EventType.YAML_SEQUENCE_START_EVENT:
                    case EventType.YAML_MAPPING_START_EVENT:
                        ++level;
                        break;

                    case EventType.YAML_DOCUMENT_END_EVENT:
                    case EventType.YAML_SEQUENCE_END_EVENT:
                    case EventType.YAML_MAPPING_END_EVENT:
                        --level;
                        break;
                }
                if (level == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void AnalyzeAnchor(string? anchor, bool isAlias)
        {
            anchorData.anchor = anchor;
            anchorData.isAlias = isAlias;
        }

        /// <summary>
        /// Check if the evt data is valid.
        /// </summary>
        private void AnalyzeEvent(ParsingEvent evt)
        {
            anchorData.anchor = null;
            tagData.handle = null;
            tagData.suffix = null;

            if (evt is AnchorAlias alias)
            {
                AnalyzeAnchor(alias.Value, true);
                return;
            }

            if (evt is NodeEvent nodeEvent)
            {
                if (evt is Scalar scalar)
                {
                    AnalyzeScalar(scalar.Value);
                }

                AnalyzeAnchor(nodeEvent.Anchor, false);

                if (!string.IsNullOrEmpty(nodeEvent.Tag) && (isCanonical || nodeEvent.IsCanonical))
                {
                    AnalyzeTag(nodeEvent.Tag);
                }
                return;
            }
        }

        /// <summary>
        /// Check if a scalar is valid.
        /// </summary>
        private void AnalyzeScalar(string value)
        {
            bool block_indicators = false;
            bool flow_indicators = false;
            bool line_breaks = false;
            bool special_characters = false;

            bool leading_space = false;
            bool leading_break = false;
            bool trailing_space = false;
            bool trailing_break = false;
            bool break_space = false;
            bool space_break = false;

            bool previous_space = false;
            bool previous_break = false;

            scalarData.value = value;

            if (value.Length == 0)
            {
                scalarData.isMultiline = false;
                scalarData.isFlowPlainAllowed = false;
                scalarData.isBlockPlainAllowed = true;
                scalarData.isSingleQuotedAllowed = true;
                scalarData.isBlockAllowed = false;
                return;
            }

            if (value.StartsWith("---", StringComparison.Ordinal) || value.StartsWith("...", StringComparison.Ordinal))
            {
                block_indicators = true;
                flow_indicators = true;
            }

            bool preceeded_by_whitespace = true;

            buffer.Value = value;
            bool followed_by_whitespace = buffer.IsOutside(buffer.Position + 1) || buffer.IsBlankOrBreakOrZero(1);

            // If the output is not detected as unicode, check if the value to encode contains 
            // special characters that would require special encoding
            if (!isUnicode)
            {
                try
                {
                    var encodedBytes = output.Encoding.GetBytes(value);
                    var decodedString = output.Encoding.GetString(encodedBytes, 0, encodedBytes.Length);
                    special_characters = decodedString != value;
                }
                catch (EncoderFallbackException)
                {
                    special_characters = true;
                }
            }

            bool isFirst = true;
            while (!buffer.EndOfInput)
            {
                if (isFirst)
                {
                    if (buffer.Check(@"#,[]{}&*!|>\""%@`"))
                    {
                        flow_indicators = true;
                        block_indicators = true;
                    }

                    if (buffer.Check("?:"))
                    {
                        flow_indicators = true;
                        if (followed_by_whitespace)
                        {
                            block_indicators = true;
                        }
                    }

                    if (buffer.Check('-') && followed_by_whitespace)
                    {
                        flow_indicators = true;
                        block_indicators = true;
                    }
                }
                else
                {
                    if (buffer.Check(",?[]{}"))
                    {
                        flow_indicators = true;
                    }

                    if (buffer.Check(':'))
                    {
                        flow_indicators = true;
                        if (followed_by_whitespace)
                        {
                            block_indicators = true;
                        }
                    }

                    if (buffer.Check('#') && preceeded_by_whitespace)
                    {
                        flow_indicators = true;
                        block_indicators = true;
                    }
                }

                if (!special_characters && !buffer.IsPrintable())
                {
                    special_characters = true;
                }

                if (buffer.IsBreak())
                {
                    line_breaks = true;
                }

                if (buffer.IsSpace())
                {
                    if (isFirst)
                    {
                        leading_space = true;
                    }
                    if (buffer.Position >= buffer.Length - 1)
                    {
                        trailing_space = true;
                    }
                    if (previous_break)
                    {
                        break_space = true;
                    }

                    previous_space = true;
                    previous_break = false;
                }

                else if (buffer.IsBreak())
                {
                    if (isFirst)
                    {
                        leading_break = true;
                    }
                    if (buffer.Position >= buffer.Length - 1)
                    {
                        trailing_break = true;
                    }

                    if (previous_space)
                    {
                        space_break = true;
                    }
                    previous_space = false;
                    previous_break = true;
                }
                else
                {
                    previous_space = false;
                    previous_break = false;
                }

                preceeded_by_whitespace = buffer.IsBlankOrBreakOrZero();
                buffer.Skip(1);
                if (!buffer.EndOfInput)
                {
                    followed_by_whitespace = buffer.IsOutside(buffer.Position + 1) || buffer.IsBlankOrBreakOrZero(1);
                }
                isFirst = false;
            }

            scalarData.isMultiline = line_breaks;

            scalarData.isFlowPlainAllowed = true;
            scalarData.isBlockPlainAllowed = true;
            scalarData.isSingleQuotedAllowed = true;
            scalarData.isBlockAllowed = true;

            if (leading_space || leading_break || trailing_space || trailing_break)
            {
                scalarData.isFlowPlainAllowed = false;
                scalarData.isBlockPlainAllowed = false;
            }

            if (trailing_space)
            {
                scalarData.isBlockAllowed = false;
            }

            if (break_space)
            {
                scalarData.isFlowPlainAllowed = false;
                scalarData.isBlockPlainAllowed = false;
                scalarData.isSingleQuotedAllowed = false;
            }

            if (space_break)
            {
                scalarData.isFlowPlainAllowed = false;
                scalarData.isBlockPlainAllowed = false;
                scalarData.isSingleQuotedAllowed = false;
                scalarData.isBlockAllowed = false;
            }

            if (special_characters)
            {
                scalarData.isFlowPlainAllowed = false;
                scalarData.isBlockPlainAllowed = false;
                scalarData.isSingleQuotedAllowed = false;
                // Don't disable block scalars for line breaks - they're the point of folded/literal scalars
                // However, disable block scalars for single-character strings containing only special characters
                // as they're better represented with quoted styles
                if (!line_breaks || (line_breaks && value.Length == 1))
                {
                    scalarData.isBlockAllowed = false;
                }
            }

            if (line_breaks)
            {
                scalarData.isFlowPlainAllowed = false;
                scalarData.isBlockPlainAllowed = false;
            }

            if (flow_indicators)
            {
                scalarData.isFlowPlainAllowed = false;
            }

            if (block_indicators)
            {
                scalarData.isBlockPlainAllowed = false;
            }
        }

        /// <summary>
        /// Check if a tag is valid.
        /// </summary>
        private void AnalyzeTag(string tag)
        {
            tagData.handle = tag;
            foreach (var tagDirective in tagDirectives)
            {
                if (tag.StartsWith(tagDirective.Prefix, StringComparison.Ordinal))
                {
                    tagData.handle = tagDirective.Handle;
                    tagData.suffix = tag.Substring(tagDirective.Prefix.Length);
                    break;
                }
            }
        }

        /// <summary>
        /// State dispatcher.
        /// </summary>
        private void StateMachine(ParsingEvent evt)
        {
            switch (state)
            {
                case EmitterState.YAML_EMIT_STREAM_START_STATE:
                    EmitStreamStart(evt);
                    break;

                case EmitterState.YAML_EMIT_FIRST_DOCUMENT_START_STATE:
                    EmitDocumentStart(evt, true);
                    break;

                case EmitterState.YAML_EMIT_DOCUMENT_START_STATE:
                    EmitDocumentStart(evt, false);
                    break;

                case EmitterState.YAML_EMIT_DOCUMENT_CONTENT_STATE:
                    EmitDocumentContent(evt);
                    break;

                case EmitterState.YAML_EMIT_DOCUMENT_END_STATE:
                    EmitDocumentEnd(evt);
                    break;

                case EmitterState.YAML_EMIT_FLOW_SEQUENCE_FIRST_ITEM_STATE:
                    EmitFlowSequenceItem(evt, true);
                    break;

                case EmitterState.YAML_EMIT_FLOW_SEQUENCE_ITEM_STATE:
                    EmitFlowSequenceItem(evt, false);
                    break;

                case EmitterState.YAML_EMIT_FLOW_MAPPING_FIRST_KEY_STATE:
                    EmitFlowMappingKey(evt, true);
                    break;

                case EmitterState.YAML_EMIT_FLOW_MAPPING_KEY_STATE:
                    EmitFlowMappingKey(evt, false);
                    break;

                case EmitterState.YAML_EMIT_FLOW_MAPPING_SIMPLE_VALUE_STATE:
                    EmitFlowMappingValue(evt, true);
                    break;

                case EmitterState.YAML_EMIT_FLOW_MAPPING_VALUE_STATE:
                    EmitFlowMappingValue(evt, false);
                    break;

                case EmitterState.YAML_EMIT_BLOCK_SEQUENCE_FIRST_ITEM_STATE:
                    EmitBlockSequenceItem(evt, true);
                    break;

                case EmitterState.YAML_EMIT_BLOCK_SEQUENCE_ITEM_STATE:
                    EmitBlockSequenceItem(evt, false);
                    break;

                case EmitterState.YAML_EMIT_BLOCK_MAPPING_FIRST_KEY_STATE:
                    EmitBlockMappingKey(evt, true);
                    break;

                case EmitterState.YAML_EMIT_BLOCK_MAPPING_KEY_STATE:
                    EmitBlockMappingKey(evt, false);
                    break;

                case EmitterState.YAML_EMIT_BLOCK_MAPPING_SIMPLE_VALUE_STATE:
                    EmitBlockMappingValue(evt, true);
                    break;

                case EmitterState.YAML_EMIT_BLOCK_MAPPING_VALUE_STATE:
                    EmitBlockMappingValue(evt, false);
                    break;

                case EmitterState.YAML_EMIT_END_STATE:
                    throw new YamlException("Expected nothing after STREAM-END");

                default:
                    Debug.Assert(false, "Invalid state.");
                    throw new InvalidOperationException("Invalid state");
            }
        }

        /// <summary>
        /// Expect STREAM-START.
        /// </summary>
        private void EmitStreamStart(ParsingEvent evt)
        {
            if (evt is not StreamStart)
            {
                throw new ArgumentException("Expected STREAM-START.", "evt");
            }

            indent = -1;
            column = 0;
            isWhitespace = true;
            isIndentation = true;

            state = EmitterState.YAML_EMIT_FIRST_DOCUMENT_START_STATE;
        }

        /// <summary>
        /// Expect DOCUMENT-START or STREAM-END.
        /// </summary>
        private void EmitDocumentStart(ParsingEvent evt, bool isFirst)
        {
            if (evt is DocumentStart documentStart)
            {
                bool isImplicit = documentStart.IsImplicit && isFirst && !isCanonical;


                if (documentStart.Version != null && isOpenEnded)
                {
                    WriteIndicator("...", true, false, false);
                    WriteIndent();
                }

                if (documentStart.Version != null)
                {
                    AnalyzeVersionDirective(documentStart.Version);

                    isImplicit = false;
                    WriteIndicator("%YAML", true, false, false);
                    WriteIndicator(FormattableString.Invariant($"{Constants.MajorVersion}.{Constants.MinorVersion}"), true, false, false);
                    WriteIndent();
                }

                if (documentStart.Tags != null)
                {
                    foreach (var tagDirective in documentStart.Tags)
                    {
                        AppendTagDirective(tagDirective, false);
                    }
                }

                foreach (var tagDirective in Constants.DefaultTagDirectives)
                {
                    AppendTagDirective(tagDirective, true);
                }

                if (documentStart.Tags != null && documentStart.Tags.Count != 0)
                {
                    isImplicit = false;
                    foreach (var tagDirective in documentStart.Tags)
                    {
                        WriteIndicator("%TAG", true, false, false);
                        WriteTagHandle(tagDirective.Handle);
                        WriteTagContent(tagDirective.Prefix, true);
                        WriteIndent();
                    }
                }

                if (CheckEmptyDocument())
                {
                    isImplicit = false;
                }

                if (!isImplicit)
                {
                    WriteIndent();
                    WriteIndicator("---", true, false, false);
                    if (isCanonical)
                    {
                        WriteIndent();
                    }
                }

                state = EmitterState.YAML_EMIT_DOCUMENT_CONTENT_STATE;
            }

            else if (evt is StreamEnd)
            {
                if (isOpenEnded)
                {
                    WriteIndicator("...", true, false, false);
                    WriteIndent();
                }

                state = EmitterState.YAML_EMIT_END_STATE;
            }
            else
            {
                throw new YamlException("Expected DOCUMENT-START or STREAM-END");
            }
        }

        /// <summary>
        /// Check if the document content is an empty scalar.
        /// </summary>
        private bool CheckEmptyDocument()
        {
            int index = 0;
            foreach (var parsingEvent in events)
            {
                if (++index == 2)
                {
                    if (parsingEvent is Scalar scalar)
                    {
                        return string.IsNullOrEmpty(scalar.Value);
                    }
                    break;
                }
            }

            return false;
        }

        private void WriteTagHandle(string value)
        {
            if (!isWhitespace)
            {
                Write(' ');
            }

            Write(value);

            isWhitespace = false;
            isIndentation = false;
        }

        private static readonly Regex uriReplacer = new Regex(@"[^0-9A-Za-z_\-;?@=$~\\\)\]/:&+,\.\*\(\[!]", RegexOptions.Singleline | RegexOptions.Compiled);

        private static string UrlEncode(string text)
        {
            return uriReplacer.Replace(text, delegate (Match match)
            {
                var buffer = new StringBuilder();
                foreach (var toEncode in Encoding.UTF8.GetBytes(match.Value))
                {
                    buffer.AppendFormat("%{0:X02}", toEncode);
                }
                return buffer.ToString();
            });
        }

        private void WriteTagContent(string value, bool needsWhitespace)
        {
            if (needsWhitespace && !isWhitespace)
            {
                Write(' ');
            }

            Write(UrlEncode(value));

            isWhitespace = false;
            isIndentation = false;
        }

        /// <summary>
        /// Append a directive to the directives stack.
        /// </summary>
        private void AppendTagDirective(TagDirective value, bool allowDuplicates)
        {
            if (tagDirectives.Contains(value))
            {
                if (allowDuplicates)
                {
                    return;
                }
                else
                {
                    throw new YamlException("Duplicate %TAG directive.");
                }
            }
            else
            {
                tagDirectives.Add(value);
            }
        }

        /// <summary>
        /// Check if a %YAML directive is valid.
        /// </summary>
        private static void AnalyzeVersionDirective(VersionDirective versionDirective)
        {
            if (versionDirective.Version.Major != Constants.MajorVersion || versionDirective.Version.Minor != Constants.MinorVersion)
            {
                throw new YamlException("Incompatible %YAML directive");
            }
        }

        private void WriteIndicator(string indicator, bool needWhitespace, bool whitespace, bool indentation)
        {
            if (needWhitespace && !isWhitespace)
            {
                Write(' ');
            }

            Write(indicator);

            isWhitespace = whitespace;
            isIndentation &= indentation;
            isOpenEnded = false;
        }

        private void WriteIndent()
        {
            int currentIndent = Math.Max(indent, 0);

            if (!isIndentation || column > currentIndent || (column == currentIndent && !isWhitespace))
            {
                WriteBreak();
            }

            while (column < currentIndent)
            {
                Write(' ');
            }

            isWhitespace = true;
            isIndentation = true;
        }

        /// <summary>
        /// Expect the root node.
        /// </summary>
        private void EmitDocumentContent(ParsingEvent evt)
        {
            states.Push(EmitterState.YAML_EMIT_DOCUMENT_END_STATE);
            EmitNode(evt, true, false, false);
        }

        /// <summary>
        /// Expect a node.
        /// </summary>
        private void EmitNode(ParsingEvent evt, bool isRoot, bool isMapping, bool isSimpleKey)
        {
            isRootContext = isRoot;
            isMappingContext = isMapping;
            isSimpleKeyContext = isSimpleKey;

            var eventType = evt.Type;
            switch (eventType)
            {
                case EventType.YAML_ALIAS_EVENT:
                    EmitAlias();
                    break;

                case EventType.YAML_SCALAR_EVENT:
                    EmitScalar(evt);
                    break;

                case EventType.YAML_SEQUENCE_START_EVENT:
                    EmitSequenceStart(evt);
                    break;

                case EventType.YAML_MAPPING_START_EVENT:
                    EmitMappingStart(evt);
                    break;

                default:
                    throw new YamlException($"Expected SCALAR, SEQUENCE-START, MAPPING-START, or ALIAS, got {eventType}");
            }
        }

        /// <summary>
        /// Expect SEQUENCE-START.
        /// </summary>
        private void EmitSequenceStart(ParsingEvent evt)
        {
            ProcessAnchor();
            ProcessTag();

            var sequenceStart = (SequenceStart)evt;

            if (flowLevel != 0 || isCanonical || sequenceStart.Style == YamlStyle.Flow || CheckEmptySequence())
            {
                state = EmitterState.YAML_EMIT_FLOW_SEQUENCE_FIRST_ITEM_STATE;
            }
            else
            {
                state = EmitterState.YAML_EMIT_BLOCK_SEQUENCE_FIRST_ITEM_STATE;
            }
        }

        /// <summary>
        /// Check if the next events represent an empty sequence.
        /// </summary>
        private bool CheckEmptySequence()
        {
            if (events.Count < 2)
            {
                return false;
            }

            var eventList = new FakeList<ParsingEvent>(events);
            return eventList[0] is SequenceStart && eventList[1] is SequenceEnd;
        }

        /// <summary>
        /// Check if the next events represent an empty mapping.
        /// </summary>
        private bool CheckEmptyMapping()
        {
            if (events.Count < 2)
            {
                return false;
            }

            var eventList = new FakeList<ParsingEvent>(events);
            return eventList[0] is MappingStart && eventList[1] is MappingEnd;
        }

        /// <summary>
        /// Write a tag.
        /// </summary>
        private void ProcessTag()
        {
            if (tagData.handle == null && tagData.suffix == null)
            {
                return;
            }

            if (tagData.handle != null)
            {
                WriteTagHandle(tagData.handle);
                if (tagData.suffix != null)
                {
                    WriteTagContent(tagData.suffix, false);
                }
            }
            else
            {
                WriteIndicator("!<", true, false, false);
                WriteTagContent(tagData.suffix, false);
                WriteIndicator(">", false, false, false);
            }
        }

        /// <summary>
        /// Expect MAPPING-START.
        /// </summary>
        private void EmitMappingStart(ParsingEvent evt)
        {
            ProcessAnchor();
            ProcessTag();

            var mappingStart = (MappingStart)evt;

            if (flowLevel != 0 || isCanonical || mappingStart.Style == YamlStyle.Flow || CheckEmptyMapping())
            {
                state = EmitterState.YAML_EMIT_FLOW_MAPPING_FIRST_KEY_STATE;
            }
            else
            {
                state = EmitterState.YAML_EMIT_BLOCK_MAPPING_FIRST_KEY_STATE;
            }
        }

        /// <summary>
        /// Expect SCALAR.
        /// </summary>
        private void EmitScalar(ParsingEvent evt)
        {
            SelectScalarStyle(evt);
            ProcessAnchor();
            ProcessTag();
            IncreaseIndent(true, false);
            ProcessScalar();

            indent = indents.Pop();
            state = states.Pop();
        }

        /// <summary>
        /// Write a scalar.
        /// </summary>
        private void ProcessScalar()
        {
            switch (scalarData.style)
            {
                case ScalarStyle.Plain:
                    WritePlainScalar(scalarData.value, !isSimpleKeyContext);
                    break;

                case ScalarStyle.SingleQuoted:
                    WriteSingleQuotedScalar(scalarData.value, !isSimpleKeyContext);
                    break;

                case ScalarStyle.DoubleQuoted:
                    WriteDoubleQuotedScalar(scalarData.value, !isSimpleKeyContext);
                    break;

                case ScalarStyle.Literal:
                    WriteLiteralScalar(scalarData.value);
                    break;

                case ScalarStyle.Folded:
                    WriteFoldedScalar(scalarData.value);
                    break;

                default:
                    // Impossible.
                    throw new InvalidOperationException();
            }
        }

        private static bool IsBreak(char character)
        {
            return character == '\r' || character == '\n' || character == '\x85' || character == '\x2028' || character == '\x2029';
        }

        private static bool IsBlank(char character)
        {
            return character == ' ' || character == '\t';
        }

        /// <summary>
        /// Check if the specified character is a space.
        /// </summary>
        private static bool IsSpace(char character)
        {
            return character == ' ';
        }

        internal static bool IsPrintable(char character)
        {
            return
                (character >= '\x20' && character <= '\x7E') ||
                character == '\x85' ||
                (character >= '\xA0' && character <= '\xD7FF') ||
                (character >= '\xE000' && character <= '\xFFFD');
        }

        private void WriteFoldedScalar(string value)
        {
            bool previous_break = true;
            bool leading_spaces = true;

            WriteIndicator(">", true, false, false);
            WriteBlockScalarHints(value);
            WriteBreak();

            isIndentation = true;
            isWhitespace = true;

            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];
                if (IsBreak(character))
                {
                    if (!previous_break && !leading_spaces && character == '\n')
                    {
                        int k = 0;
                        while (i + k < value.Length && IsBreak(value[i + k]))
                        {
                            ++k;
                        }
                        if (i + k < value.Length && !(IsBlank(value[i + k]) || IsBreak(value[i + k])))
                        {
                            WriteBreak();
                        }
                    }
                    WriteBreak();
                    isIndentation = true;
                    previous_break = true;
                }
                else
                {
                    if (previous_break)
                    {
                        WriteIndent();
                        leading_spaces = IsBlank(character);
                    }
                    if (!previous_break && character == ' ' && i + 1 < value.Length && value[i + 1] != ' ' && column > bestWidth)
                    {
                        WriteIndent();
                    }
                    else
                    {
                        Write(character);
                    }
                    isIndentation = false;
                    previous_break = false;
                }
            }
        }

        private void WriteLiteralScalar(string value)
        {
            bool previous_break = true;

            WriteIndicator("|", true, false, false);
            WriteBlockScalarHints(value);
            WriteBreak();

            isIndentation = true;
            isWhitespace = true;

            foreach (var character in value)
            {
                if (IsBreak(character))
                {
                    WriteBreak();
                    isIndentation = true;
                    previous_break = true;
                }
                else
                {
                    if (previous_break)
                    {
                        WriteIndent();
                    }
                    Write(character);
                    isIndentation = false;
                    previous_break = false;
                }
            }
        }

        private void WriteDoubleQuotedScalar(string value, bool allowBreaks)
        {
            WriteIndicator("\"", true, false, false);

            bool previous_space = false;
            for (int index = 0; index < value.Length; ++index)
            {
                char character = value[index];


                if (!IsPrintable(character) || IsBreak(character) || character == '"' || character == '\\')
                {
                    Write('\\');

                    switch (character)
                    {
                        case '\0':
                            Write('0');
                            break;

                        case '\x7':
                            Write('a');
                            break;

                        case '\x8':
                            Write('b');
                            break;

                        case '\x9':
                            Write('t');
                            break;

                        case '\xA':
                            Write('n');
                            break;

                        case '\xB':
                            Write('v');
                            break;

                        case '\xC':
                            Write('f');
                            break;

                        case '\xD':
                            Write('r');
                            break;

                        case '\x1B':
                            Write('e');
                            break;

                        case '\x22':
                            Write('"');
                            break;

                        case '\x5C':
                            Write('\\');
                            break;

                        case '\x85':
                            Write('N');
                            break;

                        case '\xA0':
                            Write('_');
                            break;

                        case '\x2028':
                            Write('L');
                            break;

                        case '\x2029':
                            Write('P');
                            break;

                        default:
                            short code = (short)character;
                            if (code <= 0xFF)
                            {
                                Write('x');
                                Write(code.ToString("X02", CultureInfo.InvariantCulture));
                            }
                            else if (CharHelper.IsHighSurrogate(character))
                            {
                                char nextChar;
                                if (index + 1 < value.Length && CharHelper.IsLowSurrogate(nextChar = value[index + 1]))
                                {
                                    Write('U');
                                    Write(CharHelper.ConvertToUtf32(character, nextChar).ToString("X08", CultureInfo.InvariantCulture));
                                    index++;
                                }
                                else
                                {
                                    throw new YamlException($"Unable to encode character low surrogate after high surrogate [{character}] at position {index + 1} of text `{value}`");
                                }
                            }
                            else
                            {
                                Write('u');
                                Write(code.ToString("X04", CultureInfo.InvariantCulture));
                            }
                            break;
                    }
                    previous_space = false;
                }
                else if (character == ' ')
                {
                    if (allowBreaks && !previous_space && column > bestWidth && index > 0 && index + 1 < value.Length)
                    {
                        WriteIndent();
                        if (value[index + 1] == ' ')
                        {
                            Write('\\');
                        }
                    }
                    else
                    {
                        Write(character);
                    }
                    previous_space = true;
                }
                else
                {
                    Write(character);
                    previous_space = false;
                }
            }

            WriteIndicator("\"", false, false, false);

            isWhitespace = false;
            isIndentation = false;
        }

        private void WriteSingleQuotedScalar(string value, bool allowBreaks)
        {
            WriteIndicator("'", true, false, false);

            bool previous_space = false;
            bool previous_break = false;

            for (int index = 0; index < value.Length; ++index)
            {
                char character = value[index];

                if (character == ' ')
                {
                    if (allowBreaks && !previous_space && column > bestWidth && index != 0 && index + 1 < value.Length && value[index + 1] != ' ')
                    {
                        WriteIndent();
                    }
                    else
                    {
                        Write(character);
                    }
                    previous_space = true;
                }
                else if (IsBreak(character))
                {
                    if (!previous_break && character == '\n')
                    {
                        WriteBreak();
                    }
                    WriteBreak();
                    isIndentation = true;
                    previous_break = true;
                }
                else
                {
                    if (previous_break)
                    {
                        WriteIndent();
                    }
                    if (character == '\'')
                    {
                        Write(character);
                    }
                    Write(character);
                    isIndentation = false;
                    previous_space = false;
                    previous_break = false;
                }
            }

            WriteIndicator("'", false, false, false);

            isWhitespace = false;
            isIndentation = false;
        }

        private void WritePlainScalar(string value, bool allowBreaks)
        {
            if (!isWhitespace)
            {
                Write(' ');
            }

            bool previous_space = false;
            bool previous_break = false;
            for (int index = 0; index < value.Length; ++index)
            {
                char character = value[index];

                if (IsSpace(character))
                {
                    if (allowBreaks && !previous_space && column > bestWidth && index + 1 < value.Length && value[index + 1] != ' ')
                    {
                        WriteIndent();
                    }
                    else
                    {
                        Write(character);
                    }
                    previous_space = true;
                }
                else if (IsBreak(character))
                {
                    if (!previous_break && character == '\n')
                    {
                        WriteBreak();
                    }
                    WriteBreak();
                    isIndentation = true;
                    previous_break = true;
                }
                else
                {
                    if (previous_break)
                    {
                        WriteIndent();
                    }
                    Write(character);
                    isIndentation = false;
                    previous_space = false;
                    previous_break = false;
                }
            }

            isWhitespace = false;
            isIndentation = false;

            if (isRootContext)
            {
                isOpenEnded = true;
            }
        }

        /// <summary>
        /// Increase the indentation level.
        /// </summary>
        private void IncreaseIndent(bool isFlow, bool isIndentless)
        {
            indents.Push(indent);

            if (indent < 0)
            {
                indent = isFlow ? bestIndent : 0;
            }
            else if (!isIndentless || !ForceIndentLess)
            {
                indent += bestIndent;
            }
        }

        /// <summary>
        /// Determine an acceptable scalar style.
        /// </summary>
        private void SelectScalarStyle(ParsingEvent evt)
        {
            var scalar = (Scalar)evt;

            var style = scalar.Style;
            bool noTag = tagData.handle == null && tagData.suffix == null;

            if (noTag && !scalar.IsPlainImplicit && !scalar.IsQuotedImplicit)
            {
                throw new YamlException("Neither tag nor isImplicit flags are specified.");
            }

            if (style == ScalarStyle.Any)
            {
                style = scalarData.isMultiline ? ScalarStyle.Folded : ScalarStyle.Plain;
            }

            if (isCanonical)
            {
                style = ScalarStyle.DoubleQuoted;
            }

            if (isSimpleKeyContext && (scalarData.isMultiline || emitKeyQuoted))
            {
                style = ScalarStyle.DoubleQuoted;
            }

            if (style == ScalarStyle.Plain)
            {
                if ((flowLevel != 0 && !scalarData.isFlowPlainAllowed) || (flowLevel == 0 && !scalarData.isBlockPlainAllowed))
                {
                    style = ScalarStyle.SingleQuoted;
                }
                if (string.IsNullOrEmpty(scalarData.value) && (flowLevel != 0 || isSimpleKeyContext))
                {
                    style = ScalarStyle.SingleQuoted;
                }
                if (noTag && !scalar.IsPlainImplicit)
                {
                    style = ScalarStyle.SingleQuoted;
                }
            }

            if (style == ScalarStyle.SingleQuoted)
            {
                if (!scalarData.isSingleQuotedAllowed)
                {
                    style = ScalarStyle.DoubleQuoted;
                }
            }

            if (style == ScalarStyle.Literal || style == ScalarStyle.Folded)
            {
                // Only override block styles if they're truly not possible to emit
                // Don't override if the user explicitly requested Folded/Literal style
                // unless we're in a context where block scalars are impossible (flow context, simple key)
                if (flowLevel != 0 || isSimpleKeyContext)
                {
                    style = ScalarStyle.DoubleQuoted;
                }
                // For both literal and folded scalars, fall back to double quotes if block scalars aren't allowed
                else if (!scalarData.isBlockAllowed)
                {
                    style = ScalarStyle.DoubleQuoted;
                }
            }

            // Final fallback: if no style is allowed, always use double quoted
            if (!scalarData.isFlowPlainAllowed && !scalarData.isBlockPlainAllowed &&
                !scalarData.isSingleQuotedAllowed && !scalarData.isBlockAllowed)
            {
                style = ScalarStyle.DoubleQuoted;
            }

            // TODO: What is this code supposed to mean?
            //if (noTag && !scalar.IsQuotedImplicit && style != ScalarStyle.Plain)
            //{
            //	tagData.handle = "!";
            //}

            // Final fallback: if no style is allowed, always use double quoted
            if (!scalarData.isFlowPlainAllowed && !scalarData.isBlockPlainAllowed &&
                !scalarData.isSingleQuotedAllowed && !scalarData.isBlockAllowed)
            {
                style = ScalarStyle.DoubleQuoted;
            }

            scalarData.style = style;
        }

        /// <summary>
        /// Expect ALIAS.
        /// </summary>
        private void EmitAlias()
        {
            ProcessAnchor();
            state = states.Pop();
        }

        /// <summary>
        /// Write an anchor.
        /// </summary>
        private void ProcessAnchor()
        {
            if (anchorData.anchor != null)
            {
                WriteIndicator(anchorData.isAlias ? "*" : "&", true, false, false);
                WriteAnchor(anchorData.anchor);
            }
        }

        private void WriteAnchor(string value)
        {
            Write(value);

            isWhitespace = false;
            isIndentation = false;
        }

        /// <summary>
        /// Expect DOCUMENT-END.
        /// </summary>
        private void EmitDocumentEnd(ParsingEvent evt)
        {
            if (evt is DocumentEnd documentEnd)
            {
                WriteIndent();
                if (!documentEnd.IsImplicit)
                {
                    WriteIndicator("...", true, false, false);
                    WriteIndent();
                }

                state = EmitterState.YAML_EMIT_DOCUMENT_START_STATE;

                tagDirectives.Clear();
            }
            else
            {
                throw new YamlException("Expected DOCUMENT-END.");
            }
        }

        /// <summary>
        /// 
        /// Expect a flow item node.
        /// </summary>
        private void EmitFlowSequenceItem(ParsingEvent evt, bool isFirst)
        {
            if (isFirst)
            {
                WriteIndicator("[", true, true, false);
                IncreaseIndent(true, false);
                ++flowLevel;
            }

            if (evt is SequenceEnd)
            {
                --flowLevel;
                indent = indents.Pop();
                if (isCanonical && !isFirst)
                {
                    WriteIndicator(",", false, false, false);
                    WriteIndent();
                }
                WriteIndicator("]", false, false, false);
                state = states.Pop();
                return;
            }

            if (!isFirst)
            {
                WriteIndicator(",", false, false, false);
            }

            if (isCanonical || column > bestWidth)
            {
                WriteIndent();
            }

            states.Push(EmitterState.YAML_EMIT_FLOW_SEQUENCE_ITEM_STATE);

            EmitNode(evt, false, false, false);
        }

        /// <summary>
        /// Expect a flow key node.
        /// </summary>
        private void EmitFlowMappingKey(ParsingEvent evt, bool isFirst)
        {
            if (isFirst)
            {
                WriteIndicator("{", true, true, false);
                IncreaseIndent(true, false);
                ++flowLevel;
            }

            if (evt is MappingEnd)
            {
                --flowLevel;
                indent = indents.Pop();
                if (isCanonical && !isFirst)
                {
                    WriteIndicator(",", false, false, false);
                    WriteIndent();
                }
                WriteIndicator("}", false, false, false);
                state = states.Pop();
                return;
            }

            if (!isFirst)
            {
                WriteIndicator(",", false, false, false);
            }
            if (isCanonical || column > bestWidth)
            {
                WriteIndent();
            }

            if (!isCanonical && CheckSimpleKey())
            {
                states.Push(EmitterState.YAML_EMIT_FLOW_MAPPING_SIMPLE_VALUE_STATE);
                EmitNode(evt, false, true, true);
            }
            else
            {
                WriteIndicator("?", true, false, false);
                states.Push(EmitterState.YAML_EMIT_FLOW_MAPPING_VALUE_STATE);
                EmitNode(evt, false, true, false);
            }
        }

        private const int MaxAliasLength = 128;

        private static int SafeStringLength(string? value)
        {
            return value != null ? value.Length : 0;
        }

        /// <summary>
        /// Check if the next node can be expressed as a simple key.
        /// </summary>
        private bool CheckSimpleKey()
        {
            if (events.Count < 1)
            {
                return false;
            }

            int length;
            switch (events.Peek().Type)
            {
                case EventType.YAML_ALIAS_EVENT:
                    length = SafeStringLength(anchorData.anchor);
                    break;

                case EventType.YAML_SCALAR_EVENT:
                    if (scalarData.isMultiline)
                    {
                        return false;
                    }

                    length =
                        SafeStringLength(anchorData.anchor) +
                        SafeStringLength(tagData.handle) +
                        SafeStringLength(tagData.suffix) +
                        SafeStringLength(scalarData.value);
                    break;

                case EventType.YAML_SEQUENCE_START_EVENT:
                    if (!CheckEmptySequence())
                    {
                        return false;
                    }
                    length =
                        SafeStringLength(anchorData.anchor) +
                        SafeStringLength(tagData.handle) +
                        SafeStringLength(tagData.suffix);
                    break;

                case EventType.YAML_MAPPING_START_EVENT:
                    if (!CheckEmptySequence())
                    {
                        return false;
                    }
                    length =
                        SafeStringLength(anchorData.anchor) +
                        SafeStringLength(tagData.handle) +
                        SafeStringLength(tagData.suffix);
                    break;

                default:
                    return false;
            }

            return length <= MaxAliasLength;
        }

        /// <summary>
        /// Expect a flow value node.
        /// </summary>
        private void EmitFlowMappingValue(ParsingEvent evt, bool isSimple)
        {
            if (isSimple)
            {
                WriteIndicator(":", false, false, false);
            }
            else
            {
                if (isCanonical || column > bestWidth)
                {
                    WriteIndent();
                }
                WriteIndicator(":", true, false, false);
            }
            states.Push(EmitterState.YAML_EMIT_FLOW_MAPPING_KEY_STATE);
            EmitNode(evt, false, true, false);
        }

        /// <summary>
        /// Expect a block item node.
        /// </summary>
        private void EmitBlockSequenceItem(ParsingEvent evt, bool isFirst)
        {
            if (isFirst)
            {
                IncreaseIndent(false, (isMappingContext && !isIndentation));
            }

            if (evt is SequenceEnd)
            {
                indent = indents.Pop();
                state = states.Pop();
                return;
            }

            WriteIndent();
            WriteIndicator("-", true, false, true);
            states.Push(EmitterState.YAML_EMIT_BLOCK_SEQUENCE_ITEM_STATE);

            EmitNode(evt, false, false, false);
        }

        /// <summary>
        /// Expect a block key node.
        /// </summary>
        private void EmitBlockMappingKey(ParsingEvent evt, bool isFirst)
        {
            if (isFirst)
            {
                IncreaseIndent(false, false);
            }

            if (evt is MappingEnd)
            {
                indent = indents.Pop();
                state = states.Pop();
                return;
            }

            WriteIndent();

            if (CheckSimpleKey())
            {
                states.Push(EmitterState.YAML_EMIT_BLOCK_MAPPING_SIMPLE_VALUE_STATE);
                EmitNode(evt, false, true, true);
            }
            else
            {
                WriteIndicator("?", true, false, true);
                states.Push(EmitterState.YAML_EMIT_BLOCK_MAPPING_VALUE_STATE);
                EmitNode(evt, false, true, false);
            }
        }

        /// <summary>
        /// Expect a block value node.
        /// </summary>
        private void EmitBlockMappingValue(ParsingEvent evt, bool isSimple)
        {
            if (isSimple)
            {
                WriteIndicator(":", false, false, false);
            }
            else
            {
                WriteIndent();
                WriteIndicator(":", true, false, true);
            }
            states.Push(EmitterState.YAML_EMIT_BLOCK_MAPPING_KEY_STATE);
            EmitNode(evt, false, true, false);
        }

        private void WriteBlockScalarHints(string value)
        {
            var analyzer = new StringLookAheadBuffer(value);

            if (analyzer.IsSpace() || analyzer.IsBreak())
            {
                var indent_hint = FormattableString.Invariant($"{bestIndent}");
                WriteIndicator(indent_hint, false, false, false);
            }

            isOpenEnded = false;

            string? chomp_hint = null;
            if (value.Length == 0 || !analyzer.IsBreak(value.Length - 1))
            {
                chomp_hint = "-";
            }
            else if (value.Length >= 2 && analyzer.IsBreak(value.Length - 2))
            {
                chomp_hint = "+";
                isOpenEnded = true;
            }

            if (chomp_hint != null)
            {
                WriteIndicator(chomp_hint, false, false, false);
            }
        }
    }
}
