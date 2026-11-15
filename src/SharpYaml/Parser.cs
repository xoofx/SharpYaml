// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SharpYaml.Tokens;
using AnchorAlias = SharpYaml.Tokens.AnchorAlias;
using DocumentEnd = SharpYaml.Tokens.DocumentEnd;
using DocumentStart = SharpYaml.Tokens.DocumentStart;
using Event = SharpYaml.Events.ParsingEvent;
using Scalar = SharpYaml.Tokens.Scalar;
using StreamEnd = SharpYaml.Tokens.StreamEnd;
using StreamStart = SharpYaml.Tokens.StreamStart;

namespace SharpYaml;

/// <summary>Represents the Parser.</summary>
public static class Parser
{
    /// <summary>
    /// Creates a YAML parser with the default maximum nesting depth.
    /// </summary>
    /// <param name="reader">The YAML reader.</param>
    /// <returns>The parser.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static IParser CreateParser(TextReader reader)
    {
        return CreateParser(reader, maxDepth: 0);
    }

    /// <summary>
    /// Creates a YAML parser with a configurable maximum nesting depth.
    /// </summary>
    /// <param name="reader">The YAML reader.</param>
    /// <param name="maxDepth">
    /// The maximum allowed nesting depth for mappings and sequences.
    /// A value of <c>0</c> uses the default limit of 64.
    /// </param>
    /// <returns>The parser.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDepth"/> is less than 0.</exception>
    public static IParser CreateParser(TextReader reader, int maxDepth)
    {
        return CreateParser(reader, maxDepth, sourceName: null);
    }

    /// <summary>
    /// Creates a YAML parser with a configurable maximum nesting depth and optional source name.
    /// </summary>
    /// <param name="reader">The YAML reader.</param>
    /// <param name="maxDepth">
    /// The maximum allowed nesting depth for mappings and sequences.
    /// A value of <c>0</c> uses the default limit of 64.
    /// </param>
    /// <param name="sourceName">
    /// An optional source name associated with the YAML payload, such as a file path, used when reporting depth-limit failures.
    /// </param>
    /// <returns>The parser.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDepth"/> is less than 0.</exception>
    public static IParser CreateParser(TextReader reader, int maxDepth, string? sourceName)
    {
        ArgumentGuard.ThrowIfNull(reader);
        var effectiveMaxDepth = YamlDepthHelper.GetEffectiveMaxDepth(maxDepth, nameof(maxDepth));
        return CreateParserCore(reader, effectiveMaxDepth, sourceName);
    }

    private static IParser CreateParserCore(TextReader reader, int maxDepth, string? sourceName)
    {
        // The scanner reads a string in place, several times faster than it reads a reader one character at a time.
        // A reader whose end is known to be reachable is therefore read at once: a string, or a seekable stream such as
        // a file. Any other reader may be a pipe or a socket that delivers documents over time, and stays incremental.
        if (reader is StringReader)
        {
            return new Parser<StringLookAheadBuffer>(new StringLookAheadBuffer(reader.ReadToEnd()), maxDepth, sourceName);
        }

        if (reader is StreamReader { BaseStream.CanSeek: true } streamReader)
        {
            return new Parser<StringLookAheadBuffer>(new StringLookAheadBuffer(ReadToEnd(streamReader)), maxDepth, sourceName);
        }

        return new Parser<LookAheadBuffer>(new LookAheadBuffer(reader, Scanner<LookAheadBuffer>.MaxBufferLength), maxDepth, sourceName);
    }

    /// <summary>
    /// Reads the rest of a reader over a seekable stream into one string, through a pooled buffer sized from the bytes
    /// left in the stream: <see cref="TextReader.ReadToEnd"/> builds the string in chunks it then copies.
    /// </summary>
    private static string ReadToEnd(StreamReader reader)
    {
        const int MaxPooledLength = 16 * 1024 * 1024;

        // A character takes at least one byte in every encoding, so the bytes left bound the characters left in the
        // stream; the reader may hold a few more it already decoded, which the loop below makes room for.
        long remaining;
        try
        {
            var stream = reader.BaseStream;
            remaining = stream.Length - stream.Position;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or ObjectDisposedException)
        {
            return reader.ReadToEnd();
        }

        if (remaining < 0 || remaining > MaxPooledLength)
        {
            return reader.ReadToEnd();
        }

        var pool = System.Buffers.ArrayPool<char>.Shared;
        var buffer = pool.Rent((int)remaining + 64);
        var length = 0;
        try
        {
            while (true)
            {
                if (length == buffer.Length)
                {
                    var larger = pool.Rent(buffer.Length * 2);
                    Array.Copy(buffer, larger, length);
                    pool.Return(buffer, clearArray: true);
                    buffer = larger;
                }

                var read = reader.Read(buffer, length, buffer.Length - length);
                if (read <= 0)
                {
                    break;
                }

                length += read;
            }

            return new string(buffer, 0, length);
        }
        finally
        {
            // The buffer held the document: do not leave it readable to the next renter.
            pool.Return(buffer, clearArray: true);
        }
    }
}

