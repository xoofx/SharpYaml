using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// Covers what the write path does to avoid work: the pooled output buffer of the serializer, the shortcut that
/// decides without the number parsers that a string is not ambiguous, and the converters shared per options.
/// </summary>
[TestClass]
public sealed class YamlWritePathRegressionTests
{
    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    private static string Serialize<T>(T value, bool generated)
        => generated ? YamlSerializer.Serialize(value, typeof(T), WritePathContext.Default) : YamlSerializer.Serialize(value);

    private static WritePathDocument CreateLargeDocument()
    {
        // Longer than the buffer the serializer flushes to a writer, with values of every length around its size.
        var document = new WritePathDocument();
        for (var i = 0; i < 300; i++)
        {
            document.Items.Add(new WritePathItem { Name = new string((char)('a' + (i % 26)), (i * 37) % 90) + i, Count = i * 1000003, Ratio = i / 7.0, Enabled = i % 3 == 0 });
        }

        document.Text = new string('x', 10_000);
        return document;
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void EveryDestinationReceivesTheSameDocument(bool generated)
    {
        var document = CreateLargeDocument();
        var expected = Serialize(document, generated);
        Assert.IsGreaterThan(20_000, expected.Length);
        Assert.EndsWith("\n", expected);

        using var textWriter = new StringWriter();
        var stream = new MemoryStream();
        var bufferWriter = new ArrayBufferWriter<char>();
        if (generated)
        {
            var typeInfo = WritePathContext.Default.WritePathDocument;
            YamlSerializer.Serialize(textWriter, document, typeInfo);
            YamlSerializer.Serialize(stream, document, typeInfo);
            YamlSerializer.Serialize(bufferWriter, document, WritePathContext.Default);
        }
        else
        {
            YamlSerializer.Serialize(textWriter, document);
            YamlSerializer.Serialize(stream, document);
            YamlSerializer.Serialize(bufferWriter, document);
        }

        Assert.AreEqual(expected, textWriter.ToString());
        Assert.AreEqual(expected, Encoding.UTF8.GetString(stream.ToArray()));
        Assert.AreEqual(expected, bufferWriter.WrittenSpan.ToString());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void LargeDocumentRoundTrips(bool generated)
    {
        var document = CreateLargeDocument();
        var yaml = Serialize(document, generated);

        var actual = generated
            ? YamlSerializer.Deserialize(yaml, WritePathContext.Default.WritePathDocument)!
            : YamlSerializer.Deserialize<WritePathDocument>(yaml)!;

        Assert.AreEqual(document.Text, actual.Text);
        Assert.HasCount(document.Items.Count, actual.Items);
        for (var i = 0; i < document.Items.Count; i++)
        {
            Assert.AreEqual(document.Items[i].Name, actual.Items[i].Name);
            Assert.AreEqual(document.Items[i].Count, actual.Items[i].Count);
            Assert.AreEqual(document.Items[i].Ratio, actual.Items[i].Ratio);
            Assert.AreEqual(document.Items[i].Enabled, actual.Items[i].Enabled);
        }
    }

    [TestMethod]
    public void RepeatedSerializationOfDifferentSizesIsIndependent()
    {
        // The buffer of one call is sized from the previous one: a short document after a long one, and back.
        var large = CreateLargeDocument();
        var small = new WritePathDocument { Text = "t" };
        var largeYaml = YamlSerializer.Serialize(large);
        var smallYaml = YamlSerializer.Serialize(small);

        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual(largeYaml, YamlSerializer.Serialize(large));
            Assert.AreEqual(smallYaml, YamlSerializer.Serialize(small));
        }
    }

    [TestMethod]
    public void SerializationInsideAConverterDoesNotDisturbTheOuterDocument()
    {
        var options = new YamlSerializerOptions { Converters = [new NestedSerializationConverter()] };
        var value = new WritePathHolder { Before = "b", Nested = new WritePathNested { Value = 5 }, After = "a" };

        var yaml = YamlSerializer.Serialize(value, options);

        Assert.AreEqual("Before: b\nNested: \"Value: 5\\n\"\nAfter: a\n", yaml);
    }

    [TestMethod]
    public void SerializationAfterAFailedOneStartsFromAnEmptyDocument()
    {
        var options = new YamlSerializerOptions { Converters = [new ThrowingConverter()] };
        var value = new WritePathHolder { Before = new string('b', 3000), Nested = new WritePathNested { Value = 5 }, After = "a" };

        Assert.Throws<Exception>(() => YamlSerializer.Serialize(value, options));
        using var writer = new StringWriter();
        Assert.Throws<Exception>(() => YamlSerializer.Serialize(writer, value, options));

        Assert.AreEqual("Value: 5\n", YamlSerializer.Serialize(new WritePathNested { Value = 5 }));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void StringsThatLookLikeOtherScalarsStayStrings(bool generated)
    {
        // Each of these must come back as the same string when it is read without a target type: the ones that read
        // as null, a boolean or a number are quoted, whatever shortcut decided that the others are not ambiguous.
        var values = new List<string>
        {
            "null", "Null", "NULL", "nUlL", "~", "true", "True", "TRUE", "tRuE", "false", "False", "FALSE",
            "0", "123", "-123", "+12", "1_000", "_1", "1__", "0x1F", "0X1f", "0o17", "0O17", "0b11", "0B11",
            "1.5", "-1.5", ".5", "5.", "1e5", "1E+5", "1.5e-3", "1_0.5", ".inf", ".Inf", ".INF", "+.inf", "-.inf", "-.INF",
            ".nan", ".NaN", ".NAN", "NaN", "nan", "Infinity", "-Infinity", "infinity", "I_n_f_i_n_i_t_y", "N_a_N",
            "<<", "-", "- a", "?", "? b", "a: b", "a #c", "#c", ":", "---", "...", "--- a",
            " lead", "trail ", "\tx", "tést", "∞", "12abc", "9f14b8a8", "2026-02-28", "2026.02.28.1", "1-2",
            "none", "nil", "no", "tier", "test", "info", "index", "items", "team-00", "feature_flag_001", "inf", "nan-x",
            "e5", "x1F", "o17", "+", "-.", ".", "..", "+.", "1e", "1e+", "0x", "0xZZ", "0o8", "a", "Z", "n", "t", "f", "i",
            "/api/v1/x", "$var", "(x)", "a=b", "user@example.com", "back\\slash", "say \"hi\"", "it's", "50%", "a,b", "[x]", "{x}",
            "line1\nline2", "\u0001", "\u0085", "﻿bom", "😁", "key with spaces",
        };

        var yaml = Serialize(values, generated);
        var actual = generated
            ? (List<object>?)YamlSerializer.Deserialize(yaml, typeof(List<object>), WritePathContext.Default)
            : YamlSerializer.Deserialize<List<object>>(yaml);

        Assert.IsNotNull(actual);
        Assert.HasCount(values.Count, actual);
        for (var i = 0; i < values.Count; i++)
        {
            Assert.AreEqual(values[i], actual[i], $"value {i}: '{values[i]}' in\n{yaml}");
        }
    }

    [TestMethod]
    public void PlainStringsAreNotQuoted()
    {
        var yaml = YamlSerializer.Serialize(new List<string> { "service-0000", "team-00", "feature_flag_001", "/api/v1/x", "us-east-1", "tier", "9f14b8a8", "2026.02.28.1" });

        Assert.AreEqual("- service-0000\n- team-00\n- feature_flag_001\n- /api/v1/x\n- us-east-1\n- tier\n- 9f14b8a8\n- 2026.02.28.1\n", yaml);
    }

    [TestMethod]
    public void MemberNamesThatNeedQuotesAreQuotedEveryTime()
    {
        var yaml = YamlSerializer.Serialize(new List<Dictionary<string, int>>
        {
            new() { ["plain"] = 1, ["needs: quotes"] = 2 },
            new() { ["plain"] = 3, ["needs: quotes"] = 4 },
        });

        Assert.AreEqual("- plain: 1\n  \"needs: quotes\": 2\n- plain: 3\n  \"needs: quotes\": 4\n", yaml);
    }

    [TestMethod]
    public void NumbersAreWrittenWithInvariantCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
        try
        {
            var yaml = YamlSerializer.Serialize(new WritePathNumbers());

            Assert.AreEqual(
                "Byte: 255\nSByte: -128\nInt16: -32768\nUInt16: 65535\nInt32: -2147483648\nUInt32: 4294967295\n" +
                "Int64: -9223372036854775808\nUInt64: 18446744073709551615\nDecimal: -79228162514264337593543950335\n" +
                "Double: -1.7976931348623157E+308\nSingle: 3.4028235E+38\nSmall: 0.1\nInfinity: .inf\nNaN: .nan\n",
                yaml);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void ConvertersAreResolvedOncePerOptionsAndNotSharedBetweenOptions()
    {
        CountingFactory.Created = 0;
        var first = new YamlSerializerOptions { Converters = [new CountingFactory("first")] };
        var second = first with { IndentSize = 4 };
        var nested = new WritePathNested { Value = 5 };

        Assert.AreEqual("first-5\n", YamlSerializer.Serialize(nested, first));
        Assert.AreEqual("first-5\n", YamlSerializer.Serialize(nested, first));
        Assert.AreEqual(5, YamlSerializer.Deserialize<WritePathNested>("first-5", first)!.Value);
        Assert.AreEqual(1, CountingFactory.Created);

        // A copy of the options resolves its own converters.
        Assert.AreEqual("first-5\n", YamlSerializer.Serialize(nested, second));
        Assert.AreEqual(2, CountingFactory.Created);

        var other = new YamlSerializerOptions { Converters = [new CountingFactory("other")] };
        Assert.AreEqual("other-5\n", YamlSerializer.Serialize(nested, other));
        Assert.AreEqual("first-5\n", YamlSerializer.Serialize(nested, first));
    }

    private sealed class NestedSerializationConverter : YamlConverter<WritePathNested>
    {
        public override WritePathNested Read(YamlReader reader) => throw new NotSupportedException();

        public override void Write(YamlWriter writer, WritePathNested value)
            => writer.WriteString(YamlSerializer.Serialize(value));
    }

    private sealed class ThrowingConverter : YamlConverter<WritePathNested>
    {
        public override WritePathNested Read(YamlReader reader) => throw new NotSupportedException();

        public override void Write(YamlWriter writer, WritePathNested value) => throw new Exception("stop");
    }

    private sealed class CountingFactory : YamlConverterFactory
    {
        private readonly string _prefix;

        public CountingFactory(string prefix) => _prefix = prefix;

        public static int Created { get; set; }

        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(WritePathNested);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            Created++;
            return new PrefixConverter(_prefix);
        }
    }

    private sealed class PrefixConverter : YamlConverter<WritePathNested>
    {
        private readonly string _prefix;

        public PrefixConverter(string prefix) => _prefix = prefix;

        public override WritePathNested Read(YamlReader reader)
        {
            var text = reader.GetScalarValue();
            reader.Read();
            return new WritePathNested { Value = int.Parse(text.Substring(_prefix.Length + 1), System.Globalization.CultureInfo.InvariantCulture) };
        }

        public override void Write(YamlWriter writer, WritePathNested value)
            => writer.WriteScalar(_prefix + "-" + value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed class WritePathDocument
{
    public string? Text { get; set; }

    public List<WritePathItem> Items { get; set; } = new();
}

public sealed class WritePathItem
{
    public string? Name { get; set; }

    public int Count { get; set; }

    public double Ratio { get; set; }

    public bool Enabled { get; set; }
}

public sealed class WritePathHolder
{
    public string? Before { get; set; }

    public WritePathNested? Nested { get; set; }

    public string? After { get; set; }
}

public sealed class WritePathNested
{
    public int Value { get; set; }
}

public sealed class WritePathNumbers
{
    public byte Byte { get; set; } = byte.MaxValue;

    public sbyte SByte { get; set; } = sbyte.MinValue;

    public short Int16 { get; set; } = short.MinValue;

    public ushort UInt16 { get; set; } = ushort.MaxValue;

    public int Int32 { get; set; } = int.MinValue;

    public uint UInt32 { get; set; } = uint.MaxValue;

    public long Int64 { get; set; } = long.MinValue;

    public ulong UInt64 { get; set; } = ulong.MaxValue;

    public decimal Decimal { get; set; } = decimal.MinValue;

    public double Double { get; set; } = double.MinValue;

    public float Single { get; set; } = float.MaxValue;

    public double Small { get; set; } = 0.1;

    public double Infinity { get; set; } = double.PositiveInfinity;

    public double NaN { get; set; } = double.NaN;
}

[YamlSerializable(typeof(WritePathDocument))]
[YamlSerializable(typeof(WritePathItem))]
[YamlSerializable(typeof(List<string>))]
[YamlSerializable(typeof(List<object>))]
internal partial class WritePathContext : YamlSerializerContext
{
}
