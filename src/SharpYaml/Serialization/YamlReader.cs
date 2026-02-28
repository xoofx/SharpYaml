using System;
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
public ref struct YamlReader
{
    private readonly YamlReaderState _state;

    internal YamlReader(YamlReaderState state)
    {
        _state = state;
    }

    internal static YamlReader Create(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var parser = SharpYaml.Parser.CreateParser(new StringReader(yaml));
        return new YamlReader(new YamlReaderState(parser, referenceReader: null));
    }

    internal static YamlReader Create(string yaml, YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(options);

        var parser = SharpYaml.Parser.CreateParser(new StringReader(yaml));
        var referenceReader = options.ReferenceHandling == YamlReferenceHandling.Preserve ? new YamlReferenceReader() : null;
        return new YamlReader(new YamlReaderState(parser, referenceReader));
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
    /// Gets the start location of the current token.
    /// </summary>
    public Mark Start => _state.Start;

    /// <summary>
    /// Gets the end location of the current token.
    /// </summary>
    public Mark End => _state.End;

    internal YamlReferenceReader? ReferenceReader => _state.ReferenceReader;

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
            throw new InvalidOperationException($"Expected a scalar token but found '{TokenType}'.");
        }

        return ScalarValue ?? string.Empty;
    }

    internal sealed class YamlReaderState
    {
        private readonly IParser _parser;

        public YamlReaderState(IParser parser, YamlReferenceReader? referenceReader)
        {
            _parser = parser;
            TokenType = YamlTokenType.None;
            ReferenceReader = referenceReader;
        }

        public YamlTokenType TokenType { get; private set; }
        public string? ScalarValue { get; private set; }
        public string? Tag { get; private set; }
        public string? Anchor { get; private set; }
        public string? Alias { get; private set; }
        public Mark Start { get; private set; } = Mark.Empty;
        public Mark End { get; private set; } = Mark.Empty;
        public YamlReferenceReader? ReferenceReader { get; }

        public bool Read()
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

                ScalarValue = null;
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
            Tag = null;
            Anchor = null;
            Alias = null;
            Start = Mark.Empty;
            End = Mark.Empty;
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
