using System;

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
        public YamlTokenType TokenType { get; set; }
        public string? ScalarValue { get; set; }
        public string? Tag { get; set; }
        public string? Anchor { get; set; }
        public string? Alias { get; set; }

        public bool Read() => throw new NotImplementedException("YamlReader is not yet wired to a token source.");

        public void Skip() => throw new NotImplementedException("YamlReader is not yet wired to a token source.");
    }
}

