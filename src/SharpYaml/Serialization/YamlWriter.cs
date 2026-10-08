// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using SharpYaml.Serialization.References;

namespace SharpYaml.Serialization;

/// <summary>
/// Writes YAML tokens for use by <see cref="YamlConverter"/> implementations.
/// </summary>
public sealed class YamlWriter : YamlReaderWriterBase
{
    private readonly TextWriter? _writer;
    private readonly StringBuilder? _stringBuilder;
    private readonly YamlReferenceWriter? _referenceWriter;
    private readonly StringBuilder _indentBuilder = new();
    private ContainerFrame[] _frames = new ContainerFrame[8];
    private int _depth;
    private string? _pendingAnchor;
    private string? _pendingTag;
    private bool _hasWrittenChar;
    private char _lastWrittenChar;
    private YamlSequenceItemStyle _blockSequenceMappingStyle;
    private YamlSequenceItemStyle _blockSequenceSequenceStyle;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlWriter"/> class.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="options">The serializer options used for formatting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public YamlWriter(TextWriter writer, YamlSerializerOptions? options = null)
        : base(options ?? YamlSerializerOptions.Default)
    {
        ArgumentGuard.ThrowIfNull(writer);
        _writer = writer;
        _referenceWriter = Options.ReferenceHandling != YamlReferenceHandling.None ? new YamlReferenceWriter() : null;
        InitializeFormattingState();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlWriter"/> class that writes directly to a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="stringBuilder">The destination string builder.</param>
    /// <param name="options">The serializer options used for formatting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stringBuilder"/> is <see langword="null"/>.</exception>
    public YamlWriter(StringBuilder stringBuilder, YamlSerializerOptions? options = null)
        : base(options ?? YamlSerializerOptions.Default)
    {
        ArgumentGuard.ThrowIfNull(stringBuilder);
        _stringBuilder = stringBuilder;
        _referenceWriter = Options.ReferenceHandling != YamlReferenceHandling.None ? new YamlReferenceWriter() : null;
        InitializeFormattingState();
    }

    internal YamlWriter(TextWriter writer, YamlSerializerOptions options, YamlReferenceWriter referenceWriter)
        : base(options)
    {
        _writer = writer;
        _referenceWriter = referenceWriter;
        InitializeFormattingState();
    }

    internal YamlReferenceWriter? ReferenceWriter => _referenceWriter;

    internal bool EndsWithNewLine => _hasWrittenChar && _lastWrittenChar == '\n';

    /// <summary>
    /// Temporarily overrides how nested block collections are emitted when they appear as items in block sequences.
    /// </summary>
    /// <param name="mappingStyle">The mapping style override, or <see cref="YamlSequenceItemStyle.Default"/> to keep the current mapping style.</param>
    /// <param name="sequenceStyle">The sequence style override, or <see cref="YamlSequenceItemStyle.Default"/> to keep the current sequence style.</param>
    /// <returns>A scope that restores the previous styles when disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mappingStyle"/> or <paramref name="sequenceStyle"/> is not a defined <see cref="YamlSequenceItemStyle"/>.</exception>
    public BlockSequenceItemStyleScope PushBlockSequenceItemStyle(YamlSequenceItemStyle mappingStyle, YamlSequenceItemStyle sequenceStyle)
    {
        YamlSerializerOptions.ValidateSequenceItemStyle(mappingStyle, nameof(mappingStyle));
        YamlSerializerOptions.ValidateSequenceItemStyle(sequenceStyle, nameof(sequenceStyle));

        var scope = new BlockSequenceItemStyleScope(this, _blockSequenceMappingStyle, _blockSequenceSequenceStyle);
        if (mappingStyle != YamlSequenceItemStyle.Default)
        {
            _blockSequenceMappingStyle = mappingStyle;
        }

        if (sequenceStyle != YamlSequenceItemStyle.Default)
        {
            _blockSequenceSequenceStyle = sequenceStyle;
        }

        return scope;
    }

    /// <summary>
    /// Attempts to preserve object references by writing an alias when <paramref name="value"/> was previously
    /// anchored, or by writing an anchor for the next value when it is seen for the first time.
    /// </summary>
    /// <param name="value">The value to track.</param>
    /// <returns><see langword="true"/> when an alias was written and no further output for this value is required.</returns>
    /// <remarks>
    /// This method is intended for use by generated serializers and custom converters. It is a no-op unless
    /// <see cref="YamlSerializerOptions.ReferenceHandling"/> is <see cref="YamlReferenceHandling.Preserve"/> or
    /// <see cref="YamlReferenceHandling.PreserveMinimal"/>.
    /// </remarks>
    public bool TryWriteReference(object? value)
    {
        if (_referenceWriter is null || value is null)
        {
            return false;
        }

        // Match the reflection pipeline behavior: do not anchor scalar strings and do not track value types.
        if (value is string || value.GetType().IsValueType)
        {
            return false;
        }

        if (_referenceWriter.TryGetAnchor(value, out var existing))
        {
            WriteAlias(existing);
            return true;
        }

        var anchor = _referenceWriter.GetOrAddAnchor(value);
        if (anchor is not null)
        {
            WriteAnchor(anchor);
        }
        return false;
    }

    /// <summary>
    /// Writes a YAML tag for the next value.
    /// </summary>
    /// <param name="tag">The YAML tag, such as <c>!dog</c>.</param>
    public void WriteTag(string tag)
    {
        ArgumentGuard.ThrowIfNull(tag);
        if (tag.Length == 0)
        {
            throw new ArgumentException("Tag cannot be empty.", nameof(tag));
        }

        _pendingTag = tag;
    }

    /// <summary>
    /// Writes a YAML anchor for the next value.
    /// </summary>
    public void WriteAnchor(string anchor)
    {
        ArgumentGuard.ThrowIfNull(anchor);
        if (anchor.Length == 0)
        {
            throw new ArgumentException("Anchor cannot be empty.", nameof(anchor));
        }

        _pendingAnchor = anchor;
    }

    /// <summary>
    /// Writes a YAML alias value (a reference to an anchor).
    /// </summary>
    public void WriteAlias(string alias)
    {
        ArgumentGuard.ThrowIfNull(alias);
        if (alias.Length == 0)
        {
            throw new ArgumentException("Alias cannot be empty.", nameof(alias));
        }

        WriteValuePrefixForAlias();
        Write('*');
        Write(alias);
        CompleteValueAfterScalar();
    }

    /// <summary>
    /// Writes the start of a mapping.
    /// </summary>
    /// <exception cref="YamlException">The configured maximum nesting depth was exceeded.</exception>
    public void WriteStartMapping()
    {
        PushContainer(ContainerKind.Mapping);
    }

    /// <summary>
    /// Writes the end of a mapping.
    /// </summary>
    public void WriteEndMapping()
    {
        var frame = PopFrame(ContainerKind.Mapping);
        if (!frame.HasContent)
        {
            WriteEmptyContainerInline(ContainerKind.Mapping, frame.PendingStart);
        }

        CompleteValueAfterContainer();
    }

    /// <summary>
    /// Writes the start of a sequence.
    /// </summary>
    /// <exception cref="YamlException">The configured maximum nesting depth was exceeded.</exception>
    public void WriteStartSequence()
    {
        PushContainer(ContainerKind.Sequence);
    }

    /// <summary>
    /// Writes the end of a sequence.
    /// </summary>
    public void WriteEndSequence()
    {
        var frame = PopFrame(ContainerKind.Sequence);
        if (!frame.HasContent)
        {
            WriteEmptyContainerInline(ContainerKind.Sequence, frame.PendingStart);
        }

        CompleteValueAfterContainer();
    }

    /// <summary>
    /// Writes a mapping key.
    /// </summary>
    /// <param name="name">The key name.</param>
    /// <exception cref="InvalidOperationException">The writer is not positioned within a mapping key.</exception>
    public void WritePropertyName(string name)
    {
        ArgumentGuard.ThrowIfNull(name);
        if (_depth == 0 || _frames[_depth - 1].Kind != ContainerKind.Mapping)
        {
            throw new InvalidOperationException("Property names can only be written inside a mapping.");
        }

        ref var frame = ref _frames[_depth - 1];
        if (!frame.ExpectingKey)
        {
            throw new InvalidOperationException("A property name cannot be written when a value is expected.");
        }

        var startedCompact = EnsureContainerStarted(ref frame);

        if (frame.HasContent && !frame.SuppressNextSeparatorNewline)
        {
            WriteNewLine();
        }
        frame.SuppressNextSeparatorNewline = false;

        if (!startedCompact)
        {
            WriteIndent();
        }
        WriteScalarCore(name, isKey: true);
        Write(':');

        frame.HasContent = true;
        frame.ExpectingKey = false;
    }

    /// <summary>
    /// Writes a scalar value.
    /// </summary>
    public void WriteScalar(string? value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);

        if (value is null)
        {
            Write("null");
            CompleteValueAfterScalar();
            return;
        }

        WriteScalarCore(value, isKey: false);
        CompleteValueAfterScalar();
    }