/// <summary>
/// Parses YAML streams.
/// </summary>
public class Parser<TBuffer> : IParser, Events.IReusedEventSource where TBuffer : ILookAheadBuffer
{
    // The events returned again when ReuseEvents is set: see IReusedEventSource.
    private Events.Scalar? _reusedScalar;
    private Events.MappingStart? _reusedMappingStart;
    private Events.MappingEnd? _reusedMappingEnd;
    private Events.SequenceStart? _reusedSequenceStart;
    private Events.SequenceEnd? _reusedSequenceEnd;

    private bool _reuseEvents;

    bool Events.IReusedEventSource.ReuseEvents
    {
        get => _reuseEvents;
        set => _reuseEvents = value;
    }

    private Event CreateScalar(string? anchor, string? tag, string value, ScalarStyle style, bool isPlainImplicit, bool isQuotedImplicit, in Mark start, in Mark end)
    {
        if (!_reuseEvents || anchor is not null || tag is not null)
        {
            return new Events.Scalar(anchor, tag, value, style, isPlainImplicit, isQuotedImplicit, start, end);
        }

        var scalar = _reusedScalar ??= new Events.Scalar(null, null, string.Empty, ScalarStyle.Plain, true, false) { IsReused = true };
        scalar.Reset(value, style, isPlainImplicit, isQuotedImplicit, start, end);
        return scalar;
    }

    private Event CreateMappingStart(string? anchor, string? tag, bool isImplicit, YamlStyle style)
        => CreateMappingStart(anchor, tag, isImplicit, style, Mark.Empty, Mark.Empty);

    private Event CreateMappingStart(string? anchor, string? tag, bool isImplicit, YamlStyle style, in Mark start, in Mark end)
    {
        if (!_reuseEvents || anchor is not null || tag is not null)
        {
            return new Events.MappingStart(anchor, tag, isImplicit, style, start, end);
        }

        var mapping = _reusedMappingStart ??= new Events.MappingStart { IsReused = true };
        mapping.Reset(isImplicit, style, start, end);
        return mapping;
    }

    private Event CreateSequenceStart(string? anchor, string? tag, bool isImplicit, YamlStyle style, in Mark start, in Mark end)
    {
        if (!_reuseEvents || anchor is not null || tag is not null)
        {
            return new Events.SequenceStart(anchor, tag, isImplicit, style, start, end);
        }

        var sequence = _reusedSequenceStart ??= new Events.SequenceStart { IsReused = true };
        sequence.Reset(isImplicit, style, start, end);
        return sequence;
    }

    private Event CreateMappingEnd(in Mark start, in Mark end)
    {
        if (!_reuseEvents)
        {
            return new Events.MappingEnd(start, end);
        }

        var mappingEnd = _reusedMappingEnd ??= new Events.MappingEnd { IsReused = true };
        mappingEnd.Reset(start, end);
        return mappingEnd;
    }

    private Event CreateSequenceEnd(in Mark start, in Mark end)
    {
        if (!_reuseEvents)
        {
            return new Events.SequenceEnd(start, end);
        }

        var sequenceEnd = _reusedSequenceEnd ??= new Events.SequenceEnd { IsReused = true };
        sequenceEnd.Reset(start, end);
        return sequenceEnd;
    }

    private readonly Stack<ParserState> states = new Stack<ParserState>();
    private readonly TagDirectiveCollection tagDirectives = new TagDirectiveCollection();
    private ParserState state;

    private readonly Scanner<TBuffer> scanner;
    private readonly int _maxDepth;
    private readonly string? _sourceName;
    private int _currentDepth;
    private Token? currentToken;

    /// <summary>Gets the kind of the current token, fetching it when needed; <see cref="TokenKind.None"/> after the last token.</summary>
    private TokenKind CurrentKind
    {
        get
        {
            var token = currentToken ?? GetCurrentToken();
            return token is null ? TokenKind.None : token.Kind;
        }
    }

