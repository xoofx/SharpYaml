using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// Covers the pooled window through which the scanner reads a reader that is not a string: what it asks of the reader,
/// how large it gets, and what it leaves in the pool.
/// </summary>
[TestClass]
public sealed class YamlReaderWindowTests
{
    // A character that no other test writes: it tells the text of these documents apart in an array of the pool.
    private const char Marker = '☃';

    private static readonly string Markers = new(Marker, 300);

    [TestMethod]
    [DataRow(128)]
    [DataRow(1024)]
    [DataRow(4096)]
    public void StreamReaderWhoseBufferWasFilledIsNotReadAgainBeforeTheParserNeedsIt(int bufferSize)
    {
        // A StreamReader that got exactly as many bytes as its buffer holds does not know that the stream has nothing
        // more for now, and reads it again when it is asked for more characters than it has: over a pipe or a socket
        // whose writer waits for an answer to this document, that read never returns. The scanner therefore asks such
        // a reader for no more than it needs, and the node of the document is read from the bytes that arrived.
        const string Document = "[1, two]\n...\n";
        var stream = new LiveStream(Encoding.UTF8.GetBytes(Document + new string(' ', bufferSize - Document.Length)));
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize, leaveOpen: true);
        var parser = Parser.CreateParser(reader);

        var scalars = new List<string>();
        while (parser.MoveNext() && parser.Current is not SequenceEnd)
        {
            if (parser.Current is Scalar scalar)
            {
                scalars.Add(scalar.Value);
            }
        }

        CollectionAssert.AreEqual(new[] { "1", "two" }, scalars);
        Assert.AreEqual(1, stream.Reads);
    }

    [TestMethod]
    [DataRow("a: 1\n{0}\nb: 2\n")]
    [DataRow("a:{0}1\nb: 2\n")]
    [DataRow("- a\n-{0}b\n")]
    [DataRow("{0}\n--- x\n")]
    [DataRow("[a,{0}b]\n")]
    [DataRow("a: x\n  y\n{0}\n  z\nb: 2\n")]
    [DataRow("a: x\n {0}y\nb: 2\n")]
    [DataRow("- x y\n{0}\n{0}\n- z\n")]
    public void LongRunOfSpacesIsNotKeptInTheWindow(string format)
    {
        // Spaces between two tokens, or between two lines of a plain scalar, are no part of any value: however many
        // there are, the window drops them as it goes instead of growing to hold them all.
        var yaml = string.Format(format, new string(' ', 1_000_000));
        var reader = new RecordingReader(yaml);

        var events = ReadEvents(Parser.CreateParser(reader));

        Assert.AreEqual(ReadEvents(Parser.CreateParser(new StringReader(yaml))), events);
        Assert.IsLessThanOrEqualTo(16 * 1024, reader.LargestBuffer, $"window of {reader.LargestBuffer} characters");
    }

    [TestMethod]
    [DataRow(7)]
    [DataRow(int.MaxValue)]
    public void WindowKeepsNothingOfADocumentReadToItsEnd(int chunk)
    {
        // Comments longer than the window make it move, and a scalar longer than the window makes it grow: neither
        // the windows left behind nor the last one may go back to the pool with text of the document.
        var yaml = "# " + new string(Marker, 5000) + "\na: " + new string(Marker, 9000) + "\n# " + new string(Marker, 5000) + "\nb: 2\n";
        var reader = new RecordingReader(yaml, chunk);

        // The arrays are looked at before anything formats text of the document, which may itself rent them.
        var parser = Parser.CreateParser(reader);
        var scalars = new List<Scalar>();
        while (parser.MoveNext())
        {
            if (parser.Current is Scalar scalar)
            {
                scalars.Add(scalar);
            }
        }

        reader.AssertNoBufferHoldsTheMarker();
        CollectionAssert.AreEqual(new[] { "a", new string(Marker, 9000), "b", "2" }, scalars.Select(scalar => scalar.Value).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowIsReturnedWhenTheDocumentIsMalformed(bool generated)
    {
        var reader = new RecordingReader("Name: " + Markers + "\nAge: [\ud800]\n", chunk: 7);

        Assert.Throws<YamlException>(() => Deserialize(reader, generated));

        reader.AssertNoBufferHoldsTheMarker();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowIsReturnedWhenAValueIsRejected(bool generated)
    {
        var reader = new RecordingReader("Name: " + Markers + "\nAge: old\n# " + Markers + "\n", chunk: 7);

        Assert.Throws<YamlException>(() => Deserialize(reader, generated));

        reader.AssertNoBufferHoldsTheMarker();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowIsReturnedWhenOnlyTheFirstDocumentIsRead(bool generated)
    {
        var reader = new RecordingReader("Name: " + Markers + "\nAge: 36\n---\nName: " + Markers + "\n", chunk: 7);

        var person = Deserialize(reader, generated)!;

        Assert.AreEqual(Markers, person.Name);
        reader.AssertNoBufferHoldsTheMarker();
    }

    [TestMethod]
    public void WindowIsReturnedWhenLoadingAStreamOfNodesFails()
    {
        var reader = new RecordingReader("a: " + Markers + "\nb: [\ud800]\n", chunk: 7);

        Assert.Throws<YamlException>(() => YamlStream.Load(reader));

        reader.AssertNoBufferHoldsTheMarker();
    }

    private static ReadPathPerson? Deserialize(TextReader reader, bool generated)
        => generated
            ? YamlSerializer.Deserialize(reader, ReadPathContext.Default.ReadPathPerson)
            : YamlSerializer.Deserialize<ReadPathPerson>(reader);

    private static string ReadEvents(IParser parser)
    {
        var text = new StringBuilder();
        while (parser.MoveNext())
        {
            text.Append(parser.Current).Append(' ').Append(parser.Current!.Start).Append(' ').Append(parser.Current.End).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>A reader that is not a <see cref="StreamReader"/> and remembers the arrays it was asked to fill.</summary>
    private sealed class RecordingReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;
        private readonly List<char[]> _buffers = new();
        private int _position;

        public RecordingReader(string text, int chunk = int.MaxValue)
        {
            _text = text;
            _chunk = chunk;
        }

        public int LargestBuffer { get; private set; }

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;

        public override int Read(char[] buffer, int index, int count)
        {
            if (!_buffers.Contains(buffer))
            {
                _buffers.Add(buffer);
                LargestBuffer = Math.Max(LargestBuffer, buffer.Length);
            }

            var read = Math.Min(Math.Min(count, _chunk), _text.Length - _position);
            _text.CopyTo(_position, buffer, index, read);
            _position += read;
            return read;
        }

        public void AssertNoBufferHoldsTheMarker()
        {
            Assert.IsNotEmpty(_buffers);
            foreach (var buffer in _buffers)
            {
                Assert.AreEqual(0, buffer.Count(character => character == Marker), $"array of {buffer.Length} characters");
            }
        }
    }

    /// <summary>
    /// A stream that cannot seek and whose writer is still there, as a pipe or a socket: it gives the bytes it has,
    /// and a read once it gave them all would wait for ever, which this one reports instead.
    /// </summary>
    private sealed class LiveStream : Stream
    {
        private readonly byte[] _bytes;
        private int _position;

        public LiveStream(byte[] bytes) => _bytes = bytes;

        public int Reads { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            if (_position == _bytes.Length)
            {
                throw new InvalidOperationException("This read would wait for the next document.");
            }

            var read = Math.Min(count, _bytes.Length - _position);
            Array.Copy(_bytes, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
