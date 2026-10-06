// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SharpYaml.Events;
using SharpYaml.Serialization.References;

namespace SharpYaml.Serialization;

/// <summary>
/// Reads YAML tokens for use by <see cref="YamlConverter"/> implementations.
/// </summary>
/// <remarks>
/// This API is intentionally similar in spirit to <c>System.Text.Json</c>'s reader,
/// but it models YAML constructs (mappings, sequences, scalars).
/// </remarks>
public sealed class YamlReader : YamlReaderWriterBase
{
    private readonly YamlReaderState _state;

    internal YamlReader(YamlReaderState state, YamlSerializerOptions options)
        : base(options)
    {
        _state = state;
    }

    /// <summary>
    /// Creates a YAML reader over a string payload.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="options">Options used for parsing behaviors such as reference handling.</param>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
    public static YamlReader Create(string yaml, YamlSerializerOptions? options = null)
    {
        ArgumentGuard.ThrowIfNull(yaml);
        var effectiveOptions = options ?? YamlSerializerOptions.Default;
        var parser = SharpYaml.Parser.CreateParser(new StringReader(yaml), effectiveOptions.EffectiveMaxDepth, effectiveOptions.SourceName);
        var referenceReader = effectiveOptions.ReferenceHandling != YamlReferenceHandling.None ? new YamlReferenceReader() : null;
        var session = YamlUnmappedMemberSession.Create(effectiveOptions);
        var tracker = session is null ? null : new YamlPathTracker(session, prefix: null);
        return new YamlReader(new YamlReaderState(parser, referenceReader, effectiveOptions.SourceName, tracker, discriminatorPropertyName: null), effectiveOptions);
    }

    /// <summary>
    /// Creates a YAML reader over a text reader.
    /// </summary>
    /// <param name="reader">The YAML payload reader.</param>
    /// <param name="options">Options used for parsing behaviors such as reference handling.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static YamlReader Create(TextReader reader, YamlSerializerOptions? options = null)
    {
        ArgumentGuard.ThrowIfNull(reader);
        var effectiveOptions = options ?? YamlSerializerOptions.Default;
        var parser = SharpYaml.Parser.CreateParser(reader, effectiveOptions.EffectiveMaxDepth, effectiveOptions.SourceName);
        var referenceReader = effectiveOptions.ReferenceHandling != YamlReferenceHandling.None ? new YamlReferenceReader() : null;
        var session = YamlUnmappedMemberSession.Create(effectiveOptions);
        var tracker = session is null ? null : new YamlPathTracker(session, prefix: null);
        return new YamlReader(new YamlReaderState(parser, referenceReader, effectiveOptions.SourceName, tracker, discriminatorPropertyName: null), effectiveOptions);
    }

    /// <summary>
    /// Creates a YAML reader over a string payload that shares the current reader's reference anchor state.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>A new reader for <paramref name="yaml"/>.</returns>
    /// <remarks>
    /// This is intended for advanced scenarios such as polymorphic deserialization that buffers a node and needs to
    /// re-parse it while preserving anchors and aliases.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
    public YamlReader CreateReader(string yaml)
    {
        ArgumentGuard.ThrowIfNull(yaml);
        var session = _state.Tracker?.Session;
        var tracker = session is null ? null : new YamlPathTracker(session, session.BufferedPath);
        return Create(yaml, _state.ReferenceReader, _state.SourceName, Options, tracker, _state.BufferedDiscriminator);
    }

