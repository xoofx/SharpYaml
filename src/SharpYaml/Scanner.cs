// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using SharpYaml.Tokens;

namespace SharpYaml;

/// <summary>
/// Converts a sequence of characters into a sequence of YAML tokens.
/// </summary>
public class Scanner<TBuffer> where TBuffer : ILookAheadBuffer
{
    private const int MaxVersionNumberLength = 9;

    private readonly Stack<int> indents = new Stack<int>();
    private readonly InsertionQueue<Token> tokens = new InsertionQueue<Token>();
    // The potential simple key of each flow level, the innermost last.
    private SimpleKey[] simpleKeys = new SimpleKey[4];
    private int simpleKeyCount;

    // The tokens the parser handed back with Recycle, which the scanner uses again instead of allocating.
    // One stack per kind of token (Token.PoolSlot), the kinds that only carry their marks sharing the code.
    private bool _recycleTokens;
    private readonly TokenStack[] _freeTokens = new TokenStack[PoolSlotCount];

    // Used by ScanFlowScalar, this will reduce memory usage by /2
    private readonly StringBuilder scanScalarValue = new StringBuilder();
    private readonly StringBuilder scanScalarWhitespaces = new StringBuilder();
    private readonly StringBuilder scanScalarLeadingBreak = new StringBuilder();
    private readonly StringBuilder scanScalarTrailingBreaks = new StringBuilder();


    private bool streamStartProduced;
    private bool streamEndProduced;
    private int indent = -1;
    private bool simpleKeyAllowed;

    private int index = 0;
    private int line = 0;
    private int column = 0;
    private int _lineIndentationSpaces;
    private char _lastCharacter;
    private bool _flowValueAllowed;
    private bool _previousHighSurrogate;
    private bool _lineHasContent;
    private int _lastTabIndex = -1;

    /// <summary>
    /// Gets the current position inside the input stream.
    /// </summary>
    /// <value>The current position.</value>
    public Mark CurrentPosition { get { return new Mark(index, line, column); } }

    private int flowLevel;
    private int tokensParsed;

    /// <summary>Defines the max buffer length constant.</summary>
    public const int MaxBufferLength = 12; // Number of characters in two 8 bit unicode codepoints.
    private readonly TBuffer analyzer;

    // The whole input when it is a string: the scanner then reads it in place at 'index', without going through
    // 'analyzer' (an interface call per character) and can consume runs of characters at once.
    private readonly string _text;
    private bool tokenAvailable;

    private static readonly IDictionary<char, char> simpleEscapeCodes = InitializeSimpleEscapeCodes();

    private static IDictionary<char, char> InitializeSimpleEscapeCodes()
    {
        IDictionary<char, char> codes = new SortedDictionary<char, char>();
        codes.Add('0', '\0');
        codes.Add('a', '\x07');
        codes.Add('b', '\x08');
        codes.Add('t', '\x09');
        codes.Add('\t', '\x09');
        codes.Add('n', '\x0A');
        codes.Add('v', '\x0B');
        codes.Add('f', '\x0C');
        codes.Add('r', '\x0D');
        codes.Add('e', '\x1B');
        codes.Add(' ', '\x20');
        codes.Add('"', '"');
        codes.Add('\\', '\\');
        codes.Add('/', '/');
        codes.Add('N', '\x85');
        codes.Add('_', '\xA0');
        codes.Add('L', '\x2028');
        codes.Add('P', '\x2029');
        return codes;
    }

    private char ReadCurrentCharacter()
    {
        char currentCharacter = Peek(0);
        Skip();
        return currentCharacter;
    }

