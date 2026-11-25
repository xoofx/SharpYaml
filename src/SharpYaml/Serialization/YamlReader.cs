// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
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

    // The number of events of the node buffered last by BufferCurrentNodeAndFindDiscriminator.
    private int _recordedEventCount;

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
        return Create(parser, effectiveOptions);
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
        return Create(parser, effectiveOptions);
    }

    private static YamlReader Create(IParser parser, YamlSerializerOptions options)
    {
        ReuseEvents(parser);
        var session = YamlReportingSession.Create(options);
        var tracker = session is null ? null : new YamlPathTracker(session, prefix: null);
        var anchors = new YamlAnchorTable(new YamlAliasScope(options));
        return new YamlReader(new YamlReaderState(parser, anchors, nesting: 0, options.SourceName, tracker, discriminatorPropertyName: null), options);
    }

    /// <summary>
    /// Lets a parser this reader owns return the same event objects again: the reader copies an event into its state
    /// before it moves the parser, and detaches the events it has to keep (<see cref="ParsingEvent.Detach"/>).
    /// </summary>
    private static void ReuseEvents(IParser parser)
    {
        if (parser is IReusedEventSource source)
        {
            source.ReuseEvents = true;
        }
    }

    /// <summary>
    /// Creates a YAML reader over a string payload that shares the current reader's reference anchor state.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>A new reader for <paramref name="yaml"/>.</returns>
    /// <remarks>
    /// This is intended for advanced scenarios such as polymorphic deserialization that buffers a node and needs to
    /// re-parse it while preserving anchors and aliases. The aliases of <paramref name="yaml"/> resolve to the anchors
    /// bound where the node was buffered, or to the anchors bound now when no node was buffered.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
    public YamlReader CreateReader(string yaml)
    {
        ArgumentGuard.ThrowIfNull(yaml);
        var parser = SharpYaml.Parser.CreateParser(new StringReader(yaml), Options.EffectiveMaxDepth, _state.SourceName);
        ReuseEvents(parser);
        return CreateBufferedReader(parser);
    }

    /// <summary>Creates the reader of a node buffered from this reader, whose events <paramref name="parser"/> gives.</summary>
    private YamlReader CreateBufferedReader(IParser parser)
    {
        var session = _state.Tracker?.Session;
        var tracker = session is null ? null : new YamlPathTracker(session, session.BufferedPath);
        var anchors = _state.CreateBufferedAnchors(out var nesting);
        return new YamlReader(new YamlReaderState(parser, anchors, nesting, _state.SourceName, tracker, _state.BufferedDiscriminator), Options);
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
    public bool IsReportingUnmappedMembers => _state.Tracker is { Session.IsReportingUnmappedMembers: true };

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
        if (_state.Tracker is not { Session.IsReportingUnmappedMembers: true } tracker)
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

    /// <summary>
    /// Runs the configured finalizers, then throws <see cref="YamlInvalidValuesException"/> when values were recovered under
    /// <see cref="YamlInvalidValueHandling.FailAtEnd"/>. Called by the serializer once a read completed.
    /// </summary>
    internal void CompleteReports() => _state.Tracker?.Session.Complete();

    /// <summary>
    /// Gets whether the read must fail at its end because values were recovered under <see cref="YamlInvalidValueHandling.FailAtEnd"/>.
    /// </summary>
    internal bool FailsAtEnd => _state.Tracker is { Session.FailsAtEnd: true };

    /// <summary>
    /// Returns what the parser of this reader took from a pool to read its input (see
    /// <see cref="IReusedEventSource.ReleaseInput"/>). Called by the serializer once a read ended, in every case.
    /// </summary>
    internal void ReleaseInput() => _state.ReleaseInput();

    /// <summary>
    /// Folds <paramref name="fatal"/>, which ended the read, into the <see cref="YamlInvalidValuesException"/> listing the values
    /// recovered so far. Call this only when <see cref="FailsAtEnd"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="fatal">The exception that ended the read.</param>
    internal YamlInvalidValuesException CreateFatalException(YamlException fatal)
        => _state.Tracker is { } tracker ? tracker.Session.CreateException(fatal) : throw new InvalidOperationException("No invalid value was recovered.");

    /// <summary>
    /// Gets whether <paramref name="exception"/>, thrown while reading a value, can be recovered from: the configured
    /// <see cref="YamlSerializerOptions.InvalidValueHandling"/> is not <see cref="YamlInvalidValueHandling.FailFast"/>, the
    /// exception does not come from the parser, and the reader is still on the token the read began on, so nothing of the
    /// value was consumed.
    /// </summary>
    /// <param name="exception">The exception the converter threw.</param>
    /// <param name="tokenType">The <see cref="TokenType"/> captured before the read.</param>
    /// <param name="start">The <see cref="Start"/> captured before the read.</param>
    /// <returns><see langword="true"/> when the value can be skipped and reported; otherwise <see langword="false"/>.</returns>
    /// <remarks>Has no side effects, so it can serve as an exception filter.</remarks>
    internal bool CanRecoverInvalidValue(YamlException exception, YamlTokenType tokenType, Mark start)
        => Options.InvalidValueHandling != YamlInvalidValueHandling.FailFast
            && exception is not (SyntaxErrorException or SemanticErrorException)
            && _state.TokenType == tokenType
            && _state.Start.Index == start.Index;

    /// <summary>
    /// Skips the value the reader is positioned on and reports it as invalid. Call this after <see cref="CanRecoverInvalidValue"/>
    /// returned <see langword="true"/>. The value is captured as a <see cref="Model.YamlElement"/> only when a callback or
    /// finalizer consumes it.
    /// </summary>
    /// <param name="exception">The exception the converter threw.</param>
    /// <param name="declaringType">The CLR type being deserialized.</param>
    /// <param name="instance">The instance being populated, or <see langword="null"/> when it does not exist yet.</param>
    /// <param name="name">The YAML key of the member or constructor parameter.</param>
    /// <param name="targetType">The CLR type the value was meant for.</param>
    internal void RecoverInvalidValue(YamlException exception, Type declaringType, object? instance, string name, Type targetType)
    {
        if (_state.Tracker is not { } tracker)
        {
            Skip();
            return;
        }

        RecoverInvalidValueCore(tracker, exception, declaringType, instance, name, tracker.GetCurrentKeyStart(), targetType);
    }

    /// <summary>
    /// Skips the collection element or dictionary value the reader is positioned on and reports it as invalid, so that
    /// the caller drops it. Call this after <see cref="CanRecoverInvalidValue"/> returned <see langword="true"/>. The
    /// name of the item comes from the path tracker: the zero-based document index of a sequence element, or the key of
    /// a dictionary entry. A sequence element has no key, so its <see cref="YamlInvalidValue.KeyStart"/> is its own
    /// start.
    /// </summary>
    /// <param name="exception">The exception the converter threw.</param>
    /// <param name="declaringType">The CLR type of the collection being deserialized.</param>
    /// <param name="instance">
    /// The collection the item would have been added to, or <see langword="null"/> when the result is built afterwards,
    /// as for arrays and immutable collections.
    /// </param>
    /// <param name="targetType">The element or dictionary value type.</param>
    internal void RecoverInvalidItem(YamlException exception, Type declaringType, object? instance, Type targetType)
    {
        if (_state.Tracker is not { } tracker)
        {
            Skip();
            return;
        }

        var name = tracker.GetCurrentName(out var isElement);
        RecoverInvalidValueCore(tracker, exception, declaringType, instance, name, isElement ? Start : tracker.GetCurrentKeyStart(), targetType);
    }

    /// <summary>
    /// Reads a collection element or dictionary value with <paramref name="converter"/>. When the converter rejects it
    /// and <see cref="CanRecoverInvalidValue"/> allows recovery, the item is skipped and reported through
    /// <see cref="RecoverInvalidItem"/>, and the caller drops it. Any other exception propagates unchanged.
    /// </summary>
    /// <param name="converter">The converter of the element or value type.</param>
    /// <param name="itemType">
    /// The element or dictionary value type, passed to the converter and reported as the target type.
    /// </param>
    /// <param name="declaringType">The CLR type of the collection being deserialized.</param>
    /// <param name="instance">
    /// The collection the item is added to, or <see langword="null"/> when the result is built afterwards.
    /// </param>
    /// <param name="value">The value read, or <see langword="null"/> when the item was skipped.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> was read; <see langword="false"/> when the item was skipped
    /// and reported.
    /// </returns>
    /// <remarks>
    /// Under <see cref="YamlInvalidValueHandling.FailFast"/> this allocates nothing: the filter runs only when the
    /// converter throws.
    /// </remarks>
    internal bool TryReadItem(YamlConverter converter, Type itemType, Type declaringType, object? instance, out object? value)
    {
        var tokenType = TokenType;
        var start = Start;
        try
        {
            value = converter.Read(this, itemType);
            return true;
        }
        catch (YamlException exception) when (CanRecoverInvalidValue(exception, tokenType, start))
        {
            RecoverInvalidItem(exception, declaringType, instance, itemType);
            value = null;
            return false;
        }
    }

    private void RecoverInvalidValueCore(YamlPathTracker tracker, YamlException exception, Type declaringType, object? instance, string name, Mark keyStart, Type targetType)
    {
        var session = tracker.Session;
        var value = new YamlInvalidValue(SourceName, declaringType, instance, name, tracker.GetPath(), keyStart, Start, targetType, exception);
        if (session.IsReportingInvalidValues)
        {
            value.SetValue(Converters.YamlModelNodeConverter.ReadUnmappedNode(this, session.Anchors));
        }
        else
        {
            Skip();
        }

        session.Report(value);
    }

    /// <summary>
    /// Creates a <see cref="YamlException"/> located at the current token, for a converter to throw when it rejects a value.
    /// </summary>
    /// <param name="message">The message describing why the value is rejected.</param>
    /// <returns>An exception carrying <see cref="SourceName"/>, <see cref="Start"/> and <see cref="End"/>.</returns>
    /// <remarks>
    /// Thrown before any of the value is consumed, the exception is recoverable under
    /// <see cref="YamlSerializerOptions.InvalidValueHandling"/> modes other than <see cref="YamlInvalidValueHandling.FailFast"/>:
    /// the serializer skips the value and reports it instead of failing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public YamlException CreateException(string message)
    {
        ArgumentGuard.ThrowIfNull(message);
        return new YamlException(SourceName, Start, End, message);
    }

    /// <summary>
    /// Gets the current token type.
    /// </summary>
    public YamlTokenType TokenType => _state.TokenType;

    /// <summary>
    /// Gets the current scalar value when <see cref="TokenType"/> is <see cref="YamlTokenType.Scalar"/>.
    /// </summary>
    public string? ScalarValue => _state.ScalarValue;

    /// <summary>
    /// Gets the characters of the current scalar value when <see cref="TokenType"/> is <see cref="YamlTokenType.Scalar"/>,
    /// without creating a string for them; an empty span for any other token.
    /// </summary>
    /// <remarks>
    /// Prefer it to <see cref="ScalarValue"/> to parse a value that is not kept as a string, such as a number. The span
    /// is only valid until the reader advances: copy what must outlive the current token.
    /// </remarks>
    public ReadOnlySpan<char> ScalarValueSpan => _state.ScalarValueSpan;

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
    /// <remarks>
    /// The reader reports an alias token only under <see cref="YamlReferenceHandling.Preserve"/>, when the converter of
    /// the anchored node registered its object with <see cref="RegisterAnchor"/>. Any other alias is replayed as a copy
    /// of its anchored node, so the reader is then positioned on the first token of the copy.
    /// </remarks>
    /// <exception cref="YamlException">The alias is unknown.</exception>
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
    /// Attempts to resolve the current alias token into an anchored object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type the caller deserializes.</typeparam>
    /// <param name="value">When successful, receives the resolved anchored object.</param>
    /// <returns>
    /// <see langword="true"/> when the current token is an alias whose object is a <typeparamref name="T"/>, which is
    /// consumed; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// When the object registered for the alias is not a <typeparamref name="T"/>, as for a node read as a list and
    /// aliased as an array, the reader replays the anchored node instead: this method returns <see langword="false"/>
    /// and the reader is positioned on the first token of the copy, for the caller to read it as a <typeparamref name="T"/>.
    /// </remarks>
    /// <exception cref="YamlException">The alias is unknown, or refers to a node that contains it and cannot be replayed.</exception>
    public bool TryReadAlias<T>([MaybeNullWhen(false)] out T value)
    {
        if (TokenType == YamlTokenType.Alias && _state.ReferenceReader is { } references)
        {
            if (references.TryGet(Alias!, out var resolved) && resolved is T typed)
            {
                references.MarkResolved(resolved);
                Read();
                value = typed;
                return true;
            }

            ExpandAlias();
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Replays the anchored node of the current alias token, so that the reader is positioned on the first token of a
    /// copy of the node. Does nothing when the current token is not an alias.
    /// </summary>
    /// <remarks>
    /// This is for the nodes that are read as a copy even under <see cref="YamlReferenceHandling.Preserve"/>: the value
    /// of a merge key (<c>&lt;&lt;</c>), and a mapping key, whose equality is structural, so that two keys aliasing the
    /// same node are not one object. By default the reader already replays every alias, so this does nothing.
    /// </remarks>
    /// <exception cref="YamlException">The alias refers to a node that contains it.</exception>
    internal void ExpandAlias() => _state.ExpandAlias();

    /// <summary>
    /// Replaces the object registered for <paramref name="anchor"/> by <paramref name="replacement"/>, as when a
    /// converter promotes the collection it registered for an anchored node to another type while reading the node.
    /// </summary>
    /// <param name="anchor">The anchor of the node, without the <c>&amp;</c> prefix.</param>
    /// <param name="current">The object registered for the node.</param>
    /// <param name="replacement">The object that takes its place.</param>
    /// <returns>
    /// <see langword="false"/> when an alias already resolved to <paramref name="current"/>, which the replacement would
    /// leave as a stale reference, so the caller must not replace it; otherwise <see langword="true"/>, including when
    /// references are not preserved.
    /// </returns>
    internal bool TryReplaceAnchoredObject(string anchor, object current, object replacement)
        => _state.ReferenceReader is not { } references || references.TryReplace(anchor, current, replacement);

    /// <summary>Gets whether an alias resolved to <paramref name="value"/> under <see cref="YamlReferenceHandling.Preserve"/>.</summary>
    /// <param name="value">An object registered for an anchored node.</param>
    internal bool WasResolvedByAlias(object value) => _state.ReferenceReader is { } references && references.WasResolved(value);

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
    /// Reads the value of a YAML merge key (<c>&lt;&lt;</c>) and yields every merged key.
    /// </summary>
    /// <param name="mergeEnabled"><see langword="true"/> when merge keys are honored; otherwise the value is skipped and nothing is yielded.</param>
    /// <param name="explicitKeys">The keys already set on the object. Merged entries with one of these keys are skipped. May be <see langword="null"/>.</param>
    /// <returns>The merged keys. Each one leaves the reader positioned on that key's value, which the caller must read or skip before asking for the next key.</returns>
    /// <remarks>
    /// This is intended for generated code, which enumerates it while the reader is positioned on the value of a <c>&lt;&lt;</c> key.
    /// The value can be a mapping or a sequence of mappings, or aliases to them, which are merged as copies. An iterator keeps the generated reader free of a delegate, so it does not allocate a closure for objects without merge keys.
    /// </remarks>
    /// <exception cref="YamlException">The value is not a mapping or a sequence of mappings.</exception>
    public IEnumerable<string> ReadMergeKeys(bool mergeEnabled, HashSet<string>? explicitKeys)
    {
        if (!mergeEnabled)
        {
            Skip();
            yield break;
        }

        ExpandAlias();
        if (TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(this))
        {
            Read();
            yield break;
        }

        if (TokenType == YamlTokenType.StartMapping)
        {
            foreach (var key in ReadMergeMapping(explicitKeys))
            {
                yield return key;
            }

            yield break;
        }

        if (TokenType == YamlTokenType.StartSequence)
        {
            Read();
            while (TokenType != YamlTokenType.EndSequence)
            {
                ExpandAlias();
                if (TokenType != YamlTokenType.StartMapping)
                {
                    throw new YamlException(SourceName, Start, End, "Merge sequence entries must be mappings.");
                }

                foreach (var key in ReadMergeMapping(explicitKeys))
                {
                    yield return key;
                }
            }

            Read();
            yield break;
        }

        throw new YamlException(SourceName, Start, End, "Merge key value must be a mapping or a sequence of mappings.");
    }

    private IEnumerable<string> ReadMergeMapping(HashSet<string>? explicitKeys)
    {
        if (TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(this);
        }

        Read();
        while (TokenType != YamlTokenType.EndMapping)
        {
            if (TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(this);
            }

            var mergeKey = ScalarValue ?? string.Empty;
            var mergeKeyIsPlain = ScalarStyle == ScalarStyle.Plain;
            Read();
            if (mergeKeyIsPlain && string.Equals(mergeKey, "<<", StringComparison.Ordinal))
            {
                foreach (var nestedKey in ReadMergeKeys(mergeEnabled: true, explicitKeys))
                {
                    yield return nestedKey;
                }

                continue;
            }

            if (explicitKeys is not null && explicitKeys.Contains(mergeKey))
            {
                Skip();
                continue;
            }

            yield return mergeKey;
        }

        Read();
    }

    /// <summary>
    /// Buffers the current YAML node while extracting a discriminator value from its root mapping, and returns a reader
    /// that reads the node again.
    /// </summary>
    /// <param name="discriminatorPropertyName">The mapping key name to treat as a discriminator.</param>
    /// <param name="discriminatorValue">Receives the discriminator scalar value when present on the root mapping.</param>
    /// <returns>
    /// A reader over the node, which shares the reference anchor state of this reader and reports the positions of the
    /// node in its document. Call <see cref="Read"/> on it to position it on the first token of the node.
    /// </returns>
    /// <remarks>
    /// This method consumes the node from this reader and advances it past the node. It is what polymorphic
    /// deserialization uses to read a value once its type is known: the events of the node are kept and read again, so
    /// that the node is neither written back to text nor parsed a second time, as it is by
    /// <see cref="BufferCurrentNodeToStringAndFindDiscriminator"/> followed by <see cref="CreateReader"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="discriminatorPropertyName"/> is <see langword="null"/>.</exception>
    /// <exception cref="YamlException">The value of the discriminator key is not a scalar.</exception>
    public YamlReader BufferCurrentNodeAndFindDiscriminator(string discriminatorPropertyName, out string? discriminatorValue)
    {
        ArgumentGuard.ThrowIfNull(discriminatorPropertyName);

        var comparison = Options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        discriminatorValue = null;
        CaptureBufferedNode(discriminatorPropertyName);

        // The values of one type tend to have as many events as each other: the array of the last one fits the next.
        var events = new RecordedParser(_recordedEventCount);
        _state.PassAliasesThrough = true;
        try
        {
            RecordBufferedNode(events, discriminatorPropertyName, comparison, ref discriminatorValue);
        }
        finally
        {
            _state.PassAliasesThrough = false;
        }

        MovePastBufferedNode();
        _recordedEventCount = events.Count;
        return CreateBufferedReader(events);
    }

    /// <summary>
    /// Moves past the node that was just buffered, whose last token is the current one. The aliases of the node were
    /// passed through; the token that follows it is read as any other, so that an alias there is replayed.
    /// </summary>
    private void MovePastBufferedNode() => Read();

    /// <summary>
    /// Reads the current node to its last token and keeps its events, the aliases of the parser as they are: the reader
    /// of the recording resolves them against the anchors captured where the node starts, as the reader of buffered text does.
    /// </summary>
    private void RecordBufferedNode(RecordedParser events, string discriminatorPropertyName, StringComparison comparison, ref string? discriminatorValue)
    {
        // The entries of the root mapping are the nodes at depth 1, a key then its value: a scalar or an alias is a
        // node by itself, and a mapping or a sequence is one once the depth is back to 1.
        var isRootMapping = TokenType == YamlTokenType.StartMapping;
        var isKey = false;
        var isDiscriminatorValue = false;
        var depth = 0;
        for (; ; )
        {
            var tokenType = TokenType;
            if (isRootMapping && depth == 1 && tokenType != YamlTokenType.EndMapping)
            {
                if (isKey)
                {
                    isDiscriminatorValue = discriminatorValue is null
                        && tokenType == YamlTokenType.Scalar
                        && string.Equals(ScalarValue ?? string.Empty, discriminatorPropertyName, comparison);
                }
                else if (isDiscriminatorValue)
                {
                    if (tokenType != YamlTokenType.Scalar)
                    {
                        throw YamlThrowHelper.ThrowExpectedDiscriminatorScalar(this, discriminatorPropertyName);
                    }

                    discriminatorValue = ScalarValue ?? string.Empty;
                    isDiscriminatorValue = false;
                }
            }

            switch (tokenType)
            {
                case YamlTokenType.Scalar:
                case YamlTokenType.Alias:
                    break;

                case YamlTokenType.StartMapping:
                case YamlTokenType.StartSequence:
                    depth++;
                    break;

                case YamlTokenType.EndMapping:
                case YamlTokenType.EndSequence:
                    depth--;
                    break;

                default:
                    throw YamlThrowHelper.ThrowUnexpectedToken(this);
            }

            // An event the parser gives again for its next token is kept as a copy: see YamlReaderState.CurrentEvent.
            events.Add(_state.CurrentEvent!);
            if (depth == 0)
            {
                return;
            }

            if (isRootMapping && depth == 1)
            {
                // The start of the root mapping is followed by its first key; any other node at this depth just ended.
                isKey = tokenType == YamlTokenType.StartMapping || !isKey;
            }

            Read();
        }
    }

    /// <summary>Keeps the events recorded by <see cref="RecordBufferedNode"/> and gives them again, as the parser of their reader.</summary>
    private sealed class RecordedParser : IParser
    {
        private ParsingEvent[] _events;
        private int _next;

        public RecordedParser(int capacity) => _events = new ParsingEvent[Math.Max(capacity, 4)];

        public int Count { get; private set; }

        public ParsingEvent? Current { get; private set; }

        public void Add(ParsingEvent parsingEvent)
        {
            if (Count == _events.Length)
            {
                Array.Resize(ref _events, _events.Length * 2);
            }

            _events[Count++] = parsingEvent;
        }

        public bool MoveNext()
        {
            if (_next < Count)
            {
                Current = _events[_next++];
                return true;
            }

            Current = null;
            return false;
        }
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

        // The writer of the serializer, which keeps the text in a pooled buffer.
        var yamlWriter = YamlWriter.CreateBuffered(writer: null, reader.Options);
        string buffered;
        reader._state.PassAliasesThrough = true;
        try
        {
            WriteBufferedNode(reader, yamlWriter, comparer, discriminatorPropertyName, isRootMapping: true, ref discriminatorValue);
            buffered = yamlWriter.GetBufferedString(isDocument: false);
        }
        finally
        {
            reader._state.PassAliasesThrough = false;
            yamlWriter.ReleaseBuffer();
        }

        reader.MovePastBufferedNode();
        return buffered;
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

        var yamlWriter = YamlWriter.CreateBuffered(writer: null, reader.Options);
        string? unused = null;
        string buffered;
        reader._state.PassAliasesThrough = true;
        try
        {
            WriteBufferedNode(reader, yamlWriter, StringComparer.Ordinal, discriminatorPropertyName: string.Empty, isRootMapping: false, ref unused);
            buffered = yamlWriter.GetBufferedString(isDocument: false);
        }
        finally
        {
            reader._state.PassAliasesThrough = false;
            yamlWriter.ReleaseBuffer();
        }

        reader.MovePastBufferedNode();
        return buffered;
    }

    /// <remarks>
    /// The buffered YAML keeps the aliases of the node, so that <see cref="CreateReader"/> resolves them against the
    /// anchors captured here instead of copying them into the text.
    /// </remarks>
    private void CaptureBufferedNode(string? discriminatorPropertyName)
    {
        _state.BufferedDiscriminator = discriminatorPropertyName;
        _state.CaptureBufferedAnchors();

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

    /// <summary>Writes the current node, and leaves the reader on its last token.</summary>
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

                writer.WriteScalar(reader.ScalarValue, reader.ScalarStyle);
                return;

            case YamlTokenType.Alias:
                writer.WriteAlias(reader.Alias ?? throw new InvalidOperationException("Alias token did not provide an alias value."));
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
                    reader.Read();
                }
                writer.WriteEndSequence();
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
                    var key = reader.TokenType == YamlTokenType.Scalar ? reader.ScalarValue ?? string.Empty : null;
                    if (reader.TokenType != YamlTokenType.Scalar || reader.Anchor is not null || reader.Tag is not null)
                    {
                        writer.WriteStartMappingKey();
                        WriteBufferedNode(reader, writer, keyComparer, discriminatorPropertyName, isRootMapping: false, ref discriminatorValue);
                        writer.WriteEndMappingKey();
                    }
                    else
                    {
                        writer.WritePropertyName(key!);
                    }

                    reader.Read();

                    if (isRootMapping && key is not null && discriminatorValue is null && keyComparer.Equals(key, discriminatorPropertyName))
                    {
                        if (reader.TokenType != YamlTokenType.Scalar)
                        {
                            throw YamlThrowHelper.ThrowExpectedDiscriminatorScalar(reader, discriminatorPropertyName);
                        }

                        discriminatorValue = reader.ScalarValue ?? string.Empty;
                    }

                    WriteBufferedNode(reader, writer, keyComparer, discriminatorPropertyName, isRootMapping: false, ref discriminatorValue);
                    reader.Read();
                }
                writer.WriteEndMapping();
                return;

            default:
                throw YamlThrowHelper.ThrowUnexpectedToken(reader);
        }
    }

    internal sealed class YamlReaderState
    {
        private readonly IParser _parser;
        private readonly YamlAnchorTable _anchors;
        private readonly string? _discriminatorPropertyName;
        private readonly int _rootNesting;
        private int _nesting;
        private bool _hasBufferedAnchors;
        private Dictionary<string, YamlAnchorBinding>? _bufferedAnchors;
        private int _bufferedNesting;
        private AnchorAlias? _pendingAlias;
        private bool _skipAliasExpansion;

        // The current token is its type and the event it comes from: the token properties read the event rather than
        // copies of its content. An event the parser reuses (IReusedEventSource) keeps its content until the parser
        // moves, which is when the reader moves, so it describes the current token for as long as it is the current one.
        private YamlTokenType _tokenType;
        private ParsingEvent? _currentEvent;

        // _currentEvent when it is a scalar or the start of a mapping or sequence, the events that have a tag and an
        // anchor, and when it is a scalar: typed, so that the properties need no type test.
        private NodeEvent? _currentNode;
        private Scalar? _currentScalar;

        /// <param name="parser">The parser of the YAML.</param>
        /// <param name="anchors">The anchors of the YAML.</param>
        /// <param name="nesting">The number of mappings and sequences that enclose the YAML in the document it was buffered from.</param>
        /// <param name="sourceName">The source name reported in exceptions.</param>
        /// <param name="tracker">The path tracker, when reporting.</param>
        /// <param name="discriminatorPropertyName">The discriminator property name of the buffered polymorphic node this reader reads.</param>
        public YamlReaderState(IParser parser, YamlAnchorTable anchors, int nesting, string? sourceName, YamlPathTracker? tracker, string? discriminatorPropertyName)
        {
            _parser = parser;
            _anchors = anchors;
            _rootNesting = nesting;
            _nesting = nesting;
            _discriminatorPropertyName = discriminatorPropertyName;
            Tracker = tracker;
            _tokenType = YamlTokenType.None;
            SourceName = sourceName;
        }

        public void ReleaseInput() => (_parser as IReusedEventSource)?.ReleaseInput();

        // Every token property resolves a pending alias first: see ResolvePendingAlias.
        public YamlTokenType TokenType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pendingAlias is null ? _tokenType : ResolvePendingAlias()._tokenType;
        }

        public string? ScalarValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_pendingAlias is null ? this : ResolvePendingAlias()).GetScalarValue();
        }

        /// <summary>Gets the characters of the current scalar without creating its string; empty when the token is not a scalar.</summary>
        public ReadOnlySpan<char> ScalarValueSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_pendingAlias is null ? this : ResolvePendingAlias())._currentScalar is { } scalar ? scalar.ValueSpan : default;
        }

        /// <summary>
        /// Gets the value of the current scalar as a string. The event creates it the first time it is asked for: a
        /// plain scalar arrives as a slice of the input, and a value read as a number or a boolean never needs its string.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? GetScalarValue() => _currentScalar?.Value;

        public ScalarStyle ScalarStyle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_pendingAlias is null ? this : ResolvePendingAlias())._currentScalar is { } scalar ? scalar.Style : ScalarStyle.Any;
        }

        public string? Tag => (_pendingAlias is null ? this : ResolvePendingAlias())._currentNode?.Tag;
        public string? Anchor => (_pendingAlias is null ? this : ResolvePendingAlias())._currentNode?.Anchor;

        public string? Alias
        {
            get
            {
                var state = _pendingAlias is null ? this : ResolvePendingAlias();
                return state._tokenType == YamlTokenType.Alias ? ((AnchorAlias)state._currentEvent!).Value : null;
            }
        }

        public Mark Start
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_pendingAlias is null ? this : ResolvePendingAlias())._currentEvent is { } current ? current.Start : default;
        }

        public Mark End
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_pendingAlias is null ? this : ResolvePendingAlias())._currentEvent is { } current ? current.End : default;
        }

        /// <summary>Gets the current event, as an object that stays valid once the reader moved: callers keep it in the nodes they build.</summary>
        public ParsingEvent? CurrentEvent
        {
            get
            {
                if (_pendingAlias is not null)
                {
                    ResolvePendingAlias();
                }

                if (_currentEvent is { IsReused: true } reused)
                {
                    // The copy takes the place of the reused event, which the parser is about to give other content.
                    _currentEvent = reused.Detach();
                    _currentNode = _currentEvent as NodeEvent;
                    _currentScalar = _currentEvent as Scalar;
                }

                return _currentEvent;
            }
        }
        public YamlReferenceReader? ReferenceReader => _anchors.Scope.References;
        public string? SourceName { get; }
        public YamlPathTracker? Tracker { get; }

        /// <summary>The discriminator property name of the node most recently buffered from this reader; used to seed the reader of the re-parse.</summary>
        public string? BufferedDiscriminator { get; set; }

        /// <summary>Gets or sets a value indicating whether aliases of the parser are reported as they are, while a node is buffered to text.</summary>
        public bool PassAliasesThrough { get; set; }

        /// <summary>
        /// Gets a value indicating whether <paramref name="memberName"/>, read in the root mapping of a buffered polymorphic node,
        /// is the type discriminator property. The discriminator is consumed by polymorphism rather than unmapped.
        /// </summary>
        /// <remarks><paramref name="comparison"/> must match the comparison used to find the discriminator when the node was buffered.</remarks>
        public bool IsDiscriminatorMember(string memberName, StringComparison comparison)
            => _discriminatorPropertyName is not null && _nesting == _rootNesting + 1 && string.Equals(memberName, _discriminatorPropertyName, comparison);

        /// <summary>Captures the anchors and the nesting of the node about to be buffered from the current token.</summary>
        public void CaptureBufferedAnchors()
        {
            _hasBufferedAnchors = true;
            _bufferedAnchors = _anchors.CaptureBindings();
            _bufferedNesting = TokenType is YamlTokenType.StartMapping or YamlTokenType.StartSequence ? _nesting - 1 : _nesting;
        }

        /// <summary>Creates the anchors of a reader over the node buffered last, or, when no node was buffered, over YAML that starts at the current token.</summary>
        public YamlAnchorTable CreateBufferedAnchors(out int nesting)
        {
            if (!_hasBufferedAnchors)
            {
                CaptureBufferedAnchors();
            }

            nesting = _bufferedNesting;

            // Each table owns its bindings, as a fragment binds the anchors it parses again.
            var bindings = _bufferedAnchors is null ? null : new Dictionary<string, YamlAnchorBinding>(_bufferedAnchors, StringComparer.Ordinal);
            return _anchors.CreateFragment(bindings);
        }

        public bool Read()
        {
            _pendingAlias = null;
            var current = _anchors.Read(_parser, PassAliasesThrough, _skipAliasExpansion);
            if (_anchors.IsPendingAlias && current is AnchorAlias alias)
            {
                _pendingAlias = alias;
                return true;
            }

            if (!Apply(current))
            {
                return false;
            }

            Tracker?.OnToken(_tokenType, GetScalarValue(), current!.Start);
            return true;
        }

        /// <summary>
        /// Decides what an alias that has no registered object yet stands for, once the converter looks at it.
        /// </summary>
        /// <remarks>
        /// Under <see cref="YamlReferenceHandling.Preserve"/>, the converter of an anchored mapping or sequence registers
        /// its object after it read the start token, so it may not be registered yet when the reader reads an alias to it
        /// as the first item. The alias is reported when the object is registered by the time a token property is read,
        /// and replayed otherwise.
        /// </remarks>
        private YamlReaderState ResolvePendingAlias()
        {
            var alias = _pendingAlias!;
            _pendingAlias = null;
            Apply(_anchors.ResolvePendingAlias(alias) ? alias : _anchors.Read(_parser, PassAliasesThrough));
            Tracker?.OnToken(_tokenType, GetScalarValue(), _currentEvent is { } current ? current.Start : Mark.Empty);
            return this;
        }

        /// <summary>Replays the anchored node of the current alias token: see <see cref="YamlReader.ExpandAlias"/>.</summary>
        public void ExpandAlias()
        {
            if (TokenType != YamlTokenType.Alias || CurrentEvent is not AnchorAlias alias)
            {
                return;
            }

            _anchors.ExpandCurrentAlias(alias);

            // The first token of the copy takes the place of the alias in the path.
            Tracker?.OnAliasExpanded();
            Read();
        }

        /// <summary>Makes <paramref name="current"/> the current token; <see langword="null"/> is the end of the stream.</summary>
        private bool Apply(ParsingEvent? current)
        {
            _currentEvent = current;
            if (current is null)
            {
                _currentNode = null;
                _currentScalar = null;
                _tokenType = YamlTokenType.None;
                return false;
            }

            switch (current.Kind)
            {
                case EventType.YAML_SCALAR_EVENT:
                    var scalar = (Scalar)current;
                    _currentNode = scalar;
                    _currentScalar = scalar;
                    _tokenType = YamlTokenType.Scalar;
                    return true;

                case EventType.YAML_MAPPING_START_EVENT:
                    _currentNode = (MappingStart)current;
                    _currentScalar = null;
                    EnterContainer(current);
                    _tokenType = YamlTokenType.StartMapping;
                    return true;

                case EventType.YAML_MAPPING_END_EVENT:
                    _currentNode = null;
                    _currentScalar = null;
                    _nesting--;
                    _tokenType = YamlTokenType.EndMapping;
                    return true;

                case EventType.YAML_SEQUENCE_START_EVENT:
                    _currentNode = (SequenceStart)current;
                    _currentScalar = null;
                    EnterContainer(current);
                    _tokenType = YamlTokenType.StartSequence;
                    return true;

                case EventType.YAML_SEQUENCE_END_EVENT:
                    _currentNode = null;
                    _currentScalar = null;
                    _nesting--;
                    _tokenType = YamlTokenType.EndSequence;
                    return true;

                case EventType.YAML_ALIAS_EVENT:
                    _currentNode = null;
                    _currentScalar = null;
                    _tokenType = YamlTokenType.Alias;
                    return true;

                default:
                    throw new YamlException(SourceName, current.Start, current.End, $"Unexpected parsing event '{current.GetType().Name}'.");
            }
        }

        private void EnterContainer(ParsingEvent start)
        {
            // The parser bounds the depth of what it parses; a replayed alias can nest deeper.
            if (!_skipAliasExpansion && _anchors.IsReplayed && _nesting >= _anchors.Scope.MaxDepth)
            {
                throw YamlDepthHelper.CreateMaxDepthExceededException(_anchors.Scope.MaxDepth, start.Start, start.End, SourceName);
            }

            _nesting++;
        }

        public void Skip()
        {
            _skipAliasExpansion = true;
            try
            {
                if (_pendingAlias is { } alias)
                {
                    _pendingAlias = null;
                    Apply(alias);
                    Tracker?.OnToken(_tokenType, GetScalarValue(), alias.Start);
                }
                if (_tokenType is YamlTokenType.StartMapping or YamlTokenType.StartSequence)
                {
                    SkipContainer();
                }
            }
            finally
            {
                _skipAliasExpansion = false;
            }
            // The next value is observed normally, even if it is itself an alias.
            Read();
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

        }
    }
}
