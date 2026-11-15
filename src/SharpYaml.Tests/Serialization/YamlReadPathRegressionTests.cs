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
/// Covers what the read path reuses or defers to avoid allocations: tokens and events handed out again, the strings of
/// repeated mapping keys, the explicit key set created only for a merge key, and readers read at once.
/// </summary>
[TestClass]
public sealed class YamlReadPathRegressionTests
{
    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions? options, bool generated)
    {
        if (!generated)
        {
            return YamlSerializer.Deserialize<T>(yaml, options);
        }

        var context = options is null ? ReadPathContext.Default : new ReadPathContext(options);
        return (T?)YamlSerializer.Deserialize(yaml, typeof(T), context);
    }

    [TestMethod]
    public void PublicParserReturnsADistinctObjectPerEvent()
    {
        var parser = Parser.CreateParser(new StringReader("- a\n- b\n- {c: d}\n- [e]\n"));
        var events = new List<ParsingEvent>();
        while (parser.MoveNext())
        {
            events.Add(parser.Current!);
        }

        Assert.AreEqual(events.Count, events.Distinct().Count());
        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e" }, events.OfType<Scalar>().Select(scalar => scalar.Value).ToArray());
    }

    [TestMethod]
    public void PublicScannerReturnsADistinctObjectPerToken()
    {
        var scanner = new Scanner<LookAheadBuffer>(new LookAheadBuffer(new StringReader("a: 1\nb: 2\nc: [3, 4]\n"), Scanner<LookAheadBuffer>.MaxBufferLength));
        var tokens = new List<SharpYaml.Tokens.Token>();
        while (scanner.MoveNext())
        {
            tokens.Add(scanner.Current);
        }

        Assert.AreEqual(tokens.Count, tokens.Distinct().Count());
        CollectionAssert.AreEqual(
            new[] { "a", "1", "b", "2", "c", "3", "4" },
            tokens.OfType<SharpYaml.Tokens.Scalar>().Select(scalar => scalar.Value).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void NodesKeptByACallbackStayValidAfterTheReaderMoved(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var model = Deserialize<ReadPathPerson>("a: 1\nb: two\nName: Ada\nc:\n  - x\n  - y: z\nd: 4\n", options, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.HasCount(4, members);
        Assert.AreEqual("1", ((YamlValue)members[0].Value!).Value);
        Assert.AreEqual("two", ((YamlValue)members[1].Value!).Value);
        var sequence = (YamlSequence)members[2].Value!;
        Assert.AreEqual("x", ((YamlValue)sequence[0]).Value);
        var mapping = (YamlMapping)sequence[1];
        var pair = mapping.Single();
        Assert.AreEqual("y", ((YamlValue)pair.Key).Value);
        Assert.AreEqual("z", ((YamlValue)pair.Value!).Value);
        Assert.AreEqual("4", ((YamlValue)members[3].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void AnchoredNodesAreReplayedAfterManyOtherEvents(bool generated)
    {
        var yaml = new StringBuilder("Items:\n  - &first {Name: Ada, Age: 36}\n");
        for (var i = 0; i < 40; i++)
        {
            yaml.Append("  - {Name: n").Append(i).Append(", Age: ").Append(i).Append("}\n");
        }

        yaml.Append("  - *first\n");

        var model = Deserialize<ReadPathPeople>(yaml.ToString(), options: null, generated)!;

        Assert.HasCount(42, model.Items);
        Assert.AreEqual("Ada", model.Items[41].Name);
        Assert.AreEqual(36, model.Items[41].Age);
        Assert.AreEqual("n39", model.Items[40].Name);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void RepeatedAndDistinctKeysKeepTheirText(bool generated)
    {
        // More distinct keys than the scanner remembers, keys longer than it remembers, and keys that only differ by case.
        var expected = new List<Dictionary<string, int>>();
        var yaml = new StringBuilder();
        for (var item = 0; item < 3; item++)
        {
            var dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < 150; i++)
            {
                var key = i % 3 == 0 ? "key_" + i : i % 3 == 1 ? "KEY_" + i : new string((char)('a' + (i % 26)), 40) + i;
                dictionary[key] = (item * 1000) + i;
                yaml.Append(dictionary.Count == 1 ? "- " : "  ").Append(key).Append(": ").Append((item * 1000) + i).Append('\n');
            }

            expected.Add(dictionary);
        }

        var actual = Deserialize<List<Dictionary<string, int>>>(yaml.ToString(), options: null, generated)!;

        Assert.HasCount(expected.Count, actual);
        for (var item = 0; item < expected.Count; item++)
        {
            CollectionAssert.AreEquivalent(expected[item].ToArray(), actual[item].ToArray());
        }
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void KeyFollowedByAColonInItsTextIsNotTruncated(bool generated)
    {
        var actual = Deserialize<Dictionary<string, string>>("a:b: 1\nhttp://x: 2\nc : 3\n", options: null, generated)!;

        Assert.AreEqual("1", actual["a:b"]);
        Assert.AreEqual("2", actual["http://x"]);
        Assert.AreEqual("3", actual["c"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExplicitKeyReadBeforeAMergeKeyIsNotOverridden(bool generated)
    {
        var model = Deserialize<ReadPathPerson>("Age: 38\n<<:\n  Name: Ada\n  Age: 37\n", options: null, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(38, model.Age);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExplicitKeyReadAfterAMergeKeyOverridesTheMergedOne(bool generated)
    {
        var model = Deserialize<ReadPathPerson>("<<:\n  Name: Ada\n  Age: 37\nAge: 38\n", options: null, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(38, model.Age);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExplicitKeysAreKnownToEveryMergeKeyOfAMapping(bool generated)
    {
        var model = Deserialize<ReadPathPerson>("Name: Ada\n<<: {Name: first, Age: 1}\nAge: 38\n<<: {Name: second, Age: 2}\n", options: null, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(38, model.Age);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void UnmappedExplicitKeyHidesTheMergedOne(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var model = Deserialize<ReadPathPerson>("Extra: explicit\n<<:\n  Extra: merged\n  Name: Ada\n", options, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.HasCount(1, members);
        Assert.AreEqual("explicit", ((YamlValue)members[0].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExtensionDataKeepsTheExplicitValueOverTheMergedOne(bool generated)
    {
        var model = Deserialize<ReadPathWithExtensionData>("Extra: explicit\nName: Ada\n<<:\n  Extra: merged\n  Other: merged\n  Name: Bob\n", options: null, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual("explicit", model.Extra!["Extra"]?.ToString());
        Assert.AreEqual("merged", model.Extra!["Other"]?.ToString());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ConstructorParametersReadBeforeAMergeKeyAreNotOverridden(bool generated)
    {
        var model = Deserialize<ReadPathCtorPerson>("Age: 38\nNote: explicit\n<<:\n  Name: Ada\n  Age: 37\n  Note: merged\n", options: null, generated)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(38, model.Age);
        Assert.AreEqual("explicit", model.Note);
    }

    [TestMethod]
    public void GeneratedLastWinsContextStillKnowsItsExplicitKeys()
    {
        var context = ReadPathLastWinsContext.Default;

        var model = YamlSerializer.Deserialize("Age: 1\nAge: 2\n<<:\n  Age: 3\n  Name: Ada\n", context.ReadPathPerson)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(2, model.Age);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void DuplicateKeyIsStillDetectedWithoutAMergeKey(bool generated)
    {
        var exception = Assert.Throws<YamlException>(() => Deserialize<ReadPathPerson>("Name: Ada\nAge: 1\nName: Bob\n", options: null, generated));

        StringAssert.Contains(exception.Message, "Duplicate mapping key 'Name'");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void SeekableAndForwardOnlyStreamsReadTheSameDocument(bool generated)
    {
        // Long enough to cross the buffers of the reader, with characters of one to four UTF-8 bytes.
        var yaml = new StringBuilder("Items:\n");
        for (var i = 0; i < 400; i++)
        {
            yaml.Append("  - Name: \"é ").Append(i).Append(" 漢 😁\"\n    Age: ").Append(i).Append('\n');
        }

        var expected = Deserialize<ReadPathPeople>(yaml.ToString(), options: null, generated)!;
        var bytes = Encoding.UTF8.GetBytes(yaml.ToString());

        foreach (var stream in new Stream[] { new MemoryStream(bytes), new ForwardOnlyStream(bytes) })
        {
            var actual = generated
                ? YamlSerializer.Deserialize(stream, ReadPathContext.Default.ReadPathPeople)!
                : YamlSerializer.Deserialize<ReadPathPeople>(stream)!;

            Assert.HasCount(expected.Items.Count, actual.Items);
            for (var i = 0; i < expected.Items.Count; i++)
            {
                Assert.AreEqual(expected.Items[i].Name, actual.Items[i].Name);
                Assert.AreEqual(expected.Items[i].Age, actual.Items[i].Age);
            }
        }
    }

    [TestMethod]
    public void StreamWithAByteOrderMarkIsRead()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Name: Ada\nAge: 36\n")).ToArray();

        foreach (var stream in new Stream[] { new MemoryStream(bytes), new ForwardOnlyStream(bytes) })
        {
            var model = YamlSerializer.Deserialize<ReadPathPerson>(stream)!;

            Assert.AreEqual("Ada", model.Name);
            Assert.AreEqual(36, model.Age);
        }
    }

    [TestMethod]
    public void StreamReaderAlreadyReadFromIsReadFromItsPosition()
    {
        var bytes = Encoding.UTF8.GetBytes("# header\nName: Ada\nAge: 36\n" + new string('#', 5000) + "\n");
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 128);
        Assert.AreEqual("# header", reader.ReadLine());

        var model = YamlSerializer.Deserialize<ReadPathPerson>(reader)!;

        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(36, model.Age);
    }

    [TestMethod]
    public void ParserOverASeekableStreamReportsTheSameEventsAsOverAString()
    {
        const string Yaml = "---\na: [1, 2]\n...\n---\n- &x b\n- *x\n";

        static string ReadAll(IParser parser)
        {
            var text = new StringBuilder();
            while (parser.MoveNext())
            {
                text.Append(parser.Current).Append(' ').Append(parser.Current!.Start).Append('\n');
            }

            return text.ToString();
        }

        var expected = ReadAll(Parser.CreateParser(new StringReader(Yaml)));

        Assert.AreEqual(expected, ReadAll(Parser.CreateParser(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(Yaml))))));
        Assert.AreEqual(expected, ReadAll(Parser.CreateParser(new StreamReader(new ForwardOnlyStream(Encoding.UTF8.GetBytes(Yaml))))));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void DepthLimitStillAppliesToTheFastPath(bool generated)
    {
        var options = new YamlSerializerOptions { MaxDepth = 8 };
        var yaml = string.Concat(Enumerable.Repeat("- ", 9)) + "x\n";

        Assert.Throws<YamlException>(() => Deserialize<object>(yaml, options, generated));
    }

    /// <summary>A stream that cannot seek, as a pipe or a network stream, and returns a few bytes per read.</summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly byte[] _bytes;
        private int _position;

        public ForwardOnlyStream(byte[] bytes) => _bytes = bytes;

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
            var read = Math.Min(Math.Min(count, 7), _bytes.Length - _position);
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

public sealed class ReadPathPerson
{
    public string? Name { get; set; }

    public int Age { get; set; }
}

public sealed class ReadPathPeople
{
    public List<ReadPathPerson> Items { get; set; } = new();
}

public sealed class ReadPathWithExtensionData
{
    public string? Name { get; set; }

    [YamlExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

public sealed class ReadPathCtorPerson
{
    [YamlConstructor]
    public ReadPathCtorPerson(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }

    public int Age { get; }

    public string? Note { get; set; }
}

[YamlSerializable(typeof(ReadPathPerson))]
[YamlSerializable(typeof(ReadPathPeople))]
[YamlSerializable(typeof(ReadPathWithExtensionData))]
[YamlSerializable(typeof(ReadPathCtorPerson))]
[YamlSerializable(typeof(List<Dictionary<string, int>>))]
[YamlSerializable(typeof(Dictionary<string, string>))]
[YamlSerializable(typeof(object))]
internal partial class ReadPathContext : YamlSerializerContext
{
    public ReadPathContext()
    {
    }

    public ReadPathContext(YamlSerializerOptions options) : base(options)
    {
    }
}

[YamlSourceGenerationOptions(DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins)]
[YamlSerializable(typeof(ReadPathPerson))]
internal partial class ReadPathLastWinsContext : YamlSerializerContext
{
}
