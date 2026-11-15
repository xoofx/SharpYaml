// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace SharpYaml;

/// <summary>
/// Reads the characters of a stream that may wait for its input, such as a pipe or a socket: a call reads the stream
/// once and returns the characters of the bytes it got, however many were asked for.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="StreamReader"/> cannot be read that way: when a read of its stream fills its buffer, it reads the
/// stream again to give more of the characters it was asked for, and waits there for ever when the writer of the stream
/// is itself waiting for an answer to what it sent. The scanner can ask this reader for a block (see
/// <see cref="Scanner{TBuffer}"/>), which a reader that may wait is otherwise only asked for one character at a time.
/// </para>
/// <para>
/// The stream is read again within a call only while its bytes give no character yet: the start of a byte order mark,
/// or a part of one character. The encoding is the one of the byte order mark the stream starts with, as
/// <see cref="StreamReader"/> detects it, or else the encoding given.
/// </para>
/// <para>
/// Only <see cref="Read(char[], int, int)"/> is implemented, which is what the scanner calls. The stream is not
/// closed with the reader.
/// </para>
/// </remarks>
internal sealed class StreamBlockReader : TextReader
{
    private const int ByteBufferSize = 4096;

    private readonly Stream _stream;
    private readonly Encoding _defaultEncoding;

    // The bytes read from the stream and not decoded yet, in a pooled array: from _bytePosition to _byteLength.
    private byte[]? _bytes;
    private int _bytePosition;
    private int _byteLength;

    // Null until the first bytes of the stream told the encoding.
    private Decoder? _decoder;
    private bool _streamEnded;
    private bool _decoderFlushed;

    // The second of two characters decoded when only one was asked for: see DecodeOne.
    private char _pendingCharacter;
    private bool _hasPendingCharacter;
    private char[]? _pair;

    /// <param name="stream">The stream to read.</param>
    /// <param name="defaultEncoding">The encoding of a stream that starts with no byte order mark.</param>
    public StreamBlockReader(Stream stream, Encoding defaultEncoding)
    {
        _stream = stream;
        _defaultEncoding = defaultEncoding;
        _bytes = ArrayPool<byte>.Shared.Rent(ByteBufferSize);
    }

    /// <inheritdoc />
    public override int Read(char[] buffer, int index, int count)
    {
        ArgumentGuard.ThrowIfNull(buffer);
        if (index < 0 || count < 0 || buffer.Length - index < count)
        {
            throw new ArgumentOutOfRangeException(index < 0 ? nameof(index) : nameof(count));
        }

        var bytes = _bytes ?? throw new ObjectDisposedException(nameof(StreamBlockReader));
        if (count == 0)
        {
            return 0;
        }

        if (_hasPendingCharacter)
        {
            _hasPendingCharacter = false;
            buffer[index] = _pendingCharacter;
            return 1;
        }

        for (; ; )
        {
            if (_decoder is { } decoder && (_bytePosition < _byteLength || (_streamEnded && !_decoderFlushed)))
            {
                var read = count == 1
                    ? DecodeOne(decoder, bytes, buffer, index)
                    : Decode(decoder, bytes, buffer, index, count);
                if (read != 0)
                {
                    return read;
                }
            }

            if (_streamEnded)
            {
                return 0;
            }

            // The bytes read so far gave no character: the stream is read once, and once more only if that is still so.
            ReadStream(bytes);
        }
    }

    private int Decode(Decoder decoder, byte[] bytes, char[] buffer, int index, int count)
    {
        decoder.Convert(bytes, _bytePosition, _byteLength - _bytePosition, buffer, index, count, flush: _streamEnded, out var bytesUsed, out var charsUsed, out var completed);
        _bytePosition += bytesUsed;
        if (_streamEnded && completed)
        {
            _decoderFlushed = true;
        }

        return charsUsed;
    }

    /// <summary>
    /// Decodes one character. The next one may be a pair of surrogates, which a decoder does not split: it is decoded
    /// into two characters, of which the second is given by the next read.
    /// </summary>
    private int DecodeOne(Decoder decoder, byte[] bytes, char[] buffer, int index)
    {
        var pair = _pair ??= new char[2];
        var read = Decode(decoder, bytes, pair, 0, 2);
        if (read == 0)
        {
            return 0;
        }

        buffer[index] = pair[0];
        if (read == 2)
        {
            _pendingCharacter = pair[1];
            _hasPendingCharacter = true;
        }

        return 1;
    }

