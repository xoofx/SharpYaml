using System;
using System.IO;
using System.Text;
using SharpYaml.Serialization.References;

namespace SharpYaml.Serialization;

/// <summary>
/// Writes YAML tokens for use by <see cref="YamlConverter"/> implementations.
/// </summary>
public sealed class YamlWriter
{
    private readonly TextWriter _writer;
    private readonly YamlSerializerOptions _options;
    private readonly YamlReferenceWriter? _referenceWriter;
    private readonly StringBuilder _indentBuilder = new();
    private ContainerFrame[] _frames = new ContainerFrame[8];
    private int _depth;
    private string? _pendingAnchor;
    private string? _pendingTag;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlWriter"/> class.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="options">The serializer options used for formatting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public YamlWriter(TextWriter writer, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _options = options ?? YamlSerializerOptions.Default;
        _referenceWriter = _options.ReferenceHandling == YamlReferenceHandling.Preserve ? new YamlReferenceWriter() : null;
    }

    internal TextWriter Writer => _writer;

    internal YamlReferenceWriter? ReferenceWriter => _referenceWriter;

    /// <summary>
    /// Writes a YAML tag for the next value.
    /// </summary>
    /// <param name="tag">The YAML tag, such as <c>!dog</c>.</param>
    public void WriteTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
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
        ArgumentNullException.ThrowIfNull(anchor);
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
        ArgumentNullException.ThrowIfNull(alias);
        if (alias.Length == 0)
        {
            throw new ArgumentException("Alias cannot be empty.", nameof(alias));
        }

        WriteValuePrefixForAlias();
        _writer.Write('*');
        _writer.Write(alias);
        CompleteValueAfterScalar();
    }

    /// <summary>
    /// Writes the start of a mapping.
    /// </summary>
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
        if (_depth == 0 || _frames[_depth - 1].Kind != ContainerKind.Mapping)
        {
            throw new InvalidOperationException("Property names can only be written inside a mapping.");
        }

        ref var frame = ref _frames[_depth - 1];
        if (!frame.ExpectingKey)
        {
            throw new InvalidOperationException("A property name cannot be written when a value is expected.");
        }

        EnsureContainerStarted(ref frame);

        if (frame.HasContent)
        {
            WriteNewLine();
        }

        WriteIndent();
        WriteScalarCore(name, isKey: true);
        _writer.Write(':');

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
            _writer.Write("null");
            CompleteValueAfterScalar();
            return;
        }

        WriteScalarCore(value, isKey: false);
        CompleteValueAfterScalar();
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
        EnsureContainerStarted(ref frame);

        if (frame.Kind == ContainerKind.Mapping)
        {
            if (frame.ExpectingKey)
            {
                throw new InvalidOperationException("A scalar value cannot be written when a key is expected.");
            }

            _writer.Write(' ');
            return;
        }

        if (frame.HasContent)
        {
            WriteNewLine();
        }

        WriteIndent();
        _writer.Write("- ");
        frame.HasContent = true;
    }

    private void WriteValuePrefixForAlias()
    {
        if (_depth == 0)
        {
            return;
        }

        ref var frame = ref _frames[_depth - 1];
        EnsureContainerStarted(ref frame);

        if (frame.Kind == ContainerKind.Mapping)
        {
            if (frame.ExpectingKey)
            {
                throw new InvalidOperationException("An alias value cannot be written when a key is expected.");
            }

            _writer.Write(' ');
            return;
        }

        if (frame.HasContent)
        {
            WriteNewLine();
        }

        WriteIndent();
        _writer.Write("- ");
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
            _writer.Write(' ');
        }

        var wroteAny = false;
        if (_pendingAnchor is not null)
        {
            _writer.Write('&');
            _writer.Write(_pendingAnchor);
            wroteAny = true;
        }

        if (_pendingTag is not null)
        {
            if (wroteAny)
            {
                _writer.Write(' ');
            }

            _writer.Write(_pendingTag);
            wroteAny = true;
        }

        _pendingAnchor = null;
        _pendingTag = null;

        if (writeTrailingSpace && wroteAny)
        {
            _writer.Write(' ');
        }
    }

    private void EnsureContainerStarted(ref ContainerFrame frame)
    {
        if (frame.PendingStart == PendingStartKind.None)
        {
            return;
        }

        frame.PendingStart = PendingStartKind.None;
        WriteNewLine();
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
            EnsureContainerStarted(ref parent);

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
                if (parent.HasContent)
                {
                    WriteNewLine();
                }

                WriteIndent();
                _writer.Write('-');
                if (_pendingAnchor is not null || _pendingTag is not null)
                {
                    WriteNodeProperties(writeLeadingSpace: true, writeTrailingSpace: false);
                }
                parent.HasContent = true;
                pendingStart = PendingStartKind.SequenceItem;
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
            _writer.Write(kind == ContainerKind.Mapping ? "{}" : "[]");
            return;
        }

        _writer.Write(' ');
        _writer.Write(kind == ContainerKind.Mapping ? "{}" : "[]");
    }

    private void WriteIndent()
    {
        if (!_options.WriteIndented)
        {
            return;
        }

        var indentLevel = Math.Max(0, _depth - 1);
        if (indentLevel == 0)
        {
            return;
        }

        var spaces = _options.IndentSize * indentLevel;
        if (_indentBuilder.Length != spaces)
        {
            _indentBuilder.Clear();
            _indentBuilder.Append(' ', spaces);
        }

        _writer.Write(_indentBuilder);
    }

    private void WriteNewLine()
    {
        _writer.Write('\n');
    }

    private void WriteScalarCore(string value, bool isKey)
    {
        if (value.Length == 0)
        {
            _writer.Write("''");
            return;
        }

        if (IsPlainSafe(value, isKey))
        {
            _writer.Write(value);
            return;
        }

        _writer.Write('"');
        WriteEscaped(value);
        _writer.Write('"');
    }

    private static bool IsPlainSafe(string value, bool isKey)
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

        if (value.Contains('\n') || value.Contains('\r') || value.Contains('\t'))
        {
            return false;
        }

        // Disallow YAML special characters and common ambiguities.
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
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

        if (!isKey && (value.StartsWith("- ", StringComparison.Ordinal) || value.StartsWith("? ", StringComparison.Ordinal)))
        {
            return false;
        }

        if (isKey && value == "-")
        {
            return false;
        }

        return true;
    }

    private void WriteEscaped(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '\\':
                    _writer.Write("\\\\");
                    break;
                case '"':
                    _writer.Write("\\\"");
                    break;
                case '\n':
                    _writer.Write("\\n");
                    break;
                case '\r':
                    _writer.Write("\\r");
                    break;
                case '\t':
                    _writer.Write("\\t");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        _writer.Write("\\u");
                        _writer.Write(((int)c).ToString("X4", global::System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                    _writer.Write(c);
                    }
                    break;
            }
        }
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
        Root,
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
    }
}
