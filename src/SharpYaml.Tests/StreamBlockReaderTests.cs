using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;
using SharpYaml.Tests.Serialization;

namespace SharpYaml.Tests;

/// <summary>
/// Covers the reader of a stream that cannot seek: it decodes what a <see cref="StreamReader"/> decodes, and reads
/// its stream once per call.
/// </summary>
[TestClass]
public sealed class StreamBlockReaderTests
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // Characters of one, two, three and four UTF-8 bytes, and of one and two UTF-16 units.
    private const string Text = "Name: \"é 漢 😁 Ａ\"\nAge: 36\n# 😁😁😁 end\n";

    public static IEnumerable<object[]> Encodings => new[]
    {
        new object[] { "utf-8", false },
        new object[] { "utf-8", true },
        new object[] { "utf-16", true },
        new object[] { "utf-16BE", true },
        new object[] { "utf-32", true },
        new object[] { "utf-32BE", true },
    };

    private static byte[] Encode(string name, bool mark, string text)
    {
        var encoding = Encoding.GetEncoding(name);
        return mark ? encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray() : encoding.GetBytes(text);
    }

    private static string ReadAll(TextReader reader, int count)
    {
        var text = new StringBuilder();
        var buffer = new char[count + 3];
        int read;
        while ((read = reader.Read(buffer, 2, count)) != 0)
        {
            Assert.IsTrue(read > 0 && read <= count, $"read {read} characters for {count}");
            text.Append(buffer, 2, read);
        }

        Assert.AreEqual(0, reader.Read(buffer, 2, count));
        return text.ToString();
    }

    [TestMethod]
    [DynamicData(nameof(Encodings))]
    public void DecodesWhatAStreamReaderDecodes(string encoding, bool mark)
    {
        var bytes = Encode(encoding, mark, Text);
        using var expectedReader = new StreamReader(new MemoryStream(bytes), Utf8, detectEncodingFromByteOrderMarks: true);
        var expected = expectedReader.ReadToEnd();
        Assert.AreEqual(Text, expected);

        // Every size of the reads of the stream splits the mark and the characters at other bytes, and every number
        // of characters asked for splits the pairs of surrogates.
        foreach (var chunk in new[] { 1, 2, 3, 4, 5, 7, int.MaxValue })
        {
            foreach (var count in new[] { 1, 2, 3, 64 })
            {
                using var reader = new StreamBlockReader(new ChunkedStream(bytes, chunk), Utf8);
                Assert.AreEqual(expected, ReadAll(reader, count), $"chunk {chunk}, count {count}");
            }
        }
    }

    [TestMethod]
    [DataRow(new byte[0], "")]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, "")]
    [DataRow(new byte[] { 0xFF, 0xFE }, "")]
    [DataRow(new byte[] { 0xFE, 0xFF }, "")]
    [DataRow(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, "")]
    [DataRow(new byte[] { 0x00, 0x00, 0xFE, 0xFF }, "")]
    [DataRow(new byte[] { 0x61 }, "a")]
    [DataRow(new byte[] { 0xFF, 0xFE, 0x61, 0x00 }, "a")]
    [DataRow(new byte[] { 0xFF, 0xFE, 0x00, 0x30 }, "　")]
    [DataRow(new byte[] { 0xEF, 0xBC, 0xA1 }, "Ａ")]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF }, "﻿")]
    public void ShortStreamsAreDecoded(byte[] bytes, string expected)
    {
        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            using var reader = new StreamBlockReader(new ChunkedStream(bytes, chunk), Utf8);
            Assert.AreEqual(expected, ReadAll(reader, 16), $"chunk {chunk}");
        }
    }

    [TestMethod]
    public void InvalidBytesFailAsWithAStreamReader()
    {
        // Without a mark, the encoding given decides: the one of the serializer rejects bytes that are not UTF-8.
        var invalid = new byte[] { 0x61, 0xC3, 0x28, 0x62 };
        Assert.Throws<DecoderFallbackException>(() => new StreamReader(new MemoryStream(invalid), Utf8, detectEncodingFromByteOrderMarks: true).ReadToEnd());
        Assert.Throws<DecoderFallbackException>(() => ReadAll(new StreamBlockReader(new ChunkedStream(invalid, int.MaxValue), Utf8), 16));

        // After a mark of UTF-8, they are replaced.
        var marked = new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0xC3, 0x28, 0x62, 0xE6 };
        var expected = new StreamReader(new MemoryStream(marked), Utf8, detectEncodingFromByteOrderMarks: true).ReadToEnd();
        foreach (var chunk in new[] { 1, 3, int.MaxValue })
        {
            Assert.AreEqual(expected, ReadAll(new StreamBlockReader(new ChunkedStream(marked, chunk), Utf8), 16), $"chunk {chunk}");
        }
    }

    [TestMethod]
    public void EachCallReadsTheStreamOnce()
    {
        // 10,000 bytes that the stream gives 100 at a time: a call returns the characters of one read of the stream,
        // however many are asked for.
        var bytes = Encoding.UTF8.GetBytes(new string('a', 10_000));
        var stream = new ChunkedStream(bytes, 100);
        using var reader = new StreamBlockReader(stream, Utf8);
        var buffer = new char[8192];

        Assert.AreEqual(100, reader.Read(buffer, 0, buffer.Length));
        Assert.AreEqual(1, stream.Reads);
        Assert.AreEqual(100, reader.Read(buffer, 0, buffer.Length));
        Assert.AreEqual(2, stream.Reads);

        // What a read of the stream gave beyond the characters asked for is returned without reading the stream.
        Assert.AreEqual(30, reader.Read(buffer, 0, 30));
        Assert.AreEqual(3, stream.Reads);
        Assert.AreEqual(70, reader.Read(buffer, 0, buffer.Length));
        Assert.AreEqual(3, stream.Reads);
    }

    [TestMethod]
    [DataRow(1024)]
    [DataRow(4096)]
    [DataRow(8192)]
    public void DocumentIsParsedFromTheBytesThatArrived(int size)
    {
        // A stream whose writer waits for an answer to the document it sent, of sizes that fill the buffers a reader
        // may have: the node of the document is read with one read of the stream, and no read is made that would wait.
        const string Document = "[1, two]\n...\n";
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes(Document + new string(' ', size - Document.Length)), int.MaxValue) { Waits = true };
        using var reader = new StreamBlockReader(stream, Utf8);
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
        Assert.IsLessThanOrEqualTo((size + 4095) / 4096, stream.Reads);
    }

    [TestMethod]
    [DynamicData(nameof(Encodings))]
    public void SerializerReadsAStreamThatCannotSeekInEveryEncoding(string encoding, bool mark)
    {
        var yaml = new StringBuilder("Items:\n");
        for (var i = 0; i < 300; i++)
        {
            yaml.Append("  - Name: \"é ").Append(i).Append(" 漢 😁\"\n    Age: ").Append(i).Append('\n');
        }

        var bytes = Encode(encoding, mark, yaml.ToString());
        var expected = YamlSerializer.Deserialize<ReadPathPeople>(new MemoryStream(bytes))!;
        Assert.HasCount(300, expected.Items);

        foreach (var chunk in new[] { 1, 7, 1000, int.MaxValue })
        {
            var reflected = YamlSerializer.Deserialize<ReadPathPeople>(new ChunkedStream(bytes, chunk))!;
            var generated = YamlSerializer.Deserialize(new ChunkedStream(bytes, chunk), ReadPathContext.Default.ReadPathPeople)!;
            foreach (var actual in new[] { reflected, generated })
            {
                Assert.HasCount(300, actual.Items);
                for (var i = 0; i < 300; i++)
                {
                    Assert.AreEqual(expected.Items[i].Name, actual.Items[i].Name);
                    Assert.AreEqual(expected.Items[i].Age, actual.Items[i].Age);
                }
            }
        }
    }

    [TestMethod]
    public void SerializerLeavesTheStreamOpen()
    {
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes("Name: Ada\nAge: 36\n"), 5);

        Assert.AreEqual("Ada", YamlSerializer.Deserialize<ReadPathPerson>(stream)!.Name);
        Assert.IsFalse(stream.Disposed);
    }

    [TestMethod]
    public void ReaderRejectsWhatATextReaderRejects()
    {
        var reader = new StreamBlockReader(new ChunkedStream(new byte[] { 0x61 }, 1), Utf8);
        Assert.Throws<ArgumentNullException>(() => reader.Read(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(new char[2], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(new char[2], 1, 2));
        Assert.AreEqual(0, reader.Read(new char[2], 0, 0));

        reader.Dispose();
        reader.Dispose();
        Assert.Throws<ObjectDisposedException>(() => reader.Read(new char[2], 0, 1));
    }

    /// <summary>A stream that cannot seek and gives at most <c>chunk</c> bytes per read.</summary>
    private sealed class ChunkedStream : Stream
    {
        private readonly byte[] _bytes;
        private readonly int _chunk;
        private int _position;

        public ChunkedStream(byte[] bytes, int chunk)
        {
            _bytes = bytes;
            _chunk = chunk;
        }

        /// <summary>Whether the writer of the stream is still there: a read once all the bytes were given would wait for ever.</summary>
        public bool Waits { get; init; }

        public int Reads { get; private set; }

        public bool Disposed { get; private set; }

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
            if (Waits && _position == _bytes.Length)
            {
                throw new InvalidOperationException("This read would wait for the next document.");
            }

            var read = Math.Min(Math.Min(count, _chunk), _bytes.Length - _position);
            Array.Copy(_bytes, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