    /// <summary>Reads the stream once, into the room left after the bytes that are not decoded yet.</summary>
    private void ReadStream(byte[] bytes)
    {
        if (_bytePosition != 0)
        {
            Buffer.BlockCopy(bytes, _bytePosition, bytes, 0, _byteLength - _bytePosition);
            _byteLength -= _bytePosition;
            _bytePosition = 0;
        }

        if (_byteLength == bytes.Length)
        {
            // Not reachable: the decoder takes the bytes of a character it cannot complete, and a mark is 4 bytes.
            throw new InvalidOperationException("The bytes read from the stream give no character.");
        }

        var read = _stream.Read(bytes, _byteLength, bytes.Length - _byteLength);
        if (read <= 0)
        {
            _streamEnded = true;
        }
        else
        {
            _byteLength += read;
        }

        if (_decoder is null)
        {
            DetectEncoding(bytes);
        }
    }

    private static ReadOnlySpan<byte> Utf32LittleEndianMark => new byte[] { 0xFF, 0xFE, 0x00, 0x00 };

    private static ReadOnlySpan<byte> Utf16LittleEndianMark => new byte[] { 0xFF, 0xFE };

    private static ReadOnlySpan<byte> Utf32BigEndianMark => new byte[] { 0x00, 0x00, 0xFE, 0xFF };

    private static ReadOnlySpan<byte> Utf8Mark => new byte[] { 0xEF, 0xBB, 0xBF };

    private static ReadOnlySpan<byte> Utf16BigEndianMark => new byte[] { 0xFE, 0xFF };

    /// <summary>
    /// Sets the decoder from the byte order mark the stream starts with, once the bytes read tell which it is: while
    /// they are the start of a mark, more are needed.
    /// </summary>
    private void DetectEncoding(byte[] bytes)
    {
        // The mark of UTF-32 in little endian starts with the one of UTF-16, so it is tested first.
        var start = new ReadOnlySpan<byte>(bytes, 0, _byteLength);
        var incomplete = false;
        Encoding encoding;
        int markLength;
        if (StartsWithMark(start, Utf32LittleEndianMark, ref incomplete))
        {
            encoding = Encoding.UTF32;
            markLength = 4;
        }
        else if (StartsWithMark(start, Utf16LittleEndianMark, ref incomplete))
        {
            encoding = Encoding.Unicode;
            markLength = 2;
        }
        else if (StartsWithMark(start, Utf32BigEndianMark, ref incomplete))
        {
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            markLength = 4;
        }
        else if (StartsWithMark(start, Utf8Mark, ref incomplete))
        {
            encoding = Encoding.UTF8;
            markLength = 3;
        }
        else if (StartsWithMark(start, Utf16BigEndianMark, ref incomplete))
        {
            encoding = Encoding.BigEndianUnicode;
            markLength = 2;
        }
        else if (incomplete)
        {
            return;
        }
        else
        {
            encoding = _defaultEncoding;
            markLength = 0;
        }

        _decoder = encoding.GetDecoder();
        _bytePosition = markLength;
    }

    /// <summary>
    /// Gets whether the stream starts with <paramref name="mark"/>. When the bytes read so far are only the start of
    /// it and the stream may still give the rest, <paramref name="incomplete"/> is set, and no shorter mark is
    /// accepted before the rest is read.
    /// </summary>
    private bool StartsWithMark(ReadOnlySpan<byte> start, ReadOnlySpan<byte> mark, ref bool incomplete)
    {
        if (incomplete)
        {
            return false;
        }

        if (start.Length >= mark.Length)
        {
            return start.StartsWith(mark);
        }

        incomplete = !_streamEnded && mark.StartsWith(start);
        return false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _bytes is { } bytes)
        {
            _bytes = null;

            // The array held a part of the document: none of it is left readable to the next renter.
            ArrayPool<byte>.Shared.Return(bytes, clearArray: true);
        }

        base.Dispose(disposing);
    }
}