    internal static YamlReader Create(string yaml, YamlReferenceReader? referenceReader, string? sourceName, YamlSerializerOptions options, YamlPathTracker? tracker, string? discriminatorPropertyName)
    {
        ArgumentGuard.ThrowIfNull(yaml);
        ArgumentGuard.ThrowIfNull(options);
        var parser = SharpYaml.Parser.CreateParser(new StringReader(yaml), options.EffectiveMaxDepth, sourceName);
        return new YamlReader(new YamlReaderState(parser, referenceReader, sourceName, tracker, discriminatorPropertyName), options);
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="memberName"/> is the type discriminator key of the polymorphic node this reader
    /// was created for. Call this while the reader is positioned on the value of the key.
    /// </summary>
    /// <param name="memberName">The YAML key.</param>
    /// <returns>
    /// <see langword="true"/> when this reader was created by <see cref="CreateReader"/> for a node buffered by
    /// <see cref="BufferCurrentNodeToStringAndFindDiscriminator"/>, the key belongs to the root mapping of that node and it matches the
    /// discriminator property name, honoring <see cref="YamlSerializerOptions.PropertyNameCaseInsensitive"/>; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This is intended for converters and generated code: the discriminator is consumed by polymorphism, so it is not an unmapped member
    /// of the derived type even when <see cref="YamlSerializerOptions.UnmappedMemberHandling"/> disallows unmapped members.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="memberName"/> is <see langword="null"/>.</exception>
    public bool IsDiscriminatorMember(string memberName)
    {
        ArgumentGuard.ThrowIfNull(memberName);
        return _state.IsDiscriminatorMember(memberName, Options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a value indicating whether unmapped members are being reported, that is whether
    /// <see cref="YamlSerializerOptions.UnmappedMemberCallback"/> or <see cref="YamlSerializerOptions.UnmappedMembersFinalizer"/> is set.
    /// </summary>
    /// <remarks>This is intended for converters and generated code, which should only call the unmapped member methods when it is <see langword="true"/>.</remarks>
    public bool IsReportingUnmappedMembers => _state.Tracker is not null;

    /// <summary>
    /// Starts reporting an unmapped member. Call this while the reader is positioned on the value of the unmapped key.
    /// </summary>
    /// <param name="declaringType">The CLR type being deserialized.</param>
    /// <param name="knownMemberNames">The serialized member names accepted by <paramref name="declaringType"/>.</param>
    /// <param name="memberName">The unmapped YAML key.</param>
    /// <param name="instance">The instance being populated, or <see langword="null"/> when it does not exist yet.</param>
    /// <returns>
    /// The member to pass to <see cref="ReadUnmappedMember"/> or <see cref="CompleteExtensionDataMember"/>, or <see langword="null"/>
    /// when <see cref="IsReportingUnmappedMembers"/> is <see langword="false"/> or the key is the type discriminator of a polymorphic node.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="declaringType"/>, <paramref name="knownMemberNames"/>, or <paramref name="memberName"/> is <see langword="null"/>.</exception>
    public YamlUnmappedMember? BeginUnmappedMember(Type declaringType, string[] knownMemberNames, string memberName, object? instance)
    {
        var tracker = _state.Tracker;
        if (tracker is null)
        {
            return null;
        }

        ArgumentGuard.ThrowIfNull(declaringType);
        ArgumentGuard.ThrowIfNull(knownMemberNames);
        ArgumentGuard.ThrowIfNull(memberName);

        if (IsDiscriminatorMember(memberName))
        {
            return null;
        }

        return new YamlUnmappedMember(SourceName, declaringType, instance, memberName, tracker.GetPath(), tracker.GetCurrentKeyStart(), Start, knownMemberNames);
    }

    /// <summary>
    /// Consumes the value of an unmapped member and reports it. When <paramref name="member"/> is <see langword="null"/> this is <see cref="Skip"/>.
    /// </summary>
    /// <param name="member">The member returned by <see cref="BeginUnmappedMember"/>.</param>
    public void ReadUnmappedMember(YamlUnmappedMember? member)
    {
        var session = _state.Tracker?.Session;
        if (member is null || session is null)
        {
            Skip();
            return;
        }

        var element = Converters.YamlModelNodeConverter.ReadUnmappedNode(this, session.Anchors);
        member.SetValue(element);
        session.Report(member);
    }

    /// <summary>
    /// Reports an unmapped member whose value was stored in extension data. Does nothing when <paramref name="member"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="member">The member returned by <see cref="BeginUnmappedMember"/>, called before the value was read.</param>
    /// <param name="value">The value that was stored.</param>
    public void CompleteExtensionDataMember(YamlUnmappedMember? member, object? value)
    {
        var session = _state.Tracker?.Session;
        if (member is null || session is null)
        {
            return;
        }

        member.SetCapturedValue(value);
        session.Report(member);
    }

    internal void CompleteUnmappedMembers() => _state.Tracker?.Session.Complete();

    /// <summary>
    /// Gets the current token type.
    /// </summary>
    public YamlTokenType TokenType => _state.TokenType;

    /// <summary>
    /// Gets the current scalar value when <see cref="TokenType"/> is <see cref="YamlTokenType.Scalar"/>.
    /// </summary>
    public string? ScalarValue => _state.ScalarValue;

    /// <summary>
    /// Gets the current scalar style when <see cref="TokenType"/> is <see cref="YamlTokenType.Scalar"/>.
    /// </summary>
    public ScalarStyle ScalarStyle => _state.ScalarStyle;

    /// <summary>
    /// Gets the current YAML tag (when present) for the current token.
    /// </summary>
    public string? Tag => _state.Tag;

    /// <summary>
    /// Gets the current YAML anchor (when present) for the current token.
    /// </summary>
    public string? Anchor => _state.Anchor;

    /// <summary>
    /// Gets the current YAML alias when <see cref="TokenType"/> is <see cref="YamlTokenType.Alias"/>.
    /// </summary>
    public string? Alias => _state.Alias;

    /// <summary>
    /// Gets the optional source name associated with the YAML payload (for example, a file path).
    /// </summary>
    public string? SourceName => _state.SourceName;

    /// <summary>
    /// Gets the start location of the current token.
    /// </summary>
    public Mark Start => _state.Start;

    /// <summary>
    /// Gets the end location of the current token.
    /// </summary>
    public Mark End => _state.End;

    internal ParsingEvent? CurrentEvent => _state.CurrentEvent;

    internal YamlReferenceReader? ReferenceReader => _state.ReferenceReader;

    /// <summary>
    /// Attempts to resolve the current alias token into an anchored object value.
    /// </summary>
    /// <param name="value">When successful, receives the resolved anchored object.</param>
    /// <returns><see langword="true"/> when the current token is an alias and reference handling is enabled; otherwise <see langword="false"/>.</returns>
    public bool TryReadAlias(out object? value)
    {
        if (TokenType == YamlTokenType.Alias && _state.ReferenceReader is not null)
        {
            var alias = Alias;
            if (alias is null)
            {
                throw new YamlException(SourceName, Start, End, "Alias token did not provide an alias value.");
            }

            try
            {
                value = _state.ReferenceReader.Resolve(alias);
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
            {
                throw new YamlException(SourceName, Start, End, exception.Message, exception);
            }

            Read();
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Registers an anchored value for later alias resolution.
    /// </summary>
    /// <param name="anchor">The anchor name, without the <c>&amp;</c> prefix.</param>
    /// <param name="value">The anchored value instance.</param>
    public void RegisterAnchor(string anchor, object value)
    {
        ArgumentGuard.ThrowIfNull(anchor);
        ArgumentGuard.ThrowIfNull(value);

        _state.ReferenceReader?.Register(anchor, value);
    }

    /// <summary>
    /// Buffers the current YAML node to a string while optionally extracting a discriminator value from the root mapping.
    /// </summary>
    /// <param name="reader">The reader positioned at the start of the node.</param>
    /// <param name="discriminatorPropertyName">The mapping key name to treat as a discriminator.</param>
    /// <param name="discriminatorValue">Receives the discriminator scalar value when present on the root mapping.</param>
    /// <returns>The buffered YAML for the node.</returns>
    /// <remarks>
    /// This method consumes the buffered node from <paramref name="reader"/> and advances it past the node.
    /// It is primarily used by polymorphic deserialization implementations.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="discriminatorPropertyName"/> is <see langword="null"/>.</exception>
    public static string BufferCurrentNodeToStringAndFindDiscriminator(
        YamlReader reader,
        string discriminatorPropertyName,
        out string? discriminatorValue)
    {
        ArgumentGuard.ThrowIfNull(reader);
        ArgumentGuard.ThrowIfNull(discriminatorPropertyName);

        var comparer = reader.Options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        discriminatorValue = null;
        reader.CaptureBufferedNode(discriminatorPropertyName);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var yamlWriter = new YamlWriter(writer, reader.Options);

        WriteBufferedNode(reader, yamlWriter, comparer, discriminatorPropertyName, isRootMapping: true, ref discriminatorValue);
        return writer.ToString();
    }

    /// <summary>
    /// Buffers the current YAML node to a string.
    /// </summary>
    /// <param name="reader">The reader positioned at the start of the node.</param>
    /// <returns>The buffered YAML for the node.</returns>
    /// <remarks>
    /// This method consumes the buffered node from <paramref name="reader"/> and advances it past the node.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static string BufferCurrentNodeToString(YamlReader reader)
    {
        ArgumentGuard.ThrowIfNull(reader);
        reader.CaptureBufferedNode(discriminatorPropertyName: null);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var yamlWriter = new YamlWriter(writer, reader.Options);

        string? unused = null;
        WriteBufferedNode(reader, yamlWriter, StringComparer.Ordinal, discriminatorPropertyName: string.Empty, isRootMapping: false, ref unused);
        return writer.ToString();
    }

    private void CaptureBufferedNode(string? discriminatorPropertyName)
    {
        _state.BufferedDiscriminator = discriminatorPropertyName;

        var tracker = _state.Tracker;
        if (tracker is not null)
        {
            tracker.Session.BufferedPath = tracker.GetPath();
        }
    }

    /// <summary>
    /// Advances to the next token.
    /// </summary>
    public bool Read() => _state.Read();

    /// <summary>
    /// Skips the current node and any nested content.
    /// </summary>
    public void Skip() => _state.Skip();

    /// <summary>
    /// Ensures the current token is <see cref="YamlTokenType.Scalar"/> and returns its value.
    /// </summary>
    /// <exception cref="InvalidOperationException">The current token is not a scalar.</exception>
    public string GetScalarValue()
    {
        if (TokenType != YamlTokenType.Scalar)
        {
            throw new YamlException(SourceName, Start, End, $"Expected a scalar token but found '{TokenType}'.");
        }

        return ScalarValue ?? string.Empty;
    }

    private static void WriteBufferedNode(
        YamlReader reader,
        YamlWriter writer,
        StringComparer keyComparer,
        string discriminatorPropertyName,
        bool isRootMapping,
        ref string? discriminatorValue)
    {
        switch (reader.TokenType)
        {
            case YamlTokenType.Scalar:
                if (reader.Anchor is not null)
                {
                    writer.WriteAnchor(reader.Anchor);
                }

                if (reader.Tag is not null)
                {
                    writer.WriteTag(reader.Tag);
                }

                writer.WriteScalar(reader.ScalarValue);
                reader.Read();
                return;

            case YamlTokenType.Alias:
                writer.WriteAlias(reader.Alias ?? throw new InvalidOperationException("Alias token did not provide an alias value."));
                reader.Read();
                return;

            case YamlTokenType.StartSequence:
                if (reader.Anchor is not null)
                {
                    writer.WriteAnchor(reader.Anchor);
                }

                if (reader.Tag is not null)
                {
                    writer.WriteTag(reader.Tag);
                }

                writer.WriteStartSequence();
                reader.Read();
                while (reader.TokenType != YamlTokenType.EndSequence)
                {
                    WriteBufferedNode(reader, writer, keyComparer, discriminatorPropertyName, isRootMapping: false, ref discriminatorValue);
                }
                writer.WriteEndSequence();
                reader.Read();
                return;

            case YamlTokenType.StartMapping:
                if (reader.Anchor is not null)
                {
                    writer.WriteAnchor(reader.Anchor);
                }

                if (reader.Tag is not null)
                {
                    writer.WriteTag(reader.Tag);
                }

                writer.WriteStartMapping();
                reader.Read();
                while (reader.TokenType != YamlTokenType.EndMapping)
                {
                    if (reader.TokenType != YamlTokenType.Scalar)
                    {
                        throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
                    }

                    var key = reader.ScalarValue ?? string.Empty;
                    writer.WritePropertyName(key);
                    reader.Read();

                    if (isRootMapping && discriminatorValue is null && keyComparer.Equals(key, discriminatorPropertyName))
                    {
                        if (reader.TokenType != YamlTokenType.Scalar)
                        {
                            throw YamlThrowHelper.ThrowExpectedDiscriminatorScalar(reader, discriminatorPropertyName);
                        }

                        discriminatorValue = reader.ScalarValue ?? string.Empty;
                    }

                    WriteBufferedNode(reader, writer, keyComparer, discriminatorPropertyName, isRootMapping: false, ref discriminatorValue);
                }
                writer.WriteEndMapping();
                reader.Read();
                return;

            default:
                throw YamlThrowHelper.ThrowUnexpectedToken(reader);
        }
    }

    internal sealed class YamlReaderState
    {
        private readonly IParser _parser;
        private readonly string? _discriminatorPropertyName;
        private int _depth;

        public YamlReaderState(IParser parser, YamlReferenceReader? referenceReader, string? sourceName, YamlPathTracker? tracker, string? discriminatorPropertyName)
        {
            _parser = parser;
            _discriminatorPropertyName = discriminatorPropertyName;
            Tracker = tracker;
            TokenType = YamlTokenType.None;
            ReferenceReader = referenceReader;
            SourceName = sourceName;
        }

        public YamlTokenType TokenType { get; private set; }
        public string? ScalarValue { get; private set; }
        public ScalarStyle ScalarStyle { get; private set; } = ScalarStyle.Any;
        public string? Tag { get; private set; }
        public string? Anchor { get; private set; }
        public string? Alias { get; private set; }
        public Mark Start { get; private set; } = Mark.Empty;
        public Mark End { get; private set; } = Mark.Empty;
        public ParsingEvent? CurrentEvent { get; private set; }
        public YamlReferenceReader? ReferenceReader { get; }
        public string? SourceName { get; }
        public YamlPathTracker? Tracker { get; }

        /// <summary>The discriminator property name of the node most recently buffered from this reader; used to seed the reader of the re-parse.</summary>
        public string? BufferedDiscriminator { get; set; }

        /// <summary>
        /// Gets a value indicating whether <paramref name="memberName"/>, read in the root mapping of a buffered polymorphic node,
        /// is the type discriminator property. The discriminator is consumed by polymorphism rather than unmapped.
        /// </summary>
        /// <remarks><paramref name="comparison"/> must match the comparison used to find the discriminator when the node was buffered.</remarks>
        public bool IsDiscriminatorMember(string memberName, StringComparison comparison)
            => _discriminatorPropertyName is not null && _depth == 1 && string.Equals(memberName, _discriminatorPropertyName, comparison);

        public bool Read()
        {
            if (!ReadCore())
            {
                return false;
            }

            if (_discriminatorPropertyName is not null)
            {
                // Only the keys of the root mapping can be the discriminator: track how deep the reader is in the buffered node.
                if (TokenType is YamlTokenType.StartMapping or YamlTokenType.StartSequence)
                {
                    _depth++;
                }
                else if (TokenType is YamlTokenType.EndMapping or YamlTokenType.EndSequence)
                {
                    _depth--;
                }
            }

            Tracker?.OnToken(TokenType, ScalarValue, Start);
            return true;
        }

        private bool ReadCore()
        {
            while (_parser.MoveNext())
            {
                var current = _parser.Current;
                if (current is null)
                {
                    continue;
                }

                Start = current.Start;
                End = current.End;

                // These are stream/document framing tokens that most converters should not see.
                if (current is StreamStart or StreamEnd or DocumentStart or DocumentEnd)
                {
                    continue;
                }

                CurrentEvent = current;
                ScalarValue = null;
                ScalarStyle = ScalarStyle.Any;
                Tag = null;
                Anchor = null;
                Alias = null;

                switch (current)
                {
                    case MappingStart mappingStart:
                        TokenType = YamlTokenType.StartMapping;
                        Tag = mappingStart.Tag;
                        Anchor = mappingStart.Anchor;
                        return true;

                    case MappingEnd:
                        TokenType = YamlTokenType.EndMapping;
                        return true;

                    case SequenceStart sequenceStart:
                        TokenType = YamlTokenType.StartSequence;
                        Tag = sequenceStart.Tag;
                        Anchor = sequenceStart.Anchor;
                        return true;

                    case SequenceEnd:
                        TokenType = YamlTokenType.EndSequence;
                        return true;

                    case Scalar scalar:
                        TokenType = YamlTokenType.Scalar;
                        ScalarValue = scalar.Value;
                        ScalarStyle = scalar.Style;
                        Tag = scalar.Tag;
                        Anchor = scalar.Anchor;
                        return true;

                    case AnchorAlias alias:
                        TokenType = YamlTokenType.Alias;
                        Alias = alias.Value;
                        return true;
                }

                // Ignore any other event types (directives, etc.) for now.
            }

            TokenType = YamlTokenType.None;
            ScalarValue = null;
            ScalarStyle = ScalarStyle.Any;
            Tag = null;
            Anchor = null;
            Alias = null;
            Start = Mark.Empty;
            End = Mark.Empty;
            CurrentEvent = null;
            return false;
        }

        public void Skip()
        {
            switch (TokenType)
            {
                case YamlTokenType.StartMapping:
                case YamlTokenType.StartSequence:
                    SkipContainer();
                    return;

                case YamlTokenType.Scalar:
                case YamlTokenType.Alias:
                    Read();
                    return;

                default:
                    Read();
                    return;
            }
        }

        private void SkipContainer()
        {
            var depth = 1;

            while (Read())
            {
                if (TokenType is YamlTokenType.StartMapping or YamlTokenType.StartSequence)
                {
                    depth++;
                }
                else if (TokenType is YamlTokenType.EndMapping or YamlTokenType.EndSequence)
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
            }

            // Move past the end token.
            if (TokenType is YamlTokenType.EndMapping or YamlTokenType.EndSequence)
            {
                Read();
            }
        }
    }
}
