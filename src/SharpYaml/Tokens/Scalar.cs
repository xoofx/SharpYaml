// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Tokens;

/// <summary>
/// Represents a scalar token.
/// </summary>
public class Scalar : Token
{
    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>The value.</value>
    public string Value => _value ??= _source?.Substring(_sourceStart, _sourceLength)!;

    // The value, or while it is null the slice of the input it is: a token the parser reuses carries the slice of a
    // plain scalar, so that no string is created for a value that is read as a number or a boolean. Tokens read
    // through the public API of the scanner always have their value.
    private string? _value;
    private string? _source;
    private int _sourceStart;
    private int _sourceLength;

    /// <summary>Gets the value when it exists as a string; otherwise <see langword="null"/>, and the value is the slice.</summary>
    internal string? ValueOrNull => _value;

    /// <summary>Gets the input the value is a slice of, when <see cref="ValueOrNull"/> is <see langword="null"/>.</summary>
    internal string? Source => _source;

    /// <summary>Gets the start of the slice in <see cref="Source"/>.</summary>
    internal int SourceStart => _sourceStart;

    /// <summary>Gets the length of the slice in <see cref="Source"/>.</summary>
    internal int SourceLength => _sourceLength;

    /// <summary>
    /// Gets the style.
    /// </summary>
    /// <value>The style.</value>
    public ScalarStyle Style { get; private set; }

    /// <summary>Gives a token of a scanner pool the content of the scalar it now stands for.</summary>
    internal void Reset(string value, ScalarStyle style, in Mark start, in Mark end)
    {
        Reset(start, end);
        _value = value;
        _source = null;
        this.Style = style;
    }

    /// <summary>Gives a token of a scanner pool the scalar it now stands for, as a slice of the input.</summary>
    internal void Reset(string source, int sourceStart, int sourceLength, ScalarStyle style, in Mark start, in Mark end)
    {
        Reset(start, end);
        _value = null;
        _source = source;
        _sourceStart = sourceStart;
        _sourceLength = sourceLength;
        this.Style = style;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scalar"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    public Scalar(string value)
        : this(value, ScalarStyle.Any)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scalar"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="style">The style.</param>
    public Scalar(string value, ScalarStyle style)
        : this(value, style, Mark.Empty, Mark.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scalar"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="style">The style.</param>
    /// <param name="start">The start position of the token.</param>
    /// <param name="end">The end position of the token.</param>
    public Scalar(string value, ScalarStyle style, Mark start, Mark end)
        : base(start, end)
    {
        Kind = TokenKind.Scalar;
        _value = value;
        this.Style = style;
    }
}