    private Token GetCurrentToken()
    {
        if (currentToken == null)
        {
            if (scanner.InternalMoveNext())
            {
                currentToken = scanner.Current;
            }
        }
        return currentToken!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IParser"/> class.
    /// </summary>
    /// <param name="buffer">The input where the YAML stream is to be read.</param>
    /// <param name="maxDepth">The maximum allowed nesting depth for mappings and sequences.</param>
    /// <param name="sourceName">The optional source name used when reporting depth-limit failures.</param>
    public Parser(TBuffer buffer, int maxDepth = YamlDepthHelper.DefaultMaxDepth, string? sourceName = null)
    {
        ArgumentGuard.ThrowIfNull(buffer);
        _maxDepth = YamlDepthHelper.GetEffectiveMaxDepth(maxDepth, nameof(maxDepth));
        _sourceName = sourceName;
        scanner = new Scanner<TBuffer>(buffer);

        // The parser copies what it needs out of a token before it skips it: see Skip.
        scanner.EnableTokenRecycling();
    }

    /// <summary>
    /// Gets the current event.
    /// </summary>
    public Event? Current { get; private set; }

    /// <summary>
    /// Moves to the next event.
    /// </summary>
    /// <returns>Returns true if there are more events available, otherwise returns false.</returns>
    public bool MoveNext()
    {
        // No events after the end of the stream or error.
        if (state == ParserState.YAML_PARSE_END_STATE)
        {
            Current = null;
            return false;
        }
        else
        {
            // Generate the next event.
            Current = StateMachine();
            EnforceMaxDepth(Current);
            return true;
        }
    }

    private void EnforceMaxDepth(Event current)
    {
        // The event type is one virtual call; the end events are not sealed, so testing their type costs a call each.
        var type = current.Type;
        if (type is Events.EventType.YAML_SEQUENCE_START_EVENT or Events.EventType.YAML_MAPPING_START_EVENT)
        {
            if (_currentDepth >= _maxDepth)
            {
                throw YamlDepthHelper.CreateMaxDepthExceededException(_maxDepth, current.Start, current.End, _sourceName);
            }

            _currentDepth++;
        }
        else if (type is Events.EventType.YAML_SEQUENCE_END_EVENT or Events.EventType.YAML_MAPPING_END_EVENT)
        {
            _currentDepth--;
        }
    }

    private Event StateMachine()
    {
        switch (state)
        {
            case ParserState.YAML_PARSE_STREAM_START_STATE:
                return ParseStreamStart();

            case ParserState.YAML_PARSE_IMPLICIT_DOCUMENT_START_STATE:
                return ParseDocumentStart(true);

            case ParserState.YAML_PARSE_DOCUMENT_START_STATE:
                return ParseDocumentStart(false);

            case ParserState.YAML_PARSE_DOCUMENT_CONTENT_STATE:
                return ParseDocumentContent();

            case ParserState.YAML_PARSE_DOCUMENT_END_STATE:
                return ParseDocumentEnd();

            case ParserState.YAML_PARSE_BLOCK_NODE_STATE:
                return ParseNode(true, false);

            case ParserState.YAML_PARSE_BLOCK_NODE_OR_INDENTLESS_SEQUENCE_STATE:
                return ParseNode(true, true);

            case ParserState.YAML_PARSE_FLOW_NODE_STATE:
                return ParseNode(false, false);

            case ParserState.YAML_PARSE_BLOCK_SEQUENCE_FIRST_ENTRY_STATE:
                return ParseBlockSequenceEntry(true);

            case ParserState.YAML_PARSE_BLOCK_SEQUENCE_ENTRY_STATE:
                return ParseBlockSequenceEntry(false);

            case ParserState.YAML_PARSE_INDENTLESS_SEQUENCE_ENTRY_STATE:
                return ParseIndentlessSequenceEntry();

            case ParserState.YAML_PARSE_BLOCK_MAPPING_FIRST_KEY_STATE:
                return ParseBlockMappingKey(true);

            case ParserState.YAML_PARSE_BLOCK_MAPPING_KEY_STATE:
                return ParseBlockMappingKey(false);

            case ParserState.YAML_PARSE_BLOCK_MAPPING_VALUE_STATE:
                return ParseBlockMappingValue();

            case ParserState.YAML_PARSE_FLOW_SEQUENCE_FIRST_ENTRY_STATE:
                return ParseFlowSequenceEntry(true);

            case ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_STATE:
                return ParseFlowSequenceEntry(false);

            case ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_KEY_STATE:
                return ParseFlowSequenceEntryMappingKey();

            case ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_VALUE_STATE:
                return ParseFlowSequenceEntryMappingValue();

            case ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_END_STATE:
                return ParseFlowSequenceEntryMappingEnd();

            case ParserState.YAML_PARSE_FLOW_MAPPING_FIRST_KEY_STATE:
                return ParseFlowMappingKey(true);

            case ParserState.YAML_PARSE_FLOW_MAPPING_KEY_STATE:
                return ParseFlowMappingKey(false);

            case ParserState.YAML_PARSE_FLOW_MAPPING_VALUE_STATE:
                return ParseFlowMappingValue(false);

            case ParserState.YAML_PARSE_FLOW_MAPPING_EMPTY_VALUE_STATE:
                return ParseFlowMappingValue(true);

            default:
                Debug.Assert(false, "Invalid state"); // Invalid state.
                throw new InvalidOperationException();
        }
    }

    private void Skip()
    {
        if (currentToken != null)
        {
            // The scanner uses the token again for a later one: nothing here keeps a reference to a skipped token
            // of a pooled kind (scalars and structural tokens), only their marks and values.
            var consumed = currentToken;
            currentToken = null;
            scanner.ConsumeCurrent();
            scanner.Recycle(consumed);
        }
    }

    /// <summary>
    /// Parse the production:
    /// stream   ::= STREAM-START implicit_document? explicit_document* STREAM-END
    ///              ************
    /// </summary>
    private Event ParseStreamStart()
    {
        if (GetCurrentToken() is not StreamStart streamStart)
        {
            var current = GetCurrentToken();
            throw new SemanticErrorException(current.Start, current.End, "Did not find expected <stream-start>.");
        }
        Skip();

        state = ParserState.YAML_PARSE_IMPLICIT_DOCUMENT_START_STATE;
        return new Events.StreamStart(streamStart.Start, streamStart.End);
    }

    /// <summary>
    /// Parse the productions:
    /// implicit_document    ::= block_node DOCUMENT-END*
    ///                          *
    /// explicit_document    ::= DIRECTIVE* DOCUMENT-START block_node? DOCUMENT-END*
    ///                          *************************
    /// </summary>
    private Event ParseDocumentStart(bool isImplicit)
    {
        while (GetCurrentToken() is DocumentEnd documentEnd)
        {
            var endLine = documentEnd.Start.Line;
            Skip();
            if (CurrentKind != TokenKind.StreamEnd && GetCurrentToken().Start.Line == endLine)
            {
                throw new SemanticErrorException(GetCurrentToken().Start, GetCurrentToken().End, "Document end markers must be followed by a line break.");
            }
        }

        // Parse an isImplicit document.

        if (isImplicit && !(CurrentKind == TokenKind.VersionDirective || CurrentKind == TokenKind.TagDirective || CurrentKind == TokenKind.ReservedDirective || CurrentKind == TokenKind.DocumentStart || CurrentKind == TokenKind.StreamEnd))
        {
            var directives = new TagDirectiveCollection();
            ProcessDirectives(directives);

            states.Push(ParserState.YAML_PARSE_DOCUMENT_END_STATE);

            state = ParserState.YAML_PARSE_BLOCK_NODE_STATE;

            return new Events.DocumentStart(null, directives, true, GetCurrentToken().Start, GetCurrentToken().End);
        }

        // Parse an explicit document.

        else if (CurrentKind != TokenKind.StreamEnd)
        {
            var start = GetCurrentToken().Start;
            var directives = new TagDirectiveCollection();
            var versionDirective = ProcessDirectives(directives);

            var current = GetCurrentToken();
            if (current is not DocumentStart)
            {
                throw new SemanticErrorException(current.Start, current.End, "Did not find expected <document start>.");
            }

            states.Push(ParserState.YAML_PARSE_DOCUMENT_END_STATE);

            state = ParserState.YAML_PARSE_DOCUMENT_CONTENT_STATE;

            Event evt = new Events.DocumentStart(versionDirective, directives, false, start, current.End);
            Skip();
            return evt;
        }

        // Parse the stream end.

        else
        {
            state = ParserState.YAML_PARSE_END_STATE;

            Event evt = new Events.StreamEnd(GetCurrentToken().Start, GetCurrentToken().End);
            // Do not call skip here because that would throw an exception
            if (scanner.InternalMoveNext())
            {
                throw new InvalidOperationException("The scanner should contain no more tokens.");
            }
            return evt;
        }
    }

    /// <summary>
    /// Parse directives.
    /// </summary>
    private VersionDirective? ProcessDirectives(TagDirectiveCollection tags)
    {
        VersionDirective? version = null;

        while (true)
        {
            if (GetCurrentToken() is VersionDirective currentVersion)
            {
                if (version != null)
                {
                    throw new SemanticErrorException(currentVersion.Start, currentVersion.End, "Found duplicate %YAML directive.");
                }

                if (!Constants.IsSupportedYamlVersion(currentVersion.Version))
                {
                    throw new SemanticErrorException(currentVersion.Start, currentVersion.End, "Found incompatible YAML document.");
                }

                version = currentVersion;
            }
            else if (GetCurrentToken() is TagDirective tag)
            {
                if (tagDirectives.Contains(tag.Handle))
                {
                    throw new SemanticErrorException(tag.Start, tag.End, "Found duplicate %TAG directive.");
                }
                tagDirectives.Add(tag);
                if (tags != null)
                {
                    tags.Add(tag);
                }
            }
            else if (CurrentKind == TokenKind.ReservedDirective)
            {
                // YAML 1.2.2 §6.8: unrecognized directives are ignored.
            }
            else
            {
                break;
            }

            Skip();
        }

        if (tags != null)
        {
            AddDefaultTagDirectives(tags);
        }
        AddDefaultTagDirectives(tagDirectives);

        return version;
    }

    private static void AddDefaultTagDirectives(TagDirectiveCollection directives)
    {
        foreach (var directive in Constants.DefaultTagDirectives)
        {
            if (!directives.Contains(directive))
            {
                directives.Add(directive);
            }
        }
    }

    /// <summary>
    /// Parse the productions:
    /// explicit_document    ::= DIRECTIVE* DOCUMENT-START block_node? DOCUMENT-END*
    ///                                                    ***********
    /// </summary>
    private Event ParseDocumentContent()
    {
        if (
            CurrentKind == TokenKind.VersionDirective ||
            CurrentKind == TokenKind.TagDirective ||
            CurrentKind == TokenKind.ReservedDirective ||
            CurrentKind == TokenKind.DocumentStart ||
            CurrentKind == TokenKind.DocumentEnd ||
            CurrentKind == TokenKind.StreamEnd
        )
        {
            state = states.Pop();
            return ProcessEmptyScalar(scanner.CurrentPosition);
        }
        else
        {
            return ParseNode(true, false);
        }
    }

    /// <summary>
    /// Generate an empty scalar event.
    /// </summary>
    private Event ProcessEmptyScalar(Mark position)
    {
        return CreateScalar(null, null, string.Empty, ScalarStyle.Plain, true, false, position, position);
    }

    /// <summary>
    /// Parse the productions:
    /// block_node_or_indentless_sequence    ::=
    ///                          ALIAS
    ///                          *****
    ///                          | properties (block_content | indentless_block_sequence)?
    ///                            **********  *
    ///                          | block_content | indentless_block_sequence
    ///                            *
    /// block_node           ::= ALIAS
    ///                          *****
    ///                          | properties block_content?
    ///                            ********** *
    ///                          | block_content
    ///                            *
    /// flow_node            ::= ALIAS
    ///                          *****
    ///                          | properties flow_content?
    ///                            ********** *
    ///                          | flow_content
    ///                            *
    /// properties           ::= TAG ANCHOR? | ANCHOR TAG?
    ///                          *************************
    /// block_content        ::= block_collection | flow_collection | SCALAR
    ///                                                               ******
    /// flow_content         ::= flow_collection | SCALAR
    ///                                            ******
    /// </summary>
    private Event ParseNode(bool isBlock, bool isIndentlessSequence)
    {
        if (CurrentKind == TokenKind.AnchorAlias)
        {
            var alias = (AnchorAlias)GetCurrentToken();
            state = states.Pop();
            Event evt = new Events.AnchorAlias(alias.Value, alias.Start, alias.End);
            Skip();
            return evt;
        }

        var start = GetCurrentToken().Start;

        Anchor? anchor = null;
        Tag? tag = null;

        // The anchor and the tag can be in any order. This loop repeats at most twice.
        while (true)
        {
            if (anchor == null && CurrentKind == TokenKind.Anchor)
            {
                anchor = (Anchor)GetCurrentToken();
                Skip();
            }
            else if (tag == null && CurrentKind == TokenKind.Tag)
            {
                tag = (Tag)GetCurrentToken();
                Skip();
            }
            else
            {
                break;
            }
        }

        string? tagName = null;
        if (tag != null)
        {
            if (string.IsNullOrEmpty(tag.Handle))
            {
                tagName = tag.Suffix;
            }
            else if (tagDirectives.Contains(tag.Handle))
            {
                tagName = string.Concat(tagDirectives[tag.Handle].Prefix, tag.Suffix);
            }
            else
            {
                throw new SemanticErrorException(tag.Start, tag.End, "While parsing a node, find undefined tag handle.");
            }
        }
        if (string.IsNullOrEmpty(tagName))
        {
            tagName = null;
        }

        var anchorName = anchor != null ? string.IsNullOrEmpty(anchor.Value) ? null : anchor.Value : null;

        bool isImplicit = string.IsNullOrEmpty(tagName);

        if (isIndentlessSequence && CurrentKind == TokenKind.BlockEntry)
        {
            state = ParserState.YAML_PARSE_INDENTLESS_SEQUENCE_ENTRY_STATE;

            return CreateSequenceStart(
                anchorName,
                tagName,
                isImplicit,
                YamlStyle.Block,
                start,
                GetCurrentToken().End
            );
        }
        else
        {
            var kind = CurrentKind;
            if (kind == TokenKind.Scalar)
            {
                var scalar = (Scalar)GetCurrentToken();
                bool isPlainImplicit = false;
                bool isQuotedImplicit = false;
                if ((scalar.Style == ScalarStyle.Plain && tagName == null) || tagName == Constants.DefaultHandle)
                {
                    isPlainImplicit = true;
                }
                else if (tagName == null)
                {
                    isQuotedImplicit = true;
                }

                state = states.Pop();
                Event evt = CreateScalar(anchorName, tagName, scalar.Value, scalar.Style, isPlainImplicit, isQuotedImplicit, start, scalar.End);

                Skip();
                return evt;
            }

            if (kind == TokenKind.FlowSequenceStart)
            {
                state = ParserState.YAML_PARSE_FLOW_SEQUENCE_FIRST_ENTRY_STATE;
                return CreateSequenceStart(anchorName, tagName, isImplicit, YamlStyle.Flow, start, GetCurrentToken().End);
            }

            if (kind == TokenKind.FlowMappingStart)
            {
                state = ParserState.YAML_PARSE_FLOW_MAPPING_FIRST_KEY_STATE;
                return CreateMappingStart(anchorName, tagName, isImplicit, YamlStyle.Flow, start, GetCurrentToken().End);
            }

            if (isBlock)
            {
                if (kind == TokenKind.BlockSequenceStart)
                {
                    state = ParserState.YAML_PARSE_BLOCK_SEQUENCE_FIRST_ENTRY_STATE;
                    return CreateSequenceStart(anchorName, tagName, isImplicit, YamlStyle.Block, start, GetCurrentToken().End);
                }

                if (kind == TokenKind.BlockMappingStart)
                {
                    state = ParserState.YAML_PARSE_BLOCK_MAPPING_FIRST_KEY_STATE;
                    return CreateMappingStart(anchorName, tagName, isImplicit, YamlStyle.Block, start, GetCurrentToken().End);
                }
            }

            if (anchorName != null || tag != null)
            {
                state = states.Pop();
                return CreateScalar(anchorName, tagName, string.Empty, ScalarStyle.Plain, isImplicit, false, start, GetCurrentToken().End);
            }

            var current = GetCurrentToken();
            throw new SemanticErrorException(current.Start, current.End, "While parsing a node, did not find expected node content.");
        }
    }

    /// <summary>
    /// Parse the productions:
    /// implicit_document    ::= block_node DOCUMENT-END*
    ///                                     *************
    /// explicit_document    ::= DIRECTIVE* DOCUMENT-START block_node? DOCUMENT-END*
    ///                                                                *************
    /// </summary>
    private Event ParseDocumentEnd()
    {
        bool isImplicit = true;
        var start = GetCurrentToken().Start;
        var end = start;

        if (CurrentKind == TokenKind.DocumentEnd)
        {
            var endLine = GetCurrentToken().Start.Line;
            end = GetCurrentToken().End;
            Skip();
            if (CurrentKind != TokenKind.StreamEnd && GetCurrentToken().Start.Line == endLine)
            {
                throw new SemanticErrorException(GetCurrentToken().Start, GetCurrentToken().End, "Document end markers must be followed by a line break.");
            }
            isImplicit = false;
        }
        else if (GetCurrentToken() is VersionDirective or TagDirective or ReservedDirective)
        {
            throw new SemanticErrorException(start, end, "A directives document must be preceded by an explicit document end marker.");
        }

        tagDirectives.Clear();

        state = isImplicit ? ParserState.YAML_PARSE_DOCUMENT_START_STATE : ParserState.YAML_PARSE_IMPLICIT_DOCUMENT_START_STATE;
        return new Events.DocumentEnd(isImplicit, start, end);
    }

    /// <summary>
    /// Parse the productions:
    /// block_sequence ::= BLOCK-SEQUENCE-START (BLOCK-ENTRY block_node?)* BLOCK-END
    ///                    ********************  *********** *             *********
    /// </summary>
    private Event ParseBlockSequenceEntry(bool isFirst)
    {
        if (isFirst)
        {
            GetCurrentToken();
            Skip();
        }

        if (CurrentKind == TokenKind.BlockEntry)
        {
            var mark = GetCurrentToken().End;

            Skip();
            if (!(CurrentKind == TokenKind.BlockEntry || CurrentKind == TokenKind.BlockEnd))
            {
                states.Push(ParserState.YAML_PARSE_BLOCK_SEQUENCE_ENTRY_STATE);
                return ParseNode(true, false);
            }
            else
            {
                state = ParserState.YAML_PARSE_BLOCK_SEQUENCE_ENTRY_STATE;
                return ProcessEmptyScalar(mark);
            }
        }

        else if (CurrentKind == TokenKind.BlockEnd)
        {
            state = states.Pop();
            Event evt = CreateSequenceEnd(GetCurrentToken().Start, GetCurrentToken().End);
            Skip();
            return evt;
        }

        else
        {
            var current = GetCurrentToken();
            throw new SemanticErrorException(current.Start, current.End, "While parsing a block collection, did not find expected '-' indicator.");
        }
    }

    /// <summary>
    /// Parse the productions:
    /// indentless_sequence  ::= (BLOCK-ENTRY block_node?)+
    ///                           *********** *
    /// </summary>
    private Event ParseIndentlessSequenceEntry()
    {
        if (CurrentKind == TokenKind.BlockEntry)
        {
            var mark = GetCurrentToken().End;
            Skip();

            if (!(CurrentKind == TokenKind.BlockEntry || CurrentKind == TokenKind.Key || CurrentKind == TokenKind.Value || CurrentKind == TokenKind.BlockEnd))
            {
                states.Push(ParserState.YAML_PARSE_INDENTLESS_SEQUENCE_ENTRY_STATE);
                return ParseNode(true, false);
            }
            else
            {
                state = ParserState.YAML_PARSE_INDENTLESS_SEQUENCE_ENTRY_STATE;
                return ProcessEmptyScalar(mark);
            }
        }
        else
        {
            state = states.Pop();
            return CreateSequenceEnd(GetCurrentToken().Start, GetCurrentToken().End);
        }
    }

    /// <summary>
    /// Parse the productions:
    /// block_mapping        ::= BLOCK-MAPPING_START
    ///                          *******************
    ///                          ((KEY block_node_or_indentless_sequence?)?
    ///                            *** *
    ///                          (VALUE block_node_or_indentless_sequence?)?)*
    ///
    ///                          BLOCK-END
    ///                          *********
    /// </summary>
    private Event ParseBlockMappingKey(bool isFirst)
    {
        if (isFirst)
        {
            GetCurrentToken();
            Skip();
        }

        if (CurrentKind == TokenKind.Key)
        {
            var mark = GetCurrentToken().End;
            Skip();
            if (!(CurrentKind == TokenKind.Key || CurrentKind == TokenKind.Value || CurrentKind == TokenKind.BlockEnd))
            {
                states.Push(ParserState.YAML_PARSE_BLOCK_MAPPING_VALUE_STATE);
                return ParseNode(true, true);
            }
            else
            {
                state = ParserState.YAML_PARSE_BLOCK_MAPPING_VALUE_STATE;
                return ProcessEmptyScalar(mark);
            }
        }

        else if (CurrentKind == TokenKind.BlockEnd)
        {
            state = states.Pop();
            Event evt = CreateMappingEnd(GetCurrentToken().Start, GetCurrentToken().End);
            Skip();
            return evt;
        }

        else
        {
            var current = GetCurrentToken();
            if (current is Value)
            {
                state = ParserState.YAML_PARSE_BLOCK_MAPPING_VALUE_STATE;
                return ProcessEmptyScalar(current.Start);
            }
            throw new SemanticErrorException(current.Start, current.End, "While parsing a block mapping, did not find expected key.");
        }
    }

    /// <summary>
    /// Parse the productions:
    /// block_mapping        ::= BLOCK-MAPPING_START
    ///
    ///                          ((KEY block_node_or_indentless_sequence?)?
    ///
    ///                          (VALUE block_node_or_indentless_sequence?)?)*
    ///                           ***** *
    ///                          BLOCK-END
    ///
    /// </summary>
    private Event ParseBlockMappingValue()
    {
        if (CurrentKind == TokenKind.Value)
        {
            var mark = GetCurrentToken().End;
            Skip();

            if (!(CurrentKind == TokenKind.Key || CurrentKind == TokenKind.Value || CurrentKind == TokenKind.BlockEnd))
            {
                states.Push(ParserState.YAML_PARSE_BLOCK_MAPPING_KEY_STATE);
                return ParseNode(true, true);
            }
            else
            {
                state = ParserState.YAML_PARSE_BLOCK_MAPPING_KEY_STATE;
                return ProcessEmptyScalar(mark);
            }
        }

        else
        {
            state = ParserState.YAML_PARSE_BLOCK_MAPPING_KEY_STATE;
            return ProcessEmptyScalar(GetCurrentToken().Start);
        }
    }

    /// <summary>
    /// Parse the productions:
    /// flow_sequence        ::= FLOW-SEQUENCE-START
    ///                          *******************
    ///                          (flow_sequence_entry FLOW-ENTRY)*
    ///                           *                   **********
    ///                          flow_sequence_entry?
    ///                          *
    ///                          FLOW-SEQUENCE-END
    ///                          *****************
    /// flow_sequence_entry  ::= flow_node | KEY flow_node? (VALUE flow_node?)?
    ///                          *
    /// </summary>
    private Event ParseFlowSequenceEntry(bool isFirst)
    {
        if (isFirst)
        {
            GetCurrentToken();
            Skip();
        }

        Event evt;
        if (CurrentKind != TokenKind.FlowSequenceEnd)
        {
            if (!isFirst)
            {
                if (CurrentKind == TokenKind.FlowEntry)
                {
                    Skip();
                }
                else
                {
                    var current = GetCurrentToken();
                    throw new SemanticErrorException(current.Start, current.End, "While parsing a flow sequence, did not find expected ',' or ']'.");
                }
            }

            if (CurrentKind == TokenKind.Value)
            {
                state = ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_KEY_STATE;
                return CreateMappingStart(null, null, true, YamlStyle.Flow);
            }
            if (CurrentKind == TokenKind.Key)
            {
                state = ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_KEY_STATE;
                evt = CreateMappingStart(null, null, true, YamlStyle.Flow);
                Skip();
                return evt;
            }
            else if (CurrentKind != TokenKind.FlowSequenceEnd)
            {
                states.Push(ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_STATE);
                return ParseNode(false, false);
            }
        }

        state = states.Pop();
        evt = CreateSequenceEnd(GetCurrentToken().Start, GetCurrentToken().End);
        Skip();
        return evt;
    }

    /// <summary>
    /// Parse the productions:
    /// flow_sequence_entry  ::= flow_node | KEY flow_node? (VALUE flow_node?)?
    ///                                      *** *
    /// </summary>
    private Event ParseFlowSequenceEntryMappingKey()
    {
        if (!(CurrentKind == TokenKind.Value || CurrentKind == TokenKind.FlowEntry || CurrentKind == TokenKind.FlowSequenceEnd))
        {
            states.Push(ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_VALUE_STATE);
            return ParseNode(false, false);
        }
        else
        {
            var mark = GetCurrentToken().End;
            state = ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_VALUE_STATE;
            return ProcessEmptyScalar(mark);
        }
    }

    /// <summary>
    /// Parse the productions:
    /// flow_sequence_entry  ::= flow_node | KEY flow_node? (VALUE flow_node?)?
    ///                                                      ***** *
    /// </summary>
    private Event ParseFlowSequenceEntryMappingValue()
    {
        if (CurrentKind == TokenKind.Value)
        {
            Skip();
            if (!(CurrentKind == TokenKind.FlowEntry || CurrentKind == TokenKind.FlowSequenceEnd))
            {
                states.Push(ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_END_STATE);
                return ParseNode(false, false);
            }
        }
        state = ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_MAPPING_END_STATE;
        return ProcessEmptyScalar(GetCurrentToken().Start);
    }

    /// <summary>
    /// Parse the productions:
    /// flow_sequence_entry  ::= flow_node | KEY flow_node? (VALUE flow_node?)?
    ///                                                                      *
    /// </summary>
    private Event ParseFlowSequenceEntryMappingEnd()
    {
        state = ParserState.YAML_PARSE_FLOW_SEQUENCE_ENTRY_STATE;
        return CreateMappingEnd(GetCurrentToken().Start, GetCurrentToken().End);
    }

    /// <summary>
    /// Parse the productions:
    /// flow_mapping         ::= FLOW-MAPPING-START
    ///                          ******************
    ///                          (flow_mapping_entry FLOW-ENTRY)*
    ///                           *                  **********
    ///                          flow_mapping_entry?
    ///                          ******************
    ///                          FLOW-MAPPING-END
    ///                          ****************
    /// flow_mapping_entry   ::= flow_node | KEY flow_node? (VALUE flow_node?)?
    ///                          *           *** *
    /// </summary>
    private Event ParseFlowMappingKey(bool isFirst)
    {
        if (isFirst)
        {
            GetCurrentToken();
            Skip();
        }

        if (CurrentKind != TokenKind.FlowMappingEnd)
        {
            if (!isFirst)
            {
                if (CurrentKind == TokenKind.FlowEntry)
                {
                    Skip();
                }
                else
                {
                    var current = GetCurrentToken();
                    throw new SemanticErrorException(current.Start, current.End, "While parsing a flow mapping,  did not find expected ',' or '}'.");
                }
            }

            if (CurrentKind == TokenKind.Value)
            {
                state = ParserState.YAML_PARSE_FLOW_MAPPING_VALUE_STATE;
                return ProcessEmptyScalar(GetCurrentToken().Start);
            }
            if (CurrentKind == TokenKind.Key)
            {
                Skip();

                if (!(CurrentKind == TokenKind.Value || CurrentKind == TokenKind.FlowEntry || CurrentKind == TokenKind.FlowMappingEnd))
                {
                    states.Push(ParserState.YAML_PARSE_FLOW_MAPPING_VALUE_STATE);
                    return ParseNode(false, false);
                }
                else
                {
                    state = ParserState.YAML_PARSE_FLOW_MAPPING_VALUE_STATE;
                    return ProcessEmptyScalar(GetCurrentToken().Start);
                }
            }
            else if (CurrentKind != TokenKind.FlowMappingEnd)
            {
                states.Push(ParserState.YAML_PARSE_FLOW_MAPPING_VALUE_STATE);
                return ParseNode(false, false);
            }
        }

        state = states.Pop();
        Event evt = CreateMappingEnd(GetCurrentToken().Start, GetCurrentToken().End);
        Skip();
        return evt;
    }

    /// <summary>
    /// Parse the productions:
    /// flow_mapping_entry   ::= flow_node | KEY flow_node? (VALUE flow_node?)?
    ///                                   *                  ***** *
    /// </summary>
    private Event ParseFlowMappingValue(bool isEmpty)
    {
        if (isEmpty)
        {
            state = ParserState.YAML_PARSE_FLOW_MAPPING_KEY_STATE;
            return ProcessEmptyScalar(GetCurrentToken().Start);
        }

        if (CurrentKind == TokenKind.Value)
        {
            Skip();
            if (!(CurrentKind == TokenKind.FlowEntry || CurrentKind == TokenKind.FlowMappingEnd))
            {
                states.Push(ParserState.YAML_PARSE_FLOW_MAPPING_KEY_STATE);
                return ParseNode(false, false);
            }
        }

        state = ParserState.YAML_PARSE_FLOW_MAPPING_KEY_STATE;
        return ProcessEmptyScalar(GetCurrentToken().Start);
    }
}
