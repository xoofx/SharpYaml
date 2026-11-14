using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;

namespace SharpYaml.Tests;

[TestClass]
public sealed class YamlTestSuiteTests
{
    public static IEnumerable<object[]> Cases()
    {
        using var stream = typeof(YamlTestSuiteTests).Assembly.GetManifestResourceStream("SharpYaml.Tests.files.yaml-test-suite.json")!;
        using var document = JsonDocument.Parse(stream);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            yield return [item.GetProperty("id").GetString()!, item.GetProperty("yaml").GetString()!,
                item.GetProperty("error").GetBoolean(), item.GetProperty("events").GetString()!, item.GetProperty("json").GetString()!];
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void OfficialEvents(string id, string yaml, bool error, string expected, string json)
    {
        // Exercise both the direct string buffer and the bounded streaming buffer.
        Verify(id, Parser.CreateParser(new StringReader(yaml)), error, expected);
        Verify(id, new Parser<LookAheadBuffer>(new LookAheadBuffer(new StringReader(yaml), Scanner<LookAheadBuffer>.MaxBufferLength)), error, expected);
    }

    public static IEnumerable<object[]> LoadCases()
    {
        foreach (var data in Cases())
        {
            if (!(bool)data[2])
            {
                yield return [data[0], data[1], data[4], false];
                yield return [data[0], data[1], data[4], true];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(LoadCases))]
    public void OfficialObjectLoading(string id, string yaml, string json, bool preserve)
    {
        // Parser fixtures include duplicate keys. Loading policy is separate from
        // syntax; exercise both alias expansion and reference preservation.
        var options = new YamlSerializerOptions
        {
            DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins,
            ReferenceHandling = preserve ? YamlReferenceHandling.Preserve : YamlReferenceHandling.None,
        };
        var value = YamlSerializer.Deserialize<object>(yaml, options);
        if (json.Length == 0) return; // Complex YAML graphs have no JSON representation.
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        using var expected = JsonDocument.ParseValue(ref reader); // Compare the first document.
        AssertValue(expected.RootElement, value, id);
    }

    private static void AssertValue(JsonElement expected, object? actual, string path)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                Assert.IsInstanceOfType<IDictionary>(actual, path);
                var dictionary = (IDictionary)actual!;
                var count = 0;
                foreach (var pair in expected.EnumerateObject())
                {
                    count++;
                    Assert.IsTrue(dictionary.Contains(pair.Name), path + "." + pair.Name);
                    AssertValue(pair.Value, dictionary[pair.Name], path + "." + pair.Name);
                }
                Assert.AreEqual(count, dictionary.Count, path);
                break;
            case JsonValueKind.Array:
                Assert.IsInstanceOfType<IList>(actual, path);
                var sequence = (IList)actual!;
                Assert.AreEqual(expected.GetArrayLength(), sequence.Count, path);
                for (var i = 0; i < sequence.Count; i++) AssertValue(expected[i], sequence[i], path + "[" + i + "]");
                break;
            case JsonValueKind.Number:
                Assert.IsNotNull(actual, path);
                Assert.AreEqual(expected.GetDouble(), Convert.ToDouble(actual, CultureInfo.InvariantCulture), path);
                break;
            case JsonValueKind.String:
                Assert.AreEqual(expected.GetString(), actual, path);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.AreEqual(expected.GetBoolean(), actual, path);
                break;
            case JsonValueKind.Null:
                Assert.IsNull(actual, path);
                break;
        }
    }

    private static void Verify(string id, IParser parser, bool error, string expected)
    {
        var events = new List<string>();
        try
        {
            while (parser.MoveNext())
            {
                events.Add(Format(parser.Current!));
            }
        }
        catch (YamlException) when (error)
        {
            return;
        }

        Assert.IsFalse(error, $"{id}: invalid YAML was accepted.");
        Assert.AreEqual(expected.Replace("\r\n", "\n").TrimEnd('\n'), string.Join('\n', events), id);
    }

    private static string Properties(NodeEvent node) =>
        (node.Anchor is null ? "" : " &" + node.Anchor) + (node.Tag is null ? "" : " <" + node.Tag + ">");

    private static string Format(ParsingEvent evt) => evt switch
    {
        StreamStart => "+STR",
        StreamEnd => "-STR",
        DocumentStart start => "+DOC" + (start.IsImplicit ? "" : " ---"),
        DocumentEnd end => "-DOC" + (end.IsImplicit ? "" : " ..."),
        MappingStart start => "+MAP" + (start.Style == YamlStyle.Flow ? " {}" : "") + Properties(start),
        MappingEnd => "-MAP",
        SequenceStart start => "+SEQ" + (start.Style == YamlStyle.Flow ? " []" : "") + Properties(start),
        SequenceEnd => "-SEQ",
        AnchorAlias alias => "=ALI *" + alias.Value,
        Scalar scalar => "=VAL" + Properties(scalar) + " " + (scalar.Style switch
        {
            ScalarStyle.SingleQuoted => "'",
            ScalarStyle.DoubleQuoted => "\"",
            ScalarStyle.Literal => "|",
            ScalarStyle.Folded => ">",
            _ => ":",
        }) + scalar.Value.Replace("\\", "\\\\").Replace("\0", "\\0").Replace("\b", "\\b")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t"),
        _ => throw new InvalidOperationException(evt.ToString()),
    };
}