    /// <summary>Gets the character at <paramref name="offset"/> from the current position, or NUL past the end of the input.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char Peek(int offset)
    {
        var text = _text;
        if (text != null)
        {
            var position = index + offset;
            return (uint)position < (uint)text.Length ? text[position] : '\0';
        }

        return analyzer.Peek(offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char Peek() => Peek(0);

    private bool EndOfInput
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _text != null ? index >= _text.Length : analyzer.EndOfInput;
    }

    /// <summary>Advances the buffer; in a string, <see cref="index"/> is the position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipBuffer(int length)
    {
        if (_text == null)
        {
            analyzer.Skip(length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CacheBuffer(int length)
    {
        if (_text == null)
        {
            analyzer.Cache(length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Check(char expected) => Peek(0) == expected;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Check(char expected, int offset) => Peek(offset) == expected;

    private bool Check(string expectedCharacters) => Check(expectedCharacters, 0);

    private bool Check(string expectedCharacters, int offset)
    {
        Debug.Assert(expectedCharacters.Length > 1, "Use Check(char, int) instead.");
        return expectedCharacters.IndexOf(Peek(offset)) >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFlowIndicator(char character) => character is ',' or '[' or ']' or '{' or '}';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBlankOrBreakOrZero(char character) => character is ' ' or '\t' or '\r' or '\n' or '\0';

    private bool IsAlpha()
    {
        var character = Peek(0);
        return character is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_' or '-';
    }

    private bool IsDigit() => Peek(0) is >= '0' and <= '9';

    private int AsDigit() => Peek(0) - '0';

    private bool IsHex(int offset) => Peek(offset) is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f');

    private int AsHex(int offset)
    {
        var character = Peek(offset);
        return character <= '9' ? character - '0' : character <= 'F' ? character - 'A' + 10 : character - 'a' + 10;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsSpace() => Peek(0) == ' ';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsTab() => Peek(0) == '\t';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsZero() => Peek(0) == '\0';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBlank() => Peek(0) is ' ' or '\t';

    // YAML 1.2 treats NEL, LS and PS as ordinary non-break characters (§5.4).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBreak() => Peek(0) is '\r' or '\n';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBreak(int offset) => Peek(offset) is '\r' or '\n';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCrLf() => Peek(0) == '\r' && Peek(1) == '\n';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBreakOrZero() => Peek(0) is '\r' or '\n' or '\0';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBlankOrBreakOrZero() => IsBlankOrBreakOrZero(Peek(0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBlankOrBreakOrZero(int offset) => IsBlankOrBreakOrZero(Peek(offset));

    // Callers must only invoke this when positioned at an actual break (i.e. after checking IsBreak(),
    // not IsBreakOrZero()). At end-of-input there is no line break to consume; calling this there
    // would return the sentinel/zero character, corrupting block/plain scalars that end at EOF with no
    // trailing newline by appending a stray '\0' to the scanned value. Returns char (not string) to avoid a
    // per-call string allocation on this hot path - every caller immediately appends the single char to a
    // StringBuilder.
    private char ReadLine()
    {
        SkipLine();
        return '\n';
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="t:Scanner"/> class.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    public Scanner(TBuffer buffer)
    {
        analyzer = buffer;
        if (buffer is StringLookAheadBuffer { Position: 0 } text)
        {
            _text = text.Value;
        }
    }

    /// <summary>
    /// Gets the current token.
    /// </summary>
    public Token Current { get; private set; }

    /// <summary>
    /// Moves to the next token.
    /// </summary>
    /// <returns></returns>
    public bool MoveNext()
    {
        if (Current != null)
        {
            ConsumeCurrent();
        }

        return InternalMoveNext();
    }

    internal bool InternalMoveNext()
    {
        if (!tokenAvailable && !streamEndProduced)
        {
            FetchMoreTokens();
        }
        if (tokens.Count > 0)
        {
            Current = tokens.Dequeue();
            tokenAvailable = false;
            return true;
        }
        else
        {
            Current = null;
            return false;
        }
    }

    /// <summary>
    /// Consumes the current token and increments the parsed token count
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ConsumeCurrent()
    {
        ++tokensParsed;
        tokenAvailable = false;
        Current = null;
    }

    private void FetchMoreTokens()
    {
        // While we need more tokens to fetch, do it.

        while (true)
        {
            // Check if we really need to fetch more tokens.

            bool needsMoreTokens = false;

            if (tokens.Count == 0)
            {
                // Queue is empty.

                needsMoreTokens = true;
            }
            else
            {
                // Check if any potential simple key may occupy the head position.

                needsMoreTokens = StaleSimpleKeys(tokensParsed);
            }

            // We are finished.
            if (!needsMoreTokens)
            {
                break;
            }

            // Fetch the next token.

            FetchNextToken();
        }
        tokenAvailable = true;
    }

    private static bool StartsWith(StringBuilder what, char start)
    {
        return what.Length > 0 && what[0] == start;
    }

    /// <summary>
    /// Check the list of potential simple keys and remove the positions that
    /// cannot contain simple keys anymore.
    /// </summary>
    /// <param name="tokenNumber">A token number, or -1.</param>
    /// <returns><see langword="true"/> when a simple key is still possible at <paramref name="tokenNumber"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool StaleSimpleKeys(int tokenNumber = -1)
    {
        // Outside of flow collections there is one key, and none is possible while a value is scanned.
        if (simpleKeyCount == 1 && !simpleKeys[0].IsPossible)
        {
            return false;
        }

        return StaleSimpleKeysCore(tokenNumber);
    }

    private bool StaleSimpleKeysCore(int tokenNumber)
    {
        // Check for a potential simple key for each flow level.

        var isPossibleAtToken = false;
        for (var i = 0; i < simpleKeyCount; i++)
        {
            ref var key = ref simpleKeys[i];
            if (!key.IsPossible)
            {
                continue;
            }

            // The specification requires that a simple key

            //  - is limited to a single line,
            //  - is shorter than 1024 characters.


            if (key.Mark.Line < line || key.Mark.Index + 1024 < index)
            {
                // Check if the potential simple key to be removed is required.

                if (key.IsRequired)
                {
                    throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "While scanning a simple key, could not find expected ':'.");
                }

                key.IsPossible = false;
            }
            else if (key.TokenNumber == tokenNumber)
            {
                isPossibleAtToken = true;
            }
        }

        return isPossibleAtToken;
    }

    private void FetchNextToken()
    {
        // Check if we just started scanning.  Fetch STREAM-START then.

        if (!streamStartProduced)
        {
            FetchStreamStart();
            return;
        }

        // Eat whitespaces and comments until we reach the next token.

        ScanToNextToken();
        var flowValueAllowed = _flowValueAllowed;
        _flowValueAllowed = false;

        if (!_lineHasContent && !EndOfInput && _lineIndentationSpaces <= indent)
        {
            if (flowLevel > 0)
            {
                throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "Flow collection content and closing indicators must be indented more than the containing block.");
            }
            if (_lineIndentationSpaces < column && simpleKeyAllowed)
            {
                throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "Tabs cannot be used for indentation.");
            }
        }

        // Remove obsolete potential simple keys.

        StaleSimpleKeys();

        // Check the indentation level against the current column.

        UnrollIndent(column);


        // Ensure that the buffer contains at least 4 characters.  4 is the length
        // of the longest indicators ('--- ' and '... ').


        CacheBuffer(4);

        // Is it the end of the stream?

        if (EndOfInput)
        {
            FetchStreamEnd();
            return;
        }

        // Find what the next character starts. It is read once: most tokens are plain scalars, which no case below takes.

        var character = Peek(0);
        switch (character)
        {
            case '%':
                // Is it a directive?
                if (column == 0)
                {
                    FetchDirective();
                    return;
                }
                break;

            case '-':
                // Is it the document start indicator?
                if (column == 0 && Check('-', 1) && Check('-', 2) && IsBlankOrBreakOrZero(3))
                {
                    FetchDocumentIndicator(true);
                    return;
                }

                // Is it the block entry indicator?
                if (IsBlankOrBreakOrZero(1))
                {
                    FetchBlockEntry();
                    return;
                }
                break;

            case '.':
                // Is it the document end indicator?
                if (column == 0 && Check('.', 1) && Check('.', 2) && IsBlankOrBreakOrZero(3))
                {
                    FetchDocumentIndicator(false);
                    return;
                }
                break;

            case '[':
                // The flow sequence start indicator.
                FetchFlowCollectionStart(true);
                return;

            case '{':
                // The flow mapping start indicator.
                FetchFlowCollectionStart(false);
                return;

            case ']':
                // The flow sequence end indicator.
                FetchFlowCollectionEnd(true);
                return;

            case '}':
                // The flow mapping end indicator.
                FetchFlowCollectionEnd(false);
                return;

            case ',':
                // The flow entry indicator.
                FetchFlowEntry();
                return;

            case '?':
                // Is it the key indicator?
                if (IsBlankOrBreakOrZero(1))
                {
                    FetchKey();
                    return;
                }
                break;

            case ':':
                // Is it the value indicator?
                if (IsBlankOrBreakOrZero(1) || (flowLevel > 0 && (flowValueAllowed || IsFlowIndicator(Peek(1)))))
                {
                    FetchValue();
                    return;
                }
                break;

            case '*':
                // An alias.
                FetchAnchor(true);
                return;

            case '&':
                // An anchor.
                FetchAnchor(false);
                return;

            case '!':
                // A tag.
                FetchTag();
                return;

            case '|':
                // Is it a literal scalar?
                if (flowLevel == 0)
                {
                    FetchBlockScalar(true);
                    return;
                }
                break;

            case '>':
                // Is it a folded scalar?
                if (flowLevel == 0)
                {
                    FetchBlockScalar(false);
                    return;
                }
                break;

            case '\'':
                // A single-quoted scalar.
                FetchFlowScalar(true);
                return;

            case '"':
                // A double-quoted scalar.
                FetchFlowScalar(false);
                return;
        }

        // Is it a plain scalar?

        // A plain scalar may start with any non-blank characters except

        //      '-', '?', ':', ',', '[', ']', '{', '}',
        //      '#', '&', '*', '!', '|', '>', '\'', '\"',
        //      '%', '@', '`'.

        // In the block context (and, for the '-' indicator, in the flow context
        // too), it may also start with the characters

        //      '-', '?', ':'

        // if it is followed by a non-space character.

        bool isInvalidPlainScalarCharacter = character is ' ' or '\t' or '\r' or '\n' or '\0'
            or '-' or '?' or ':' or ',' or '[' or ']' or '{' or '}' or '#' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`';

        bool isPlainScalar =
            !isInvalidPlainScalarCharacter ||
            (character is '-' or '?' or ':' && !IsBlankOrBreakOrZero(1) &&
                (flowLevel == 0 || !IsFlowIndicator(Peek(1))));

        if (isPlainScalar)
        {
            FetchPlainScalar();
            return;
        }

        // If we don't determine the token type so far, it is an error.
        throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "While scanning for the next token, find character that cannot start any token.");
    }

    private bool CheckWhiteSpace()
    {
        return IsBlank();
    }

    private bool IsDocumentIndicator()
    {
        if (column == 0 && IsBlankOrBreakOrZero(3))
        {
            bool isDocumentStart = Check('-', 0) && Check('-', 1) && Check('-', 2);
            bool isDocumentEnd = Check('.', 0) && Check('.', 1) && Check('.', 2);

            return isDocumentStart || isDocumentEnd;
        }
        else
        {
            return false;
        }
    }

    private void Skip()
    {
        var character = Peek(0);
        var highSurrogate = char.IsHighSurrogate(character);
        if (!(character is '\t' or '\r' or '\n' || Emitter.IsPrintable(character) ||
            (highSurrogate && char.IsLowSurrogate(Peek(1))) ||
            (char.IsLowSurrogate(character) && _previousHighSurrogate)))
        {
            throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "The input contains a character outside the YAML character set.");
        }
        _previousHighSurrogate = highSurrogate;
        _lastCharacter = character;
        if (character == '\t') _lastTabIndex = index;
        if (character is not (' ' or '\t')) _lineHasContent = true;
        if (column == _lineIndentationSpaces && character == ' ')
        {
            _lineIndentationSpaces++;
        }
        ++index;
        ++column;
        SkipBuffer(1);
    }

    /// <summary>
    /// Consumes the run of spaces at the current position of a string input at once: what <see cref="Skip"/> does for
    /// each of them. Does nothing for other inputs, which the caller reads character by character.
    /// </summary>
    private void SkipSpaces()
    {
        var text = _text;
        if (text == null)
        {
            return;
        }

        var end = index;
        while ((uint)end < (uint)text.Length && text[end] == ' ')
        {
            end++;
        }

        var count = end - index;
        if (count == 0)
        {
            return;
        }

        // The spaces that start a line are its indentation: both counters then advance together.
        if (column == _lineIndentationSpaces)
        {
            _lineIndentationSpaces += count;
        }

        _previousHighSurrogate = false;
        _lastCharacter = ' ';
        index = end;
        column += count;
    }

    /// <summary>
    /// Counts the characters from <paramref name="start"/> that a plain scalar takes as they are, whatever follows them:
    /// printable, not a blank or a break, not a surrogate, not <c>:</c> (which ends the scalar depending on the next
    /// character) and, in a flow collection, not a flow indicator.
    /// </summary>
    private int CountPlainScalarRun(string text, int start)
    {
        var position = start;
#if NET8_0_OR_GREATER
        // The ASCII part of the run, several characters at a time.
        var remaining = text.AsSpan(start);
        var ascii = remaining.IndexOfAnyExcept(flowLevel == 0 ? ScannerCharacters.PlainScalarRunInBlock : ScannerCharacters.PlainScalarRunInFlow);
        position += ascii < 0 ? remaining.Length : ascii;
#endif
        if (flowLevel == 0)
        {
            while ((uint)position < (uint)text.Length)
            {
                var character = text[position];
                if ((uint)(character - 0x21) <= 0x7E - 0x21 ? character == ':' : !IsPrintableBeyondAscii(character))
                {
                    break;
                }

                position++;
            }
        }
        else
        {
            while ((uint)position < (uint)text.Length)
            {
                var character = text[position];
                if ((uint)(character - 0x21) <= 0x7E - 0x21 ? character == ':' || IsFlowIndicator(character) : !IsPrintableBeyondAscii(character))
                {
                    break;
                }

                position++;
            }
        }

        return position - start;
    }

    /// <summary>Gets whether a character above the ASCII range is printable (<see cref="Emitter.IsPrintable"/>) and not a surrogate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPrintableBeyondAscii(char character)
        => character is '\x85' or (>= '\xA0' and <= '\xD7FF') or (>= '\xE000' and <= '\xFFFD');

    /// <summary>
    /// Consumes <paramref name="count"/> characters of a string input that <see cref="CountPlainScalarRun"/> counted:
    /// what <see cref="Skip"/> does for each of them.
    /// </summary>
    private void SkipPlainScalarRun(string text, int count)
    {
        index += count;
        column += count;
        _previousHighSurrogate = false;
        _lastCharacter = text[index - 1];
        _lineHasContent = true;
    }

    private void SkipLine()
    {
        _lastCharacter = '\n';
        _previousHighSurrogate = false;
        _lineHasContent = false;
        if (IsCrLf())
        {
            index += 2;
            column = 0;
            _lineIndentationSpaces = 0;
            ++line;
            SkipBuffer(2);
        }
        else if (IsBreak())
        {
            ++index;
            column = 0;
            _lineIndentationSpaces = 0;
            ++line;
            SkipBuffer(1);
        }
        else if (!IsZero())
        {
            throw new InvalidOperationException("Not at a break.");
        }
    }

    private void ScanToNextToken()
    {
        // Until the next token is not find.

        for (; ; )
        {
            if (column == 0 && Check('\uFEFF'))
            {
                SkipBuffer(1);
                index++;
            }
            // Eat whitespaces.

            // Tabs may separate tokens but do not contribute to indentation.
            SkipSpaces();
            while (CheckWhiteSpace())
            {
                Skip();
            }

            // Eat a comment until a line break.

            if (Check('#'))
            {
                if (column != 0 && _lastCharacter is not (' ' or '\t' or '\r' or '\n'))
                {
                    throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "A comment must be separated from the preceding token by whitespace.");
                }
                while (!IsBreakOrZero())
                {
                    Skip();
                }
            }

            // If it is a line break, eat it.

            if (IsBreak())
            {
                SkipLine();

                // In the block context, a new line may start a simple key.

                if (flowLevel == 0)
                {
                    simpleKeyAllowed = true;
                }
            }
            else
            {
                // We have find a token.

                break;
            }
        }
    }

    private void FetchStreamStart()
    {
        // Initialize the simple key stack.

        PushSimpleKey(default);

        // A simple key is allowed at the beginning of the stream.

        simpleKeyAllowed = true;

        // We have started.

        streamStartProduced = true;

        // Create the STREAM-START token and append it to the queue.

        tokens.Enqueue(new StreamStart(CurrentPosition, CurrentPosition));
    }

    /// <summary>
    /// Pop indentation levels from the indents stack until the current level
    /// becomes less or equal to the column.  For each intendation level, append
    /// the BLOCK-END token.
    /// </summary>
    private void UnrollIndent(int column)
    {
        // In the flow context, do nothing.

        if (flowLevel != 0)
        {
            return;
        }

        // Loop through the intendation levels in the stack.

        while (indent > column)
        {
            // Create a token and append it to the queue.

            tokens.Enqueue(NewBlockEnd(CurrentPosition, CurrentPosition));

            // Pop the indentation level.

            indent = indents.Pop();
        }
    }

    /// <summary>
    /// Produce the STREAM-END token and shut down the scanner.
    /// </summary>
    private void FetchStreamEnd()
    {
        // Force new line.

        if (column != 0)
        {
            column = 0;
            ++line;
        }

        // Reset the indentation level.

        UnrollIndent(-1);

        // Reset simple keys.

        RemoveSimpleKey();

        simpleKeyAllowed = false;

        // Create the STREAM-END token and append it to the queue.

        streamEndProduced = true;
        tokens.Enqueue(new StreamEnd(CurrentPosition, CurrentPosition));
    }

    private void FetchDirective()
    {
        // Reset the indentation level.

        UnrollIndent(-1);

        // Reset simple keys.

        RemoveSimpleKey();

        simpleKeyAllowed = false;

        // Create the YAML-DIRECTIVE or TAG-DIRECTIVE token.

        var token = ScanDirective();

        // Append the token to the queue.

        tokens.Enqueue(token);
    }

    /// <summary>
    /// Scan a YAML-DIRECTIVE or TAG-DIRECTIVE token.
    ///
    /// Scope:
    ///      %YAML    1.1    # a comment \n
    ///      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    ///      %TAG    !yaml!  tag:yaml.org,2002:  \n
    ///      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    /// </summary>
    private Token ScanDirective()
    {
        // Eat '%'.

        var start = CurrentPosition;

        Skip();

        // Scan the directive name.

        var name = ScanDirectiveName(start);

        // Is it a YAML directive?

        Token directive;
        switch (name)
        {
            case "YAML":
                directive = ScanVersionDirectiveValue(start);
                break;

            case "TAG":
                directive = ScanTagDirectiveValue(start);
                break;

            default:
                while (!IsBreakOrZero())
                {
                    Skip();
                }
                directive = new ReservedDirective(start, CurrentPosition);
                break;
        }

        // Eat the rest of the line including any comments.

        while (IsBlank())
        {
            Skip();
        }

        if (Check('#'))
        {
            while (!IsBreakOrZero())
            {
                Skip();
            }
        }

        // Check if we are at the end of the line.

        if (!IsBreakOrZero())
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a directive, did not find expected comment or line break.");
        }

        // Eat a line break.

        if (IsBreak())
        {
            SkipLine();
        }

        return directive;
    }

    /// <summary>
    /// Produce the DOCUMENT-START or DOCUMENT-END token.
    /// </summary>
    private void FetchDocumentIndicator(bool isStartToken)
    {
        // Reset the indentation level.

        UnrollIndent(-1);

        // Reset simple keys.

        RemoveSimpleKey();

        simpleKeyAllowed = false;

        // Consume the token.

        var start = CurrentPosition;

        Skip();
        Skip();
        Skip();

        var token = isStartToken ? (Token)new DocumentStart(start, CurrentPosition) : new DocumentEnd(start, start);
        tokens.Enqueue(token);
    }

    /// <summary>
    /// Produce the FLOW-SEQUENCE-START or FLOW-MAPPING-START token.
    /// </summary>
    private void FetchFlowCollectionStart(bool isSequenceToken)
    {
        // The indicators '[' and '{' may start a simple key.

        SaveSimpleKey();

        // Increase the flow level.

        IncreaseFlowLevel();

        // A simple key may follow the indicators '[' and '{'.

        simpleKeyAllowed = true;

        // Consume the token.

        var start = CurrentPosition;
        Skip();

        // Create the FLOW-SEQUENCE-START of FLOW-MAPPING-START token.

        Token token;
        if (isSequenceToken)
        {
            token = new FlowSequenceStart(start, start);
        }
        else
        {
            token = new FlowMappingStart(start, start);
        }

        tokens.Enqueue(token);
    }

    /// <summary>
    /// Increase the flow level and resize the simple key list if needed.
    /// </summary>
    private void IncreaseFlowLevel()
    {
        // Reset the simple key on the next level.

        PushSimpleKey(default);

        // Increase the flow level.

        ++flowLevel;
    }

    /// <summary>
    /// Produce the FLOW-SEQUENCE-END or FLOW-MAPPING-END token.
    /// </summary>
    private void FetchFlowCollectionEnd(bool isSequenceToken)
    {
        // Reset any potential simple key on the current flow level.

        RemoveSimpleKey();

        // Decrease the flow level.

        DecreaseFlowLevel();

        // No simple keys after the indicators ']' and '}'.

        simpleKeyAllowed = false;

        // Consume the token.

        var start = CurrentPosition;
        Skip();

        Token token;
        if (isSequenceToken)
        {
            token = new FlowSequenceEnd(start, start);
        }
        else
        {
            token = new FlowMappingEnd(start, start);
        }

        tokens.Enqueue(token);
        _flowValueAllowed = true;
    }

    /// <summary>
    /// Decrease the flow level.
    /// </summary>
    private void DecreaseFlowLevel()
    {
        Debug.Assert(flowLevel > 0, "Could flowLevel be zero when this method is called?");
        if (flowLevel > 0)
        {
            --flowLevel;
            simpleKeyCount--;
        }
    }

    /// <summary>
    /// Produce the FLOW-ENTRY token.
    /// </summary>
    private void FetchFlowEntry()
    {
        // Reset any potential simple keys on the current flow level.

        RemoveSimpleKey();

        // Simple keys are allowed after ','.

        simpleKeyAllowed = true;

        // Consume the token.

        var start = CurrentPosition;
        Skip();

        // Create the FLOW-ENTRY token and append it to the queue.

        tokens.Enqueue(NewFlowEntry(start, CurrentPosition));
    }

    /// <summary>
    /// Produce the BLOCK-ENTRY token.
    /// </summary>
    private void FetchBlockEntry()
    {
        // Check if the scanner is in the block context.

        if (flowLevel == 0)
        {
            // Check if we are allowed to start a new entry.

            if (!simpleKeyAllowed)
            {
                throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "Block sequence entries are not allowed in this context.");
            }

            // Add the BLOCK-SEQUENCE-START token if needed.
            RollIndent(column, -1, true, CurrentPosition);
        }
        else
        {
            // It is an error for the '-' indicator to occur in the flow context,
            // but we let the Parser detect and report about it because the Parser
            // is able to point to the context.
        }

        // Reset any potential simple keys on the current flow level.

        RemoveSimpleKey();

        // Simple keys are allowed after '-'.

        simpleKeyAllowed = true;

        // Consume the token.

        var start = CurrentPosition;
        Skip();

        // Create the BLOCK-ENTRY token and append it to the queue.

        tokens.Enqueue(NewBlockEntry(start, CurrentPosition));
    }

    /// <summary>
    /// Produce the KEY token.
    /// </summary>
    private void FetchKey()
    {
        // In the block context, additional checks are required.

        if (flowLevel == 0)
        {
            // Check if we are allowed to start a new key (not nessesary simple).

            if (!simpleKeyAllowed)
            {
                throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "Mapping keys are not allowed in this context.");
            }

            // Add the BLOCK-MAPPING-START token if needed.

            RollIndent(column, -1, false, CurrentPosition);
        }

        // Reset any potential simple keys on the current flow level.

        RemoveSimpleKey();

        // Simple keys are allowed after '?' in the block context.

        simpleKeyAllowed = flowLevel == 0;

        // Consume the token.

        var start = CurrentPosition;
        Skip();

        // Create the KEY token and append it to the queue.

        tokens.Enqueue(NewKey(start, CurrentPosition));
    }

    /// <summary>
    /// Produce the VALUE token.
    /// </summary>
    private void FetchValue()
    {
        ref var simpleKey = ref simpleKeys[simpleKeyCount - 1];

        // Have we find a simple key?

        if (simpleKey.IsPossible)
        {
            // Create the KEY token and insert it into the queue.

            tokens.Insert(simpleKey.TokenNumber - tokensParsed, NewKey(simpleKey.Mark, simpleKey.Mark));

            // In the block context, we may need to add the BLOCK-MAPPING-START token.

            RollIndent(simpleKey.Mark.Column, simpleKey.TokenNumber, false, simpleKey.Mark);

            // Remove the simple key.

            simpleKey.IsPossible = false;

            // A simple key cannot follow another simple key.

            simpleKeyAllowed = false;
        }
        else
        {
            // The ':' indicator follows a complex key.

            // In the block context, extra checks are required.

            if (flowLevel == 0)
            {
                // Check if we are allowed to start a complex value.

                if (!simpleKeyAllowed)
                {
                    throw new SyntaxErrorException(CurrentPosition, CurrentPosition, "Mapping values are not allowed in this context.");
                }

                // Add the BLOCK-MAPPING-START token if needed.

                RollIndent(column, -1, false, CurrentPosition);
            }

            // Simple keys after ':' are allowed in the block context.

            simpleKeyAllowed = flowLevel == 0;
        }

        // Consume the token.

        var start = CurrentPosition;
        Skip();

        // Create the VALUE token and append it to the queue.

        tokens.Enqueue(NewValue(start, CurrentPosition));
    }

    /// <summary>
    /// Push the current indentation level to the stack and set the new level
    /// the current column is greater than the indentation level.  In this case,
    /// append or insert the specified token into the token queue.
    /// </summary>
    private void RollIndent(int column, int number, bool isSequence, Mark position)
    {
        // In the flow context, do nothing.

        if (flowLevel > 0)
        {
            return;
        }

        if (indent < column)
        {
            if (_lastTabIndex >= index - this.column && _lastTabIndex < position.Index)
            {
                throw new SyntaxErrorException(position, position, "Tabs cannot be used to indent a block collection.");
            }
            // Push the current indentation level to the stack and set the new
            // indentation level.


            indents.Push(indent);

            indent = column;

            // Create a token and insert it into the queue.

            Token token;
            if (isSequence)
            {
                token = NewBlockSequenceStart(position, position);
            }
            else
            {
                token = NewBlockMappingStart(position, position);
            }

            if (number == -1)
            {
                tokens.Enqueue(token);
            }
            else
            {
                tokens.Insert(number - tokensParsed, token);
            }
        }
    }

    /// <summary>
    /// Produce the ALIAS or ANCHOR token.
    /// </summary>
    private void FetchAnchor(bool isAlias)
    {
        // An anchor or an alias could be a simple key.

        SaveSimpleKey();

        // A simple key cannot follow an anchor or an alias.

        simpleKeyAllowed = false;

        // Create the ALIAS or ANCHOR token and append it to the queue.

        tokens.Enqueue(ScanAnchor(isAlias));
    }

    private Token ScanAnchor(bool isAlias)
    {
        // Eat the indicator character.

        var start = CurrentPosition;

        Skip();

        // Consume the value.

        var value = new StringBuilder();
        while (!IsBlankOrBreakOrZero() && !Check(",[]{}"))
        {
            value.Append(ReadCurrentCharacter());
        }


        // Anchor names exclude whitespace and flow indicators (§6.9.2).
        if (value.Length == 0)
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning an anchor or alias, did not find expected alphabetic or numeric character.");
        }

        // Create a token.

        if (isAlias)
        {
            return new AnchorAlias(value.ToString(), start, CurrentPosition);
        }
        else
        {
            return new Anchor(value.ToString(), start, CurrentPosition);
        }
    }

    /// <summary>
    /// Produce the TAG token.
    /// </summary>
    private void FetchTag()
    {
        // A tag could be a simple key.

        SaveSimpleKey();

        // A simple key cannot follow a tag.

        simpleKeyAllowed = false;

        // Create the TAG token and append it to the queue.

        tokens.Enqueue(ScanTag());
    }

    /// <summary>
    /// Scan a TAG token.
    /// </summary>
    Token ScanTag()
    {
        var start = CurrentPosition;

        // Check if the tag is in the canonical form.

        string handle;
        string suffix;

        if (Check('<', 1))
        {
            // Set the handle to ''

            handle = string.Empty;

            // Eat '!<'

            Skip();
            Skip();

            // Consume the tag value.

            suffix = ScanTagUri(null, start, true);

            // Check for '>' and eat it.

            if (!Check('>'))
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While scanning a tag, did not find the expected '>'.");
            }

            Skip();
        }
        else
        {
            // The tag has either the '!suffix' or the '!handle!suffix' form.

            // First, try to scan a handle.

            var firstPart = ScanTagHandle(false, start);

            // Check if it is, indeed, handle.

            if (firstPart.Length > 1 && firstPart[0] == '!' && firstPart[firstPart.Length - 1] == '!')
            {
                handle = firstPart;

                // Scan the suffix now.

                suffix = ScanTagUri(null, start);
            }
            else
            {
                // It wasn't a handle after all.  Scan the rest of the tag.

                suffix = ScanTagUri(firstPart, start);

                // Set the handle to '!'.

                handle = "!";


                // A special case: the '!' tag.  Set the handle to '' and the
                // suffix to '!'.


                if (suffix.Length == 0)
                {
                    suffix = handle;
                    handle = string.Empty;
                }
            }
        }

        // Check the character which ends the tag.

        if (!IsBlankOrBreakOrZero() && !(flowLevel > 0 && Check(",]}")))
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a tag, did not find expected whitespace or line break.");
        }

        // Create a token.

        return new Tag(handle, suffix, start, CurrentPosition);
    }

    /// <summary>
    /// Produce the SCALAR(...,literal) or SCALAR(...,folded) tokens.
    /// </summary>
    private void FetchBlockScalar(bool isLiteral)
    {
        // Remove any potential simple keys.

        RemoveSimpleKey();

        // A simple key may follow a block scalar.

        simpleKeyAllowed = true;

        // Create the SCALAR token and append it to the queue.

        tokens.Enqueue(ScanBlockScalar(isLiteral));
    }

    /// <summary>
    /// Scan a block scalar.
    /// </summary>
    Token ScanBlockScalar(bool isLiteral)
    {
        var value = new StringBuilder();
        var leadingBreak = new StringBuilder();
        var trailingBreaks = new StringBuilder();

        int chomping = 0;
        int increment = 0;
        int currentIndent = -1;
        bool leadingBlank = false;

        // Eat the indicator '|' or '>'.

        var start = CurrentPosition;

        Skip();

        // Check for a chomping indicator.

        if (Check("+-"))
        {
            // Set the chomping method and eat the indicator.

            chomping = Check('+') ? +1 : -1;

            Skip();

            // Check for an indentation indicator.

            if (IsDigit())
            {
                // Check that the intendation is greater than 0.

                if (Check('0'))
                {
                    throw new SyntaxErrorException(start, CurrentPosition, "While scanning a block scalar, find an intendation indicator equal to 0.");
                }

                // Get the intendation level and eat the indicator.

                increment = AsDigit();

                Skip();
            }
        }

        // Do the same as above, but in the opposite order.

        else if (IsDigit())
        {
            if (Check('0'))
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While scanning a block scalar, find an intendation indicator equal to 0.");
            }

            increment = AsDigit();

            Skip();

            if (Check("+-"))
            {
                chomping = Check('+') ? +1 : -1;

                Skip();
            }
        }

        // A block scalar header comment requires separation from its indicators.
        if (Check('#'))
        {
            throw new SyntaxErrorException(start, CurrentPosition, "A block scalar header comment requires whitespace.");
        }

        // Eat whitespaces and comments to the end of the line.

        while (IsBlank())
        {
            Skip();
        }

        if (Check('#'))
        {
            while (!IsBreakOrZero())
            {
                Skip();
            }
        }

        // Check if we are at the end of the line.

        if (!IsBreakOrZero())
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a block scalar, did not find expected comment or line break.");
        }

        // Eat a line break.

        if (IsBreak())
        {
            SkipLine();
        }

        var end = CurrentPosition;

        // Set the intendation level if it was specified.

        if (increment != 0)
        {
            currentIndent = indent >= 0 ? indent + increment : increment;
        }

        // Scan the leading line breaks and determine the indentation level if needed.

        currentIndent = ScanBlockScalarBreaks(currentIndent, trailingBreaks, start, ref end);

        // Scan the block scalar content.

        while (column == currentIndent && !IsZero() && !IsDocumentIndicator())
        {
            // We are at the beginning of a non-empty line.


            // Is it a trailing whitespace?

            bool trailingBlank = IsBlank();

            // YAML 1.2 section 6.5: fold a single break to a space, or discard the first
            // break before empty lines and retain the remaining breaks as content.
            if (!isLiteral && StartsWith(leadingBreak, '\n') && !leadingBlank && !trailingBlank)
            {
                if (trailingBreaks.Length == 0)
                {
                    value.Append(' ');
                }
                leadingBreak.Length = 0;
            }
            else
            {
                value.Append(leadingBreak.ToString());
                leadingBreak.Length = 0;
            }

            // Append the remaining line breaks.

            value.Append(trailingBreaks.ToString());
            trailingBreaks.Length = 0;

            // Is it a leading whitespace?

            leadingBlank = IsBlank();

            // Consume the current line.

            while (!IsBreakOrZero())
            {
                value.Append(ReadCurrentCharacter());
            }

            // Consume the line break, if there is one. A block scalar's last content line may end at
            // end-of-input with no break at all; ReadLine() must not be called in that case (see its summary).
            if (IsBreak())
            {
                leadingBreak.Append(ReadLine());
            }
            else if (EndOfInput && !_lineHasContent && column > 0)
            {
                // A final whitespace-only content line is an empty line (§8.1.2).
                leadingBreak.Append('\n');
            }

            // Eat the following intendation spaces and line breaks.

            if (EndOfInput)
            {
                end = CurrentPosition;
                break;
            }
            currentIndent = ScanBlockScalarBreaks(currentIndent, trailingBreaks, start, ref end);
        }

        // Chomp the tail.

        if (chomping != -1)
        {
            value.Append(leadingBreak);
        }
        if (chomping == 1)
        {
            value.Append(trailingBreaks);
        }

        // Create a token.

        var style = isLiteral ? ScalarStyle.Literal : ScalarStyle.Folded;
        return NewScalar(value.ToString(), style, start, end);
    }

    /// <summary>
    /// Scan intendation spaces and line breaks for a block scalar.  Determine the
    /// intendation level if needed.
    /// </summary>
    private int ScanBlockScalarBreaks(int currentIndent, StringBuilder breaks, Mark start, ref Mark end)
    {
        int maxIndent = 0;

        end = CurrentPosition;

        // Eat the intendation spaces and line breaks.

        for (; ; )
        {
            // Eat the intendation spaces.

            while ((currentIndent < 0 || column < currentIndent) && IsSpace())
            {
                Skip();
            }

            if (column > maxIndent)
            {
                maxIndent = column;
            }

            // Check for a tab character messing the intendation.

            if (column < (currentIndent < 0 ? indent + 1 : currentIndent) && IsTab())
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While scanning a block scalar, find a tab character where an intendation space is expected.");
            }

            // Have we find a non-empty line?

            if (!IsBreak())
            {
                break;
            }

            // Consume the line break.

            breaks.Append(ReadLine());

            end = CurrentPosition;
        }

        // Determine the indentation level if needed.

        if (currentIndent < 0)
        {
            currentIndent = Math.Max(column, Math.Max(indent + 1, 0));
            if (!EndOfInput && maxIndent > currentIndent)
            {
                throw new SyntaxErrorException(start, CurrentPosition, "A leading empty line is more indented than the first content line.");
            }
        }

        if (EndOfInput && !_lineHasContent && column > 0)
        {
            breaks.Append('\n');
        }

        return currentIndent;
    }

    /// <summary>
    /// Produce the SCALAR(...,single-quoted) or SCALAR(...,double-quoted) tokens.
    /// </summary>
    private void FetchFlowScalar(bool isSingleQuoted)
    {
        // A plain scalar could be a simple key.

        SaveSimpleKey();

        // A simple key cannot follow a flow scalar.

        simpleKeyAllowed = false;

        // Create the SCALAR token and append it to the queue.

        tokens.Enqueue(ScanFlowScalar(isSingleQuoted));
        _flowValueAllowed = true;
    }

    /// <summary>
    /// Scan a quoted scalar.
    /// </summary>
    private Token ScanFlowScalar(bool isSingleQuoted)
    {
        // Eat the left quote.

        var start = CurrentPosition;
        var end = CurrentPosition;

        Skip();

        // Consume the content of the quoted scalar.
        scanScalarValue.Length = 0;
        scanScalarWhitespaces.Length = 0;
        scanScalarLeadingBreak.Length = 0;
        scanScalarTrailingBreaks.Length = 0;
        for (; ; )
        {
            // Check that there are no document indicators at the beginning of the line.

            if (IsDocumentIndicator())
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While scanning a quoted scalar, find unexpected document indicator.");
            }

            // Check for EOF.

            if (IsZero())
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While scanning a quoted scalar, find unexpected end of stream.");
            }

            // Consume non-blank characters.

            bool hasLeadingBlanks = false;

            while (!IsBlankOrBreakOrZero())
            {
                // Check for an escaped single quote.

                if (isSingleQuoted && Check('\'', 0) && Check('\'', 1))
                {
                    scanScalarValue.Append('\'');
                    Skip();
                    Skip();
                }

                // Check for the right quote.

                else if (Check(isSingleQuoted ? '\'' : '"'))
                {
                    break;
                }

                // Check for an escaped line break.

                else if (!isSingleQuoted && Check('\\') && IsBreak(1))
                {
                    Skip();
                    SkipLine();
                    hasLeadingBlanks = true;
                    break;
                }

                // Check for an escape sequence.

                else if (!isSingleQuoted && Check('\\'))
                {
                    int codeLength = 0;

                    // Check the escape character.

                    char escapeCharacter = Peek(1);
                    switch (escapeCharacter)
                    {
                        case 'x':
                            codeLength = 2;
                            break;

                        case 'u':
                            codeLength = 4;
                            break;

                        case 'U':
                            codeLength = 8;
                            break;

                        default:
                            char unescapedCharacter;
                            if (simpleEscapeCodes.TryGetValue(escapeCharacter, out unescapedCharacter))
                            {
                                scanScalarValue.Append(unescapedCharacter);
                            }
                            else
                            {
                                throw new SyntaxErrorException(start, CurrentPosition, "While parsing a quoted scalar, find unknown escape character.");
                            }
                            break;
                    }

                    Skip();
                    Skip();

                    // Consume an arbitrary escape code.

                    if (codeLength > 0)
                    {
                        int character = 0;

                        // Scan the character value.

                        for (int k = 0; k < codeLength; ++k)
                        {
                            if (!IsHex(k))
                            {
                                throw new SyntaxErrorException(start, CurrentPosition, "While parsing a quoted scalar, did not find expected hexdecimal number.");
                            }
                            character = (character << 4) + AsHex(k);
                        }

                        // Check the value and write the character.

                        if (character < 0 || character > 0x10FFFF)
                        {
                            throw new SyntaxErrorException(start, CurrentPosition, "Invalid Unicode character escape code.");
                        }
                        if (character >= 0xD800 && character <= 0xDFFF)
                        {
                            var foundNextCharacter = true;
                            int nextCharacter = 0;

                            // We might be dealing with a surrogate pair - try to read the next unicode character.
                            if (codeLength == 4 && Check('\\', codeLength) && Check('u', codeLength + 1))
                            {
                                for (int k = 0; k < codeLength; ++k)
                                {
                                    if (!IsHex(k + codeLength + 2))
                                    {
                                        foundNextCharacter = false;
                                        break;
                                    }
                                    nextCharacter = (nextCharacter << 4) + AsHex(k + codeLength + 2);
                                }

                                if (foundNextCharacter)
                                {
                                    for (int k = 0; k < codeLength + 2; ++k)
                                        Skip();
                                }
                            }
                            else
                                foundNextCharacter = false;

                            if (foundNextCharacter && char.IsHighSurrogate((char)character) && char.IsLowSurrogate((char)nextCharacter))
                                scanScalarValue.Append(CharHelper.ConvertFromUtf32(CharHelper.ConvertToUtf32((char)character, (char)nextCharacter)));
                            else
                                throw new SyntaxErrorException(start, CurrentPosition, "While parsing a quoted scalar, find invalid Unicode character escape code.");
                        }
                        else
                            scanScalarValue.Append(CharHelper.ConvertFromUtf32(character));

                        // Advance the pointer.

                        for (int k = 0; k < codeLength; ++k)
                        {
                            Skip();
                        }
                    }
                }
                else
                {
                    // It is a non-escaped non-blank character.

                    scanScalarValue.Append(ReadCurrentCharacter());
                }
            }

            // Check if we are at the end of the scalar.

            if (Check(isSingleQuoted ? '\'' : '"'))
                break;

            // Consume blank characters.

            while (IsBlank() || IsBreak())
            {
                if (IsBlank())
                {
                    // Consume a space or a tab character.

                    if (!hasLeadingBlanks)
                    {
                        scanScalarWhitespaces.Append(ReadCurrentCharacter());
                    }
                    else
                    {
                        Skip();
                    }
                }
                else
                {
                    // Check if it is a first line break.

                    if (!hasLeadingBlanks)
                    {
                        scanScalarWhitespaces.Length = 0;
                        scanScalarLeadingBreak.Append(ReadLine());
                        hasLeadingBlanks = true;
                    }
                    else
                    {
                        scanScalarTrailingBreaks.Append(ReadLine());
                    }
                }
            }

            // Join the whitespaces or fold line breaks.

            if (hasLeadingBlanks && !IsZero() && _lineIndentationSpaces <= indent)
            {
                throw new SyntaxErrorException(start, CurrentPosition, "A quoted scalar continuation must be indented.");
            }

            if (hasLeadingBlanks)
            {
                // Do we need to fold line breaks?

                if (StartsWith(scanScalarLeadingBreak, '\n'))
                {
                    if (scanScalarTrailingBreaks.Length == 0)
                    {
                        scanScalarValue.Append(' ');
                    }
                    else
                    {
                        scanScalarValue.Append(scanScalarTrailingBreaks.ToString());
                    }
                }
                else
                {
                    scanScalarValue.Append(scanScalarLeadingBreak.ToString());
                    scanScalarValue.Append(scanScalarTrailingBreaks.ToString());
                }
                scanScalarLeadingBreak.Length = 0;
                scanScalarTrailingBreaks.Length = 0;
            }
            else
            {
                scanScalarValue.Append(scanScalarWhitespaces.ToString());
                scanScalarWhitespaces.Length = 0;
            }
        }

        // Eat the right quote.
        Skip();

        end = CurrentPosition;
        return NewScalar(scanScalarValue.ToString(), isSingleQuoted ? ScalarStyle.SingleQuoted : ScalarStyle.DoubleQuoted, start, end);
    }

    /// <summary>
    /// Produce the SCALAR(...,plain) token.
    /// </summary>
    private void FetchPlainScalar()
    {
        // A plain scalar could be a simple key.

        SaveSimpleKey();

        // A simple key cannot follow a flow scalar.

        simpleKeyAllowed = false;

        // Create the SCALAR token and append it to the queue.

        tokens.Enqueue(ScanPlainScalar());
    }

    /// <summary>
    /// Scan a plain scalar.
    /// </summary>
    private Token ScanPlainScalar()
    {
        if (_text != null)
        {
            return ScanPlainScalarInText(_text);
        }

        scanScalarValue.Length = 0;
        scanScalarWhitespaces.Length = 0;
        scanScalarLeadingBreak.Length = 0;
        scanScalarTrailingBreaks.Length = 0;

        bool hasLeadingBlanks = false;
        int currentIndent = indent + 1;

        var start = CurrentPosition;
        var end = CurrentPosition;

        // Consume the content of the plain scalar.

        for (; ; )
        {
            // Check for a document indicator.

            if (IsDocumentIndicator())
            {
                break;
            }

            // Check for a comment.

            if (Check('#'))
            {
                break;
            }

            // Consume non-blank characters.
            while (!IsBlankOrBreakOrZero())
            {
                // Check for indicators that may end a plain scalar.

                if ((Check(':') && (IsBlankOrBreakOrZero(1) ||
                    (flowLevel > 0 && Check(",[]{}", 1)))) || (flowLevel > 0 && Check(",[]{}")))
                {
                    break;
                }

                // Check if we need to join whitespaces and breaks.

                if (hasLeadingBlanks || scanScalarWhitespaces.Length > 0)
                {
                    if (hasLeadingBlanks)
                    {
                        // Do we need to fold line breaks?

                        if (StartsWith(scanScalarLeadingBreak, '\n'))
                        {
                            if (scanScalarTrailingBreaks.Length == 0)
                            {
                                scanScalarValue.Append(' ');
                            }
                            else
                            {
                                scanScalarValue.Append(scanScalarTrailingBreaks);
                            }
                        }
                        else
                        {
                            scanScalarValue.Append(scanScalarLeadingBreak);
                            scanScalarValue.Append(scanScalarTrailingBreaks);
                        }

                        scanScalarLeadingBreak.Length = 0;
                        scanScalarTrailingBreaks.Length = 0;

                        hasLeadingBlanks = false;
                    }
                    else
                    {
                        scanScalarValue.Append(scanScalarWhitespaces);
                        scanScalarWhitespaces.Length = 0;
                    }
                }

                // Copy the character.

                scanScalarValue.Append(ReadCurrentCharacter());

                end = CurrentPosition;
            }

            // Is it the end?

            if (!(IsBlank() || IsBreak()))
            {
                break;
            }

            // Consume blank characters.

            while (IsBlank() || IsBreak())
            {
                if (IsBlank())
                {
                    // Consume a space or a tab character.

                    if (!hasLeadingBlanks)
                    {
                        scanScalarWhitespaces.Append(ReadCurrentCharacter());
                    }
                    else
                    {
                        Skip();
                    }
                }
                else
                {
                    // Check if it is a first line break.

                    if (!hasLeadingBlanks)
                    {
                        scanScalarWhitespaces.Length = 0;
                        scanScalarLeadingBreak.Append(ReadLine());
                        hasLeadingBlanks = true;
                    }
                    else
                    {
                        scanScalarTrailingBreaks.Append(ReadLine());
                    }
                }
            }

            // Check intendation level.

            if (flowLevel == 0 && _lineIndentationSpaces < currentIndent && hasLeadingBlanks)
            {
                break;
            }
        }

        // Note that we change the 'simple_key_allowed' flag.

        if (hasLeadingBlanks)
        {
            simpleKeyAllowed = true;
        }

        // Create a token.

        return NewScalar(scanScalarValue.ToString(), ScalarStyle.Plain, start, end);
    }

    /// <summary>
    /// Scans a plain scalar of a string input: <see cref="ScanPlainScalar"/> with the runs of ordinary characters
    /// consumed at once. The value of a scalar that stays on one line is a slice of the input, so it is built from
    /// <see cref="scanScalarValue"/> only once a line break has to be folded.
    /// </summary>
    private Token ScanPlainScalarInText(string text)
    {
        bool hasLeadingBlanks = false;
        bool isFolded = false;
        bool isKey = false;
        int currentIndent = indent + 1;

        // The blanks read since the last character of the value on its line, and the breaks read after the first one.
        int pendingBlanks = 0;
        int trailingBreaks = 0;

        var start = CurrentPosition;
        var end = start;
        int valueStart = index;
        int valueEnd = index;

        // Consume the content of the plain scalar.

        for (; ; )
        {
            // Check for a document indicator.

            if (IsDocumentIndicator())
            {
                break;
            }

            // Check for a comment.

            if (Check('#'))
            {
                break;
            }

            // Consume non-blank characters.
            for (; ; )
            {
                var character = Peek(0);
                if (IsBlankOrBreakOrZero(character))
                {
                    break;
                }

                // Check for indicators that may end a plain scalar.

                if (character == ':')
                {
                    var next = Peek(1);
                    if (IsBlankOrBreakOrZero(next) || (flowLevel > 0 && IsFlowIndicator(next)))
                    {
                        // A value indicator follows: the scalar is a mapping key.
                        isKey = true;
                        break;
                    }
                }
                else if (flowLevel > 0 && IsFlowIndicator(character))
                {
                    break;
                }

                // Check if we need to join whitespaces and breaks.

                if (hasLeadingBlanks)
                {
                    // Fold the line breaks: the value is not a slice of the input anymore.

                    if (!isFolded)
                    {
                        isFolded = true;
                        scanScalarValue.Length = 0;
                        scanScalarValue.Append(text, valueStart, valueEnd - valueStart);
                    }

                    if (trailingBreaks == 0)
                    {
                        scanScalarValue.Append(' ');
                    }
                    else
                    {
                        scanScalarValue.Append('\n', trailingBreaks);
                    }

                    trailingBreaks = 0;
                    hasLeadingBlanks = false;
                }
                else if (pendingBlanks > 0)
                {
                    if (isFolded)
                    {
                        scanScalarValue.Append(text, index - pendingBlanks, pendingBlanks);
                    }

                    pendingBlanks = 0;
                }

                // Copy the characters.

                var run = CountPlainScalarRun(text, index);
                if (run > 0)
                {
                    if (isFolded)
                    {
                        scanScalarValue.Append(text, index, run);
                    }

                    SkipPlainScalarRun(text, run);
                }
                else
                {
                    // A ':' inside the scalar, or a character that Skip has to check.
                    Skip();
                    if (isFolded)
                    {
                        scanScalarValue.Append(character);
                    }
                }

                valueEnd = index;
                end = CurrentPosition;
            }

            // Is it the end?

            if (!(IsBlank() || IsBreak()))
            {
                break;
            }

            // Consume blank characters.

            for (; ; )
            {
                var character = Peek(0);
                if (character == ' ')
                {
                    // Consume the spaces: the ones that follow the value, or the indentation of the next line.

                    var spaces = index;
                    SkipSpaces();
                    if (!hasLeadingBlanks)
                    {
                        pendingBlanks += index - spaces;
                    }
                }
                else if (character == '\t')
                {
                    // Consume a tab character.

                    if (!hasLeadingBlanks)
                    {
                        pendingBlanks++;
                    }

                    Skip();
                }
                else if (character is '\r' or '\n')
                {
                    // Check if it is a first line break.

                    if (!hasLeadingBlanks)
                    {
                        pendingBlanks = 0;
                        hasLeadingBlanks = true;
                    }
                    else
                    {
                        trailingBreaks++;
                    }

                    SkipLine();
                }
                else
                {
                    break;
                }
            }

            // Check intendation level.

            if (flowLevel == 0 && _lineIndentationSpaces < currentIndent && hasLeadingBlanks)
            {
                break;
            }
        }

        // Note that we change the 'simple_key_allowed' flag.

        if (hasLeadingBlanks)
        {
            simpleKeyAllowed = true;
        }

        // Create a token.

        string value;
        if (isFolded)
        {
            value = scanScalarValue.ToString();
        }
        else
        {
            var length = valueEnd - valueStart;
            value = isKey && length <= MaxCachedKeyLength ? GetCachedKey(text, valueStart, length) : text.Substring(valueStart, length);
        }

        return NewScalar(value, ScalarStyle.Plain, start, end);
    }

    private const int MaxCachedKeyLength = 32;
    private const int KeyCacheSize = 64;

    // The strings of the short plain mapping keys read last, by hash: a document repeats the keys of its mappings,
    // so most keys are found here instead of being allocated again.
    private string[] _keyCache;

    private string GetCachedKey(string text, int start, int length)
    {
        if (length == 0)
        {
            return string.Empty;
        }

        var key = text.AsSpan(start, length);
        var hash = (uint)length;
        for (var i = 0; i < key.Length; i++)
        {
            hash = (hash * 31) + key[i];
        }

        var cache = _keyCache ??= new string[KeyCacheSize];
        ref var entry = ref cache[(int)(hash & (KeyCacheSize - 1))];
        var cached = entry;
        if (cached != null && cached.Length == length && key.SequenceEqual(cached.AsSpan()))
        {
            return cached;
        }

        return entry = key.ToString();
    }


    /// <summary>
    /// Remove a potential simple key at the current flow level.
    /// </summary>
    private void RemoveSimpleKey()
    {
        ref var key = ref simpleKeys[simpleKeyCount - 1];

        if (key.IsPossible && key.IsRequired)
        {
            // If the key is required, it is an error.

            throw new SyntaxErrorException(key.Mark, key.Mark, "While scanning a simple key, could not find expected ':'.");
        }

        // Remove the key from the stack.

        key.IsPossible = false;
    }

    /// <summary>
    /// Scan the directive name.
    ///
    /// Scope:
    ///      %YAML   1.1     # a comment \n
    ///       ^^^^
    ///      %TAG    !yaml!  tag:yaml.org,2002:  \n
    ///       ^^^
    /// </summary>
    private string ScanDirectiveName(Mark start)
    {
        var name = new StringBuilder();

        // Consume the directive name.

        while (!IsBlankOrBreakOrZero())
        {
            name.Append(ReadCurrentCharacter());
        }

        // Check if the name is empty.

        if (name.Length == 0)
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a directive, could not find expected directive name.");
        }

        // Check for an blank character after the name.

        if (!IsBlankOrBreakOrZero())
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a directive, find unexpected non-alphabetical character.");
        }

        return name.ToString();
    }

    private void SkipWhitespaces()
    {
        // Eat whitespaces.

        while (IsBlank())
        {
            Skip();
        }
    }

    /// <summary>
    /// Scan the value of VERSION-DIRECTIVE.
    ///
    /// Scope:
    ///      %YAML   1.1     # a comment \n
    ///           ^^^^^^
    /// </summary>
    private Token ScanVersionDirectiveValue(Mark start)
    {
        SkipWhitespaces();

        // Consume the major version number.

        int major = ScanVersionDirectiveNumber(start);

        // Eat '.'.

        if (!Check('.'))
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a %YAML directive, did not find expected digit or '.' character.");
        }

        Skip();

        // Consume the minor version number.

        int minor = ScanVersionDirectiveNumber(start);

        if (!IsBlankOrBreakOrZero())
        {
            throw new SyntaxErrorException(start, CurrentPosition, "The YAML version must be followed by whitespace or a line break.");
        }

        return new VersionDirective(new Version(major, minor), start, start);
    }

    /// <summary>
    /// Scan the value of a TAG-DIRECTIVE token.
    ///
    /// Scope:
    ///      %TAG    !yaml!  tag:yaml.org,2002:  \n
    ///          ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    /// </summary>
    private Token ScanTagDirectiveValue(Mark start)
    {
        SkipWhitespaces();

        // Scan a handle.

        var handle = ScanTagHandle(true, start);

        // Expect a whitespace.

        if (!IsBlank())
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a %TAG directive, did not find expected whitespace.");
        }

        SkipWhitespaces();

        // Scan a prefix.

        var prefix = ScanTagUri(null, start, true);

        // Expect a whitespace or line break.

        if (!IsBlankOrBreakOrZero())
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a %TAG directive, did not find expected whitespace or line break.");
        }

        return new TagDirective(handle, prefix, start, start);
    }

    /// <summary>
    /// Scan a tag.
    /// </summary>
    private string ScanTagUri(string head, Mark start, bool allowFlowIndicators = false)
    {
        var tag = new StringBuilder();
        if (head != null && head.Length > 1)
        {
            tag.Append(head.Substring(1));
        }

        // Scan the tag.

        // The set of characters that may appear in URI is as follows:

        //      '0'-'9', 'A'-'Z', 'a'-'z', '_', '-', ';', '/', '?', ':', '@', '&',
        //      '=', '+', '$', ',', '.', '!', '~', '*', '\'', '(', ')', '[', ']',
        //      '%'.


        while (IsAlpha() || Check(";/?:@&=+$,.!~*'()[]%"))
        {
            if (!allowFlowIndicators && Check("!,[]{}"))
            {
                break;
            }
            // Check if it is a URI-escape sequence.

            if (Check('%'))
            {
                tag.Append(ScanUriEscapes(start));
            }
            else
            {
                tag.Append(ReadCurrentCharacter());
            }
        }

        // Check if the tag is non-empty. The head counts as content, so that
        // the non-specific tag '!' is accepted.

        if (tag.Length == 0 && string.IsNullOrEmpty(head))
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While parsing a tag, did not find expected tag URI.");
        }

        return tag.ToString();
    }

    /// <summary>
    /// Decode an URI-escape sequence corresponding to a single UTF-8 character.
    /// </summary>
    private string ScanUriEscapes(Mark start)
    {
        // Decode the required number of characters.

        int width = 0;
        int character = 0;
        int minimum = 0;
        do
        {
            // Check for a URI-escaped octet.

            if (!(Check('%') && IsHex(1) && IsHex(2)))
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While parsing a tag, did not find URI escaped octet.");
            }

            // Get the octet.

            int octet = (AsHex(1) << 4) + AsHex(2);

            // If it is the leading octet, determine the length of the UTF-8 sequence.

            if (width == 0)
            {
                width = (octet & 0x80) == 0x00 ? 1 :
                    (octet & 0xE0) == 0xC0 ? 2 :
                    (octet & 0xF0) == 0xE0 ? 3 :
                    (octet & 0xF8) == 0xF0 ? 4 : 0;

                if (width == 0)
                {
                    throw new SyntaxErrorException(start, CurrentPosition, "While parsing a tag, find an incorrect leading UTF-8 octet.");
                }

                minimum = width == 1 ? 0 : width == 2 ? 0x80 : width == 3 ? 0x800 : 0x10000;
                character = octet & (width == 1 ? 0x7F : (1 << (7 - width)) - 1);
            }
            else
            {
                // Check if the trailing octet is correct.

                if ((octet & 0xC0) != 0x80)
                {
                    throw new SyntaxErrorException(start, CurrentPosition, "While parsing a tag, find an incorrect trailing UTF-8 octet.");
                }
                character = (character << 6) | (octet & 0x3F);
            }

            Skip();
            Skip();
            Skip();
        } while (--width > 0);

        if (character < minimum || character > 0x10FFFF || character is >= 0xD800 and <= 0xDFFF)
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While parsing a tag, find an incorrect UTF-8 sequence.");
        }

        return CharHelper.ConvertFromUtf32(character);
    }

    /// <summary>
    /// Scan a tag handle.
    /// </summary>
    private string ScanTagHandle(bool isDirective, Mark start)
    {
        // Check the initial '!' character.

        if (!Check('!'))
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a tag, did not find expected '!'.");
        }

        // Copy the '!' character.

        var tagHandle = new StringBuilder();
        tagHandle.Append(ReadCurrentCharacter());

        // Copy all subsequent alphabetical and numerical characters.

        while (IsAlpha() && !Check('_'))
        {
            tagHandle.Append(ReadCurrentCharacter());
        }

        // Check if the trailing character is '!' and copy it.

        if (Check('!'))
        {
            tagHandle.Append(ReadCurrentCharacter());
        }
        else
        {
            // It's either the '!' tag or not really a tag handle.  If it's a %TAG
            // directive, it's an error.  If it's a tag token, it must be a part of
            // URI.


            if (isDirective && (tagHandle.Length != 1 || tagHandle[0] != '!'))
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While parsing a tag directive, did not find expected '!'.");
            }
        }

        return tagHandle.ToString();
    }

    /// <summary>
    /// Scan the version number of VERSION-DIRECTIVE.
    ///
    /// Scope:
    ///      %YAML   1.1     # a comment \n
    ///              ^
    ///      %YAML   1.1     # a comment \n
    ///                ^
    /// </summary>
    private int ScanVersionDirectiveNumber(Mark start)
    {
        int value = 0;
        int length = 0;

        // Repeat while the next character is digit.

        while (IsDigit())
        {
            // Check if the number is too long.

            if (++length > MaxVersionNumberLength)
            {
                throw new SyntaxErrorException(start, CurrentPosition, "While scanning a %YAML directive, find extremely long version number.");
            }

            value = value * 10 + AsDigit();

            Skip();
        }

        // Check if the number was present.

        if (length == 0)
        {
            throw new SyntaxErrorException(start, CurrentPosition, "While scanning a %YAML directive, did not find expected version number.");
        }

        return value;
    }

    /// <summary>
    /// Check if a simple key may start at the current position and add it if
    /// needed.
    /// </summary>
    private void SaveSimpleKey()
    {
        // A simple key is required at the current position if the scanner is in
        // the block context and the current column coincides with the indentation
        // level.


        bool isRequired = (flowLevel == 0 && indent == column);


        // A simple key is required only when it is the first token in the current
        // line.  Therefore it is always allowed.  But we add a check anyway.


        Debug.Assert(simpleKeyAllowed || !isRequired, "Can't require a simple key and disallow it at the same time."); // Impossible.


        // If the current position may start a simple key, save it.


        if (simpleKeyAllowed)
        {
            RemoveSimpleKey();

            simpleKeys[simpleKeyCount - 1] = new SimpleKey(true, isRequired, tokensParsed + tokens.Count, CurrentPosition);
        }
    }

    private void PushSimpleKey(SimpleKey key)
    {
        if (simpleKeyCount == simpleKeys.Length)
        {
            Array.Resize(ref simpleKeys, simpleKeyCount * 2);
        }

        simpleKeys[simpleKeyCount++] = key;
    }

    /// <summary>
    /// Makes the scanner use again the tokens handed back with <see cref="Recycle"/>. Only for an owner that keeps no
    /// token once it consumed it, as the parser.
    /// </summary>
    internal void EnableTokenRecycling() => _recycleTokens = true;

    /// <summary>Hands back a token the caller consumed and keeps no reference to.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Recycle(Token token)
    {
        var slot = token.PoolSlot;
        if (slot != 0)
        {
            _freeTokens[slot].Push(token);
        }
    }

    private const byte PoolSlotScalar = 1;
    private const byte PoolSlotKey = 2;
    private const byte PoolSlotValue = 3;
    private const byte PoolSlotBlockEntry = 4;
    private const byte PoolSlotBlockEnd = 5;
    private const byte PoolSlotBlockMappingStart = 6;
    private const byte PoolSlotBlockSequenceStart = 7;
    private const byte PoolSlotFlowEntry = 8;
    private const int PoolSlotCount = 9;

    private Token NewScalar(string value, ScalarStyle style, in Mark start, in Mark end)
    {
        if (_freeTokens[PoolSlotScalar].TryPop(out var token))
        {
            // Only scalars go to this slot.
            ((Scalar)token).Reset(value, style, start, end);
            return token;
        }

        return new Scalar(value, style, start, end) { PoolSlot = _recycleTokens ? PoolSlotScalar : (byte)0 };
    }

    private Token NewKey(in Mark start, in Mark end)
        => TryReuse(PoolSlotKey, start, end) ?? new Key(start, end) { PoolSlot = _recycleTokens ? PoolSlotKey : (byte)0 };

    private Token NewValue(in Mark start, in Mark end)
        => TryReuse(PoolSlotValue, start, end) ?? new Value(start, end) { PoolSlot = _recycleTokens ? PoolSlotValue : (byte)0 };

    private Token NewBlockEntry(in Mark start, in Mark end)
        => TryReuse(PoolSlotBlockEntry, start, end) ?? new BlockEntry(start, end) { PoolSlot = _recycleTokens ? PoolSlotBlockEntry : (byte)0 };

    private Token NewBlockEnd(in Mark start, in Mark end)
        => TryReuse(PoolSlotBlockEnd, start, end) ?? new BlockEnd(start, end) { PoolSlot = _recycleTokens ? PoolSlotBlockEnd : (byte)0 };

    private Token NewBlockMappingStart(in Mark start, in Mark end)
        => TryReuse(PoolSlotBlockMappingStart, start, end) ?? new BlockMappingStart(start, end) { PoolSlot = _recycleTokens ? PoolSlotBlockMappingStart : (byte)0 };

    private Token NewBlockSequenceStart(in Mark start, in Mark end)
        => TryReuse(PoolSlotBlockSequenceStart, start, end) ?? new BlockSequenceStart(start, end) { PoolSlot = _recycleTokens ? PoolSlotBlockSequenceStart : (byte)0 };

    private Token NewFlowEntry(in Mark start, in Mark end)
        => TryReuse(PoolSlotFlowEntry, start, end) ?? new FlowEntry(start, end) { PoolSlot = _recycleTokens ? PoolSlotFlowEntry : (byte)0 };

    /// <summary>Takes a free token of a kind that only carries its marks, or returns <see langword="null"/>.</summary>
    private Token TryReuse(byte slot, in Mark start, in Mark end)
    {
        if (_freeTokens[slot].TryPop(out var token))
        {
            token.Reset(start, end);
        }

        return token;
    }

    /// <summary>A stack of the tokens of one kind that are free to use again.</summary>
    private struct TokenStack
    {
        private Slot[] _items;
        private int _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out Token token)
        {
            if (_count == 0)
            {
                token = null;
                return false;
            }

            token = _items[--_count].Token;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(Token token)
        {
            var items = _items;
            var count = _count;
            if (items != null && (uint)count < (uint)items.Length)
            {
                items[count].Token = token;
                _count = count + 1;
                return;
            }

            PushWithResize(token);
        }

        private void PushWithResize(Token token)
        {
            if (_items == null)
            {
                _items = new Slot[4];
            }
            else
            {
                Array.Resize(ref _items, _count * 2);
            }

            _items[_count++].Token = token;
        }

        /// <summary>A token in a struct: an array of references checks the type of every reference stored into it.</summary>
        private struct Slot
        {
            public Token Token;
        }
    }
}

#if NET8_0_OR_GREATER
/// <summary>
/// The character sets of <see cref="Scanner{TBuffer}"/>, outside of it: a static field of a generic class is found
/// through a lookup on every access.
/// </summary>
internal static class ScannerCharacters
{
    /// <summary>The ASCII characters a plain scalar takes as they are in the block context: the printable ones but the space and <c>:</c>.</summary>
    public static readonly System.Buffers.SearchValues<char> PlainScalarRunInBlock = System.Buffers.SearchValues.Create(
        "!\"#$%&'()*+,-./0123456789;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

    /// <summary>The same in a flow collection, where the flow indicators end a plain scalar.</summary>
    public static readonly System.Buffers.SearchValues<char> PlainScalarRunInFlow = System.Buffers.SearchValues.Create(
        "!\"#$%&'()*+-./0123456789;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ\\^_`abcdefghijklmnopqrstuvwxyz|~");
}
#endif