    /// <summary>
    /// Writes a CLR string value, quoting ambiguous YAML scalars when configured.
    /// </summary>
    /// <param name="value">The string value to write.</param>
    public void WriteString(string? value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);

        if (value is null)
        {
            Write("null");
            CompleteValueAfterScalar();
            return;
        }

        WriteStringCore(value.AsSpan(), isKey: false);
        CompleteValueAfterScalar();
    }

    /// <summary>
    /// Writes a scalar value from a character span.
    /// </summary>
    /// <param name="value">The scalar text.</param>
    public void WriteScalar(ReadOnlySpan<char> value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);
        WriteScalarCore(value, isKey: false);
        CompleteValueAfterScalar();
    }

    /// <summary>
    /// Writes a string scalar using a specific requested <see cref="SharpYaml.ScalarStyle"/>.
    /// </summary>
    /// <param name="value">The scalar text, or <see langword="null"/> to write a null scalar.</param>
    /// <param name="style">The requested scalar style.</param>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="WriteScalar(string?)"/> and <see cref="WriteString(string?)"/>, this overload lets a
    /// custom <see cref="YamlConverter{T}"/> force a specific scalar style for a value, e.g.
    /// <see cref="SharpYaml.ScalarStyle.Literal"/> block style for multiline content that should stay
    /// human-readable on disk:
    /// <code>
    /// public override void Write(YamlWriter writer, string value) =>
    ///     writer.WriteScalar(value, ScalarStyle.Literal);
    /// </code>
    /// </para>
    /// <para>
    /// When the requested style cannot safely represent <paramref name="value"/> without corrupting it on
    /// read-back, this method falls back to a style that can:
    /// <list type="bullet">
    /// <item><see cref="SharpYaml.ScalarStyle.Plain"/> falls back to <see cref="SharpYaml.ScalarStyle.DoubleQuoted"/>
    /// when the value is not plain-safe (e.g. contains a line break or a leading/trailing space).</item>
    /// <item><see cref="SharpYaml.ScalarStyle.SingleQuoted"/> falls back to
    /// <see cref="SharpYaml.ScalarStyle.DoubleQuoted"/> when the value contains a control character other than
    /// a line break (single-quoted scalars have no escape mechanism for those).</item>
    /// <item><see cref="SharpYaml.ScalarStyle.Literal"/> and <see cref="SharpYaml.ScalarStyle.Folded"/> fall back
    /// to <see cref="SharpYaml.ScalarStyle.DoubleQuoted"/> when the value contains a character that cannot appear
    /// unescaped in block scalar content (e.g. most control characters).</item>
    /// <item><see cref="SharpYaml.ScalarStyle.Folded"/> additionally falls back to
    /// <see cref="SharpYaml.ScalarStyle.Literal"/> when the content has a line break between two non-blank lines
    /// that YAML's folding rule would otherwise collapse into a space on read-back.</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="style"/> is not a defined <see cref="SharpYaml.ScalarStyle"/>.</exception>
    public void WriteScalar(string? value, ScalarStyle style)
    {
        if (value is null)
        {
            WriteScalar((string?)null);
            return;
        }

        switch (style)
        {
            case ScalarStyle.Any:
                WriteScalar(value);
                return;
            case ScalarStyle.Plain:
                WritePlainStyle(value);
                return;
            case ScalarStyle.SingleQuoted:
                WriteSingleQuotedStyle(value);
                return;
            case ScalarStyle.DoubleQuoted:
                WriteDoubleQuotedStyle(value);
                return;
            case ScalarStyle.Literal:
                WriteBlockStyle(value, folded: false);
                return;
            case ScalarStyle.Folded:
                WriteBlockStyle(value, folded: true);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(style), style, null);
        }
    }

    /// <summary>
    /// Writes a boolean scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(bool value)
    {
        WritePlainScalar(value ? "true" : "false");
    }

    /// <summary>
    /// Writes an 8-bit unsigned integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(byte value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes an 8-bit signed integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(sbyte value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a 16-bit signed integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(short value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(ushort value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a 32-bit signed integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(int value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a 32-bit unsigned integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(uint value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a 64-bit signed integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(long value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(ulong value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a decimal scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(decimal value)
    {
        WriteFormattableScalar(value, format: default, plainSafe: true);
    }

    /// <summary>
    /// Writes a platform-sized signed integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(nint value)
    {
        WritePlainScalar(((long)value).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a platform-sized unsigned integer scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(nuint value)
    {
        WritePlainScalar(((ulong)value).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Writes a character scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(char value)
    {
        Span<char> span = stackalloc char[1];
        span[0] = value;
        WriteScalar(span);
    }

    /// <summary>
    /// Writes a double-precision floating-point scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            WritePlainScalar(".inf");
            return;
        }

        if (double.IsNegativeInfinity(value))
        {
            WritePlainScalar("-.inf");
            return;
        }

        if (double.IsNaN(value))
        {
            WritePlainScalar(".nan");
            return;
        }

        WriteFormattableScalar(value, format: "R", plainSafe: true);
    }

    /// <summary>
    /// Writes a single-precision floating-point scalar value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteScalar(float value)
    {
        if (float.IsPositiveInfinity(value))
        {
            WritePlainScalar(".inf");
            return;
        }

        if (float.IsNegativeInfinity(value))
        {
            WritePlainScalar("-.inf");
            return;
        }

        if (float.IsNaN(value))
        {
            WritePlainScalar(".nan");
            return;
        }

        WriteFormattableScalar(value, format: "R", plainSafe: true);
    }

    /// <summary>
    /// Writes a scalar value for a span-formattable type using invariant culture formatting.
    /// </summary>
    /// <typeparam name="T">The value type to write.</typeparam>
    /// <param name="value">The value to write.</param>
    public void WriteScalar<T>(T value)
        where T : IFormattable
    {
        WriteFormattableScalar(value, format: default, plainSafe: false);
    }

    /// <summary>
    /// Writes a null scalar.
    /// </summary>
    public void WriteNullValue() => WriteScalar(null);

    private void WriteValuePrefixForScalar()
    {
        if (_depth == 0)
        {
            return;
        }

        ref var frame = ref _frames[_depth - 1];
        var startedCompact = EnsureContainerStarted(ref frame);

        if (frame.Kind == ContainerKind.Mapping)
        {
            if (frame.ExpectingKey)
            {
                throw new InvalidOperationException("A scalar value cannot be written when a key is expected.");
            }

            Write(' ');
            return;
        }

        if (frame.HasContent && !frame.SuppressNextSeparatorNewline)
        {
            WriteNewLine();
        }
        frame.SuppressNextSeparatorNewline = false;

        if (!startedCompact)
        {
            WriteIndent();
        }
        Write("- ");
        frame.HasContent = true;
    }

    private void WriteValuePrefixForAlias()
    {
        if (_depth == 0)
        {
            return;
        }

        ref var frame = ref _frames[_depth - 1];
        var startedCompact = EnsureContainerStarted(ref frame);

        if (frame.Kind == ContainerKind.Mapping)
        {
            if (frame.ExpectingKey)
            {
                throw new InvalidOperationException("An alias value cannot be written when a key is expected.");
            }

            Write(' ');
            return;
        }

        if (frame.HasContent && !frame.SuppressNextSeparatorNewline)
        {
            WriteNewLine();
        }
        frame.SuppressNextSeparatorNewline = false;

        if (!startedCompact)
        {
            WriteIndent();
        }
        Write("- ");
        frame.HasContent = true;
    }

    private void WriteNodeProperties(bool writeLeadingSpace, bool writeTrailingSpace)
    {
        if (_pendingAnchor is null && _pendingTag is null)
        {
            return;
        }

        if (writeLeadingSpace)
        {
            Write(' ');
        }

        var wroteAny = false;
        if (_pendingAnchor is not null)
        {
            Write('&');
            Write(_pendingAnchor);
            wroteAny = true;
        }

        if (_pendingTag is not null)
        {
            if (wroteAny)
            {
                Write(' ');
            }

            Write(_pendingTag);
            wroteAny = true;
        }

        _pendingAnchor = null;
        _pendingTag = null;

        if (writeTrailingSpace && wroteAny)
        {
            Write(' ');
        }
    }

    private bool EnsureContainerStarted(ref ContainerFrame frame)
    {
        if (frame.PendingStart == PendingStartKind.None)
        {
            return false;
        }

        var pendingStart = frame.PendingStart;
        frame.PendingStart = PendingStartKind.None;
        if (pendingStart == PendingStartKind.SequenceItemCompact)
        {
            Write(' ');
            return true;
        }

        WriteNewLine();
        return false;
    }

    private void CompleteValueAfterScalar()
    {
        if (_depth == 0)
        {
            return;
        }

        ref var frame = ref _frames[_depth - 1];
        if (frame.Kind == ContainerKind.Mapping)
        {
            frame.ExpectingKey = true;
        }
    }

    private void CompleteValueAfterContainer()
    {
        if (_depth == 0)
        {
            return;
        }

        ref var frame = ref _frames[_depth - 1];
        if (frame.Kind == ContainerKind.Mapping)
        {
            frame.ExpectingKey = true;
        }
    }

    private void PushContainer(ContainerKind kind)
    {
        if (_depth >= Options.EffectiveMaxDepth)
        {
            throw YamlDepthHelper.CreateMaxDepthExceededException(Options.EffectiveMaxDepth);
        }

        PendingStartKind pendingStart;

        if (_depth == 0)
        {
            if (_pendingAnchor is not null || _pendingTag is not null)
            {
                // Root node properties must be followed by a newline before content.
                WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: false);
                pendingStart = PendingStartKind.Root;
            }
            else
            {
                pendingStart = PendingStartKind.None;
            }
        }
        else
        {
            ref var parent = ref _frames[_depth - 1];
            var parentStartedCompact = EnsureContainerStarted(ref parent);

            if (parent.Kind == ContainerKind.Mapping)
            {
                if (parent.ExpectingKey)
                {
                    throw new InvalidOperationException("A container value cannot be written when a key is expected.");
                }

                pendingStart = PendingStartKind.MappingValue;
                if (_pendingAnchor is not null || _pendingTag is not null)
                {
                    WriteNodeProperties(writeLeadingSpace: true, writeTrailingSpace: false);
                }
            }
            else
            {
                if (parent.HasContent && !parent.SuppressNextSeparatorNewline)
                {
                    WriteNewLine();
                }
                parent.SuppressNextSeparatorNewline = false;

                if (!parentStartedCompact)
                {
                    WriteIndent();
                }
                Write('-');
                if (_pendingAnchor is not null || _pendingTag is not null)
                {
                    WriteNodeProperties(writeLeadingSpace: true, writeTrailingSpace: false);
                }
                parent.HasContent = true;
                pendingStart = ShouldCompactSequenceItem(kind)
                    ? PendingStartKind.SequenceItemCompact
                    : PendingStartKind.SequenceItem;
            }
        }

        if (_depth == _frames.Length)
        {
            Array.Resize(ref _frames, _frames.Length * 2);
        }

        _frames[_depth++] = new ContainerFrame(kind, pendingStart);
    }

    private ContainerFrame PopFrame(ContainerKind expectedKind)
    {
        if (_depth == 0)
        {
            throw new InvalidOperationException("No container is open.");
        }

        var frame = _frames[--_depth];
        if (frame.Kind != expectedKind)
        {
            throw new InvalidOperationException($"Mismatched container end. Expected '{expectedKind}' but was '{frame.Kind}'.");
        }

        return frame;
    }

    private void WriteEmptyContainerInline(ContainerKind kind, PendingStartKind pendingStart)
    {
        if ((pendingStart == PendingStartKind.None || pendingStart == PendingStartKind.Root) && _depth == 0)
        {
            Write(kind == ContainerKind.Mapping ? "{}" : "[]");
            return;
        }

        Write(' ');
        Write(kind == ContainerKind.Mapping ? "{}" : "[]");
    }

    private void WriteIndent()
    {
        if (!Options.WriteIndented)
        {
            return;
        }

        var indentLevel = Math.Max(0, _depth - 1);
        if (indentLevel == 0)
        {
            return;
        }

        var spaces = Options.IndentSize * indentLevel;
        if (_indentBuilder.Length != spaces)
        {
            _indentBuilder.Clear();
            _indentBuilder.Append(' ', spaces);
        }

        Write(_indentBuilder);
    }

    private void WriteNewLine()
    {
        Write('\n');
    }

    private void InitializeFormattingState()
    {
        _blockSequenceMappingStyle = ResolveOptionStyle(Options.BlockSequenceMappingStyle, YamlSequenceItemStyle.Compact);
        _blockSequenceSequenceStyle = ResolveOptionStyle(Options.BlockSequenceSequenceStyle, YamlSequenceItemStyle.Expanded);
    }

    private bool ShouldCompactSequenceItem(ContainerKind kind)
    {
        return kind switch
        {
            ContainerKind.Mapping => _blockSequenceMappingStyle == YamlSequenceItemStyle.Compact,
            ContainerKind.Sequence => _blockSequenceSequenceStyle == YamlSequenceItemStyle.Compact,
            _ => false,
        };
    }

    private void RestoreBlockSequenceItemStyle(YamlSequenceItemStyle mappingStyle, YamlSequenceItemStyle sequenceStyle)
    {
        _blockSequenceMappingStyle = mappingStyle;
        _blockSequenceSequenceStyle = sequenceStyle;
    }

    private static YamlSequenceItemStyle ResolveOptionStyle(YamlSequenceItemStyle style, YamlSequenceItemStyle fallback)
        => style == YamlSequenceItemStyle.Default ? fallback : style;

    private void WriteFormattableScalar<T>(T value, ReadOnlySpan<char> format, bool plainSafe)
        where T : IFormattable
    {
#if !NETSTANDARD2_0
        if (value is ISpanFormattable spanFormattable)
        {
            Span<char> buffer = stackalloc char[64];
            if (!spanFormattable.TryFormat(buffer, out var written, format, CultureInfo.InvariantCulture))
            {
                throw new InvalidOperationException($"Unable to format scalar value of type '{typeof(T)}'.");
            }

            if (plainSafe)
            {
                WritePlainScalar(buffer[..written]);
                return;
            }

            WriteScalar(buffer[..written]);
            return;
        }
#endif

        var formatString = format.Length == 0
            ? null
#if NETSTANDARD2_0
            : format.ToString();
#else
            : new string(format);
#endif
        var text = value.ToString(formatString, CultureInfo.InvariantCulture);
        if (text is null)
        {
            throw new InvalidOperationException($"Unable to format scalar value of type '{typeof(T)}'.");
        }

        if (plainSafe)
        {
            WritePlainScalar(text);
            return;
        }

        WriteScalar(text);
    }

    private void WriteScalarCore(string value, bool isKey)
    {
        WriteScalarCore(value.AsSpan(), isKey);
    }

    private void WriteScalarCore(ReadOnlySpan<char> value, bool isKey)
    {
        if (value.Length == 0)
        {
            Write("''");
            return;
        }

        if (IsPlainSafe(value, isKey))
        {
            Write(value);
            return;
        }

        Write('"');
        WriteEscaped(value);
        Write('"');
    }

    private void WriteStringCore(ReadOnlySpan<char> value, bool isKey)
    {
        if (value.Length == 0)
        {
            Write("''");
            return;
        }

        if (!isKey && Options.ScalarStylePreferences.MultilineStringStyle != ScalarStyle.Any && ContainsLineBreak(value))
        {
            // The caller (WriteString) already wrote the value prefix/node properties, so use the prefix-less
            // core writer directly here rather than WriteBlockStyle, which would write them a second time.
            // value is passed through as a span (no ToString() allocation).
            WriteBlockStyleCore(value, folded: Options.ScalarStylePreferences.MultilineStringStyle == ScalarStyle.Folded);
            return;
        }

        if (ShouldQuoteAmbiguousScalar(value))
        {
            Write('"');
            WriteEscaped(value);
            Write('"');
            return;
        }

        WriteScalarCore(value, isKey);
    }

    private static bool ContainsLineBreak(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (c is '\n' or '\r')
            {
                return true;
            }
        }

        return false;
    }

    private void WritePlainStyle(string value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);
        WritePlainStyleCore(value);
        CompleteValueAfterScalar();
    }

    private void WritePlainStyleCore(string value)
    {
        if (!IsPlainSafe(value.AsSpan(), isKey: false))
        {
            WriteDoubleQuotedStyleCore(value);
            return;
        }

        Write(value);
    }

    private void WriteSingleQuotedStyle(string value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);
        WriteSingleQuotedStyleCore(value);
        CompleteValueAfterScalar();
    }

    private void WriteSingleQuotedStyleCore(string value)
    {
        if (!IsSingleQuotedSafe(value))
        {
            WriteDoubleQuotedStyleCore(value);
            return;
        }

        Write('\'');
        foreach (var c in value)
        {
            if (c == '\'')
            {
                Write("''");
            }
            else
            {
                Write(c);
            }
        }
        Write('\'');
    }

    private static bool IsSingleQuotedSafe(string value)
    {
        // Single-quoted scalars have no escape mechanism besides doubling an embedded quote, so any other
        // control character (including line breaks, which would need YAML's line-folding rules to round-trip
        // correctly) is not safely representable here.
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    private void WriteDoubleQuotedStyle(string value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);
        WriteDoubleQuotedStyleCore(value);
        CompleteValueAfterScalar();
    }

    private void WriteDoubleQuotedStyleCore(ReadOnlySpan<char> value)
    {
        Write('"');
        WriteEscaped(value);
        Write('"');
    }

    private void WriteBlockStyle(string value, bool folded)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: false);
        WriteBlockStyleCore(value, folded);
        CompleteValueAfterScalar();
    }

    private void WriteBlockStyleCore(ReadOnlySpan<char> value, bool folded)
    {
        if (!IsBlockScalarSafe(value))
        {
            WriteDoubleQuotedStyleCore(value);
            return;
        }

        var trailingBreaks = CountTrailingBreaks(value, out var trailingBreakLength);
        var meaningful = value[..^trailingBreakLength];

        if (folded && ContainsUnfoldableBreak(meaningful))
        {
            // A single break between two non-blank lines would be folded into a space on read-back,
            // corrupting the value. Literal style preserves every break exactly, so fall back to it.
            folded = false;
        }

        var contentIndentSpaces = Options.IndentSize * Math.Max(_depth, 1);
        var needsIndentIndicator = meaningful.Length == 0 || meaningful[0] is ' ' or '\n' or '\r';
        var indentIndicatorDigit = '\0';
        if (needsIndentIndicator && !TryGetIndentIndicatorDigit(contentIndentSpaces, out indentIndicatorDigit))
        {
            // The YAML indentation indicator is a single digit (1-9) giving the indentation *increment*
            // relative to the parent block's indent, not an absolute column; it cannot represent every
            // IndentSize/depth combination (e.g. a sufficiently large IndentSize). Double-quoted style has
            // no such limitation, so fall back to it rather than emit an indicator that would desync the
            // reader's expected indentation from what was actually written.
            WriteDoubleQuotedStyleCore(value);
            return;
        }

        Write(folded ? '>' : '|');
        if (needsIndentIndicator)
        {
            Write(indentIndicatorDigit);
        }

        var chompIndicator = trailingBreaks switch
        {
            0 => '-',
            1 => (char?)null,
            _ => '+',
        };
        if (chompIndicator is char c)
        {
            Write(c);
        }

        WriteNewLine();
        WriteBlockScalarBody(meaningful, contentIndentSpaces);

        // Every trailing break mandated by the chomping rule (0 for strip, 1 for clip, N for keep) must be
        // written explicitly: this value may be the last thing in the document (or the last value in its
        // container), in which case nothing else will ever write a newline on its behalf.
        for (var i = 0; i < trailingBreaks; i++)
        {
            WriteNewLine();
        }

        if (trailingBreaks > 0 && _depth > 0)
        {
            // The newline(s) just written already provide the separator a following sibling would
            // otherwise add on its own; suppress that one redundant separator so a chomped block scalar
            // isn't followed by a stray blank line.
            _frames[_depth - 1].SuppressNextSeparatorNewline = true;
        }
    }

    /// <summary>
    /// Computes the block scalar indentation indicator digit for <paramref name="contentIndentSpaces"/> (the
    /// absolute column at which this scalar's content is indented), returning <see langword="false"/> when
    /// it cannot be represented.
    /// </summary>
    /// <remarks>
    /// The scanner interprets this digit as an <em>increment</em> added to the
    /// parent block's already-established indent, not as an absolute column: for every level of container
    /// nesting below the root, that parent indent is exactly <c>contentIndentSpaces - Options.IndentSize</c>
    /// (one <see cref="YamlSerializerOptions.IndentSize"/> step shallower than this scalar's own content
    /// indent), so the increment to write is always <see cref="YamlSerializerOptions.IndentSize"/> itself.
    /// At the document root there is no parent indent to add to, so the increment written must be the full
    /// absolute value instead. Per the YAML spec the indicator is always exactly one digit (1-9).
    /// </remarks>
    private bool TryGetIndentIndicatorDigit(int contentIndentSpaces, out char digit)
    {
        var increment = _depth > 0 ? Options.IndentSize : contentIndentSpaces;
        if (increment is < 1 or > 9)
        {
            digit = '\0';
            return false;
        }

        digit = (char)('0' + increment);
        return true;
    }

    private void WriteBlockScalarBody(ReadOnlySpan<char> content, int indentSpaces)
    {
        var pos = 0;
        var first = true;
        while (true)
        {
            var lineEnd = pos;
            while (lineEnd < content.Length && content[lineEnd] is not ('\n' or '\r'))
            {
                lineEnd++;
            }

            if (!first)
            {
                WriteNewLine();
            }
            first = false;

            var line = content[pos..lineEnd];
            if (line.Length > 0)
            {
                WriteBlockIndent(indentSpaces);
                Write(line);
            }

            if (lineEnd >= content.Length)
            {
                return;
            }

            pos = content[lineEnd] == '\r' && lineEnd + 1 < content.Length && content[lineEnd + 1] == '\n'
                ? lineEnd + 2
                : lineEnd + 1;
        }
    }

    private void WriteBlockIndent(int spaces)
    {
        if (spaces <= 0)
        {
            return;
        }

        if (_indentBuilder.Length != spaces)
        {
            _indentBuilder.Clear();
            _indentBuilder.Append(' ', spaces);
        }

        Write(_indentBuilder);
    }

    /// <summary>
    /// Counts the trailing line breaks in <paramref name="value"/> (a <c>\r\n</c> pair counts as a single
    /// break) and reports the total character length they occupy.
    /// </summary>
    private static int CountTrailingBreaks(ReadOnlySpan<char> value, out int charLength)
    {
        var count = 0;
        var i = value.Length;
        charLength = 0;
        while (i > 0)
        {
            if (value[i - 1] == '\n')
            {
                if (i >= 2 && value[i - 2] == '\r')
                {
                    i -= 2;
                    charLength += 2;
                }
                else
                {
                    i -= 1;
                    charLength += 1;
                }
                count++;
                continue;
            }

            if (value[i - 1] == '\r')
            {
                i -= 1;
                charLength += 1;
                count++;
                continue;
            }

            break;
        }

        return count;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="content"/> contains a line break between two
    /// non-blank lines, i.e. a break that YAML's folded-style line-folding rule would collapse into a space
    /// when the value is read back.
    /// </summary>
    /// <remarks>
    /// Internal (rather than private) so <see cref="Emitter"/>'s lower-level, event-based folded-scalar
    /// writer can share this exact check instead of re-implementing it, keeping both writers' definitions
    /// of "safe to fold" from drifting apart.
    /// </remarks>
    internal static bool ContainsUnfoldableBreak(ReadOnlySpan<char> content)
    {
        var lineStart = 0;
        var previousLineWasNonBlank = false;
        while (true)
        {
            var lineEnd = lineStart;
            while (lineEnd < content.Length && content[lineEnd] is not ('\n' or '\r'))
            {
                lineEnd++;
            }

            var lineIsNonBlank = lineEnd > lineStart;
            if (lineIsNonBlank && previousLineWasNonBlank)
            {
                return true;
            }
            previousLineWasNonBlank = lineIsNonBlank;

            if (lineEnd >= content.Length)
            {
                return false;
            }

            lineStart = content[lineEnd] == '\r' && lineEnd + 1 < content.Length && content[lineEnd + 1] == '\n'
                ? lineEnd + 2
                : lineEnd + 1;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when every character of <paramref name="value"/> can appear unescaped
    /// in literal/folded block scalar content (printable characters and line breaks).
    /// </summary>
    private static bool IsBlockScalarSafe(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (c is '\n' or '\r')
            {
                continue;
            }

            if (!Emitter.IsPrintable(c))
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldQuoteAmbiguousScalar(ReadOnlySpan<char> value)
    {
        if (!Options.ScalarStylePreferences.PreferQuotedForAmbiguousScalars)
        {
            return false;
        }

        if (value.Equals("<<", StringComparison.Ordinal))
        {
            return true;
        }

        return YamlScalar.IsNull(value) ||
               YamlScalar.TryParseBool(value, out _) ||
               YamlScalar.TryParseInt64(value, out _) ||
               YamlScalar.TryParseDouble(value, out _);
    }

    private static bool IsPlainSafe(ReadOnlySpan<char> value, bool isKey)
    {
        // Keep it conservative: if in doubt, quote.
        if (value.Length == 0)
        {
            return false;
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return false;
        }

        // Disallow YAML special characters and common ambiguities.
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '\n' or '\r' or '\t')
            {
                return false;
            }

            // Control characters (including NUL) must be quoted and escaped.
            if (char.IsControl(c))
            {
                return false;
            }
            if (c is ':' or '#' or '{' or '}' or '[' or ']' or ',' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`')
            {
                return false;
            }
        }

        // A leading '-' or '?' followed by separation/end is a collection/key indicator, not a plain scalar.
        if ((value[0] is '-' or '?') && (value.Length == 1 || char.IsWhiteSpace(value[1])))
        {
            return false;
        }

        return true;
    }

    private void WriteEscaped(ReadOnlySpan<char> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\':
                    Write("\\\\");
                    break;
                case '"':
                    Write("\\\"");
                    break;
                case '\n':
                    Write("\\n");
                    break;
                case '\r':
                    Write("\\r");
                    break;
                case '\t':
                    Write("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        Write("\\u");
                        Write(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Write(c);
                    }
                    break;
            }
        }
    }

    private void WritePlainScalar(string value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);
        Write(value);
        CompleteValueAfterScalar();
    }

    private void WritePlainScalar(ReadOnlySpan<char> value)
    {
        WriteValuePrefixForScalar();
        WriteNodeProperties(writeLeadingSpace: false, writeTrailingSpace: true);
        Write(value);
        CompleteValueAfterScalar();
    }

    private void Write(string value)
    {
        if (_stringBuilder is not null)
        {
            _stringBuilder.Append(value);
            TrackLastChar(value);
            return;
        }

        _writer!.Write(value);
        TrackLastChar(value);
    }

    private void Write(char value)
    {
        if (_stringBuilder is not null)
        {
            _stringBuilder.Append(value);
            TrackLastChar(value);
            return;
        }

        _writer!.Write(value);
        TrackLastChar(value);
    }

    private void Write(ReadOnlySpan<char> value)
    {
        if (_stringBuilder is not null)
        {
#if NETSTANDARD2_0
            _stringBuilder.Append(value.ToString());
#else
            _stringBuilder.Append(value);
#endif
            TrackLastChar(value);
            return;
        }

#if NETSTANDARD2_0
        _writer!.Write(value.ToString());
#else
        _writer!.Write(value);
#endif
        TrackLastChar(value);
    }

    private void Write(StringBuilder value)
    {
        if (_stringBuilder is not null)
        {
            _stringBuilder.Append(value);
            TrackLastChar(value);
            return;
        }

#if NETSTANDARD2_0
        _writer!.Write(value.ToString());
#else
        _writer!.Write(value);
#endif
        TrackLastChar(value);
    }

    private void TrackLastChar(string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _hasWrittenChar = true;
        _lastWrittenChar = value[value.Length - 1];
    }

    private void TrackLastChar(char value)
    {
        _hasWrittenChar = true;
        _lastWrittenChar = value;
    }

    private void TrackLastChar(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _hasWrittenChar = true;
        _lastWrittenChar = value[value.Length - 1];
    }

    private void TrackLastChar(StringBuilder value)
    {
        if (value.Length == 0)
        {
            return;
        }

        _hasWrittenChar = true;
        _lastWrittenChar = value[value.Length - 1];
    }

    private enum ContainerKind
    {
        Mapping,
        Sequence,
    }

    private enum PendingStartKind
    {
        None,
        MappingValue,
        SequenceItem,
        SequenceItemCompact,
        Root,
    }

    /// <summary>
    /// Restores the block sequence item styles that were active before a <see cref="PushBlockSequenceItemStyle"/> call.
    /// </summary>
    public readonly struct BlockSequenceItemStyleScope : IDisposable
    {
        private readonly YamlWriter? _writer;
        private readonly YamlSequenceItemStyle _mappingStyle;
        private readonly YamlSequenceItemStyle _sequenceStyle;

        internal BlockSequenceItemStyleScope(YamlWriter writer, YamlSequenceItemStyle mappingStyle, YamlSequenceItemStyle sequenceStyle)
        {
            _writer = writer;
            _mappingStyle = mappingStyle;
            _sequenceStyle = sequenceStyle;
        }

        /// <summary>
        /// Restores the previously active block sequence item styles.
        /// </summary>
        public void Dispose()
        {
            _writer?.RestoreBlockSequenceItemStyle(_mappingStyle, _sequenceStyle);
        }
    }

    private struct ContainerFrame
    {
        public ContainerFrame(ContainerKind kind, PendingStartKind pendingStart)
        {
            Kind = kind;
            HasContent = false;
            ExpectingKey = kind == ContainerKind.Mapping;
            PendingStart = pendingStart;
        }

        public ContainerKind Kind;
        public bool HasContent;
        public bool ExpectingKey;
        public PendingStartKind PendingStart;

        /// <summary>
        /// Set when the value just written already ended with its own physical trailing newline(s) (a
        /// clip/keep-chomped block scalar). Consumed exactly once by the next sibling's separator check so a
        /// block scalar's own chomped newline isn't followed by a redundant blank line.
        /// </summary>
        public bool SuppressNextSeparatorNewline;
    }
}
