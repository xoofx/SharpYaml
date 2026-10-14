using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class ScalarStyleTests
{
    public static IEnumerable<object[]> BlockScalarCases()
    {
        string[] values = ["", "\n", "\n\n", "text", "text\n", "text\n\n", "a\nb", "a\n\nb",
            "\ntext", "  text\nnext", "a\n  b\nc", "a\n\n  b\n\nc", " \n", "\ttext\nnext",
            "a\rb", "a\r\nb\r\n", "a\u0085b", "a\u2028b", "a\u2029b", "a\u0000b", "a\uFFFEb", "a\uFFFFb", "💡\ntext"];
        foreach (var style in new[] { ScalarStyle.Literal, ScalarStyle.Folded })
        foreach (var value in values)
        {
            yield return [style, value];
        }
    }

    [TestMethod]
    [DynamicData(nameof(BlockScalarCases))]
    public void BlockStyles_PreserveContent(ScalarStyle style, string value)
    {
        var buffer = new StringBuilder();
        new YamlWriter(buffer).WriteScalar(value, style);
        Assert.AreEqual(value, YamlSerializer.Deserialize<string>(buffer.ToString()), buffer.ToString());
    }

    [TestMethod]
    [DataRow(">-\n  a\n  b\n", "a b")]
    [DataRow(">-\n  a\n\n  b\n", "a\nb")]
    [DataRow(">-\n  a\n\n\n  b\n", "a\n\nb")]
    [DataRow(">\n  a\n\n  b\n", "a\nb\n")]
    [DataRow(">+\n  a\n\n  b\n\n", "a\nb\n\n")]
    [DataRow(">2-\n  a\n\n  b\n", "a\nb")]
    [DataRow(">-\n  a\n    b\n", "a\n  b")]
    [DataRow(">-\n  a\n\n    b\n", "a\n\n  b")]
    [DataRow(">\n  a\n\n  b", "a\nb")]
    [DataRow("|\n  a\n  b", "a\nb")]
    public void BlockScalarParsing_UsesYamlFoldingAndChompingRules(string yaml, string expected)
    {
        // YAML 1.2.2 sections 6.5 and 8.1; expectations are independent of our emitter.
        Assert.AreEqual(expected, YamlSerializer.Deserialize<string>(yaml));
    }

    [TestMethod]
    [DataRow(ScalarStyle.Literal)]
    [DataRow(ScalarStyle.Folded)]
    public void BlockStyles_SeparateNodeProperties(ScalarStyle style)
    {
        foreach (var value in new[] { "text\n", "a\rb" })
        {
            var buffer = new StringBuilder();
            var writer = new YamlWriter(buffer);
            writer.WriteAnchor("id");
            writer.WriteTag("!!str");
            writer.WriteScalar(value, style);
            StringAssert.StartsWith(buffer.ToString(), "&id !!str ");
            Assert.AreEqual(value, YamlSerializer.Deserialize<string>(buffer.ToString()));
        }
    }

    [TestMethod]
    [DataRow(ScalarStyle.Literal)]
    [DataRow(ScalarStyle.Folded)]
    public void BlockStyles_KeepChompingSurvivesClosingNestedContainers(ScalarStyle style)
    {
        var buffer = new StringBuilder();
        var writer = new YamlWriter(buffer);
        writer.WriteStartSequence();
        writer.WriteStartSequence();
        writer.WriteStartMapping();
        writer.WritePropertyName("text");
        writer.WriteScalar("text\n\n", style);
        writer.WriteEndMapping();
        writer.WriteEndSequence();
        writer.WriteStartSequence();
        writer.WriteStartMapping();
        writer.WritePropertyName("text");
        writer.WriteString("next");
        writer.WriteEndMapping();
        writer.WriteEndSequence();
        writer.WriteEndSequence();

        var result = YamlSerializer.Deserialize<List<List<Dictionary<string, string>>>>(buffer.ToString())!;
        Assert.AreEqual("text\n\n", result[0][0]["text"], buffer.ToString());
        Assert.AreEqual("next", result[1][0]["text"]);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(9)]
    [DataRow(10)]
    public void BlockStyles_CompactMappingIndentationPreservesLeadingSpaces(int indentSize)
    {
        var buffer = new StringBuilder();
        var writer = new YamlWriter(buffer, new YamlSerializerOptions { IndentSize = indentSize });
        writer.WriteStartSequence();
        writer.WriteStartMapping();
        writer.WritePropertyName("text");
        writer.WriteScalar(" leading\nnext\n", ScalarStyle.Literal);
        writer.WritePropertyName("second");
        writer.WriteString("sibling");
        writer.WriteEndMapping();
        writer.WriteEndSequence();
        var result = YamlSerializer.Deserialize<List<Dictionary<string, string>>>(buffer.ToString())!;
        Assert.AreEqual(" leading\nnext\n", result[0]["text"], buffer.ToString());
        Assert.AreEqual("sibling", result[0]["second"]);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(9)]
    [DataRow(10)]
    public void BlockStyles_NestedCompactSequencesKeepEstablishedIndentation(int indentSize)
    {
        var buffer = new StringBuilder();
        var writer = new YamlWriter(buffer, new YamlSerializerOptions
        {
            IndentSize = indentSize,
            BlockSequenceSequenceStyle = YamlSequenceItemStyle.Compact,
        });
        writer.WriteStartSequence();
        for (var i = 0; i < 2; i++)
        {
            writer.WriteStartSequence();
            writer.WriteStartMapping();
            writer.WritePropertyName("text");
            writer.WriteScalar(" leading\nnext\n\n", ScalarStyle.Folded);
            writer.WritePropertyName("second");
            writer.WriteString("sibling");
            writer.WriteEndMapping();
            writer.WriteEndSequence();
        }
        writer.WriteEndSequence();

        var result = YamlSerializer.Deserialize<List<List<Dictionary<string, string>>>>(buffer.ToString())!;
        Assert.HasCount(2, result);
        foreach (var sequence in result)
        {
            Assert.AreEqual(" leading\nnext\n\n", sequence[0]["text"], buffer.ToString());
            Assert.AreEqual("sibling", sequence[0]["second"]);
        }
    }

    [TestMethod]
    public void ExplicitStyle_RejectsInvalidStyleBeforeWritingEvenForNull()
    {
        foreach (var value in new string?[] { null, "text" })
        {
            var buffer = new StringBuilder();
            var writer = new YamlWriter(buffer);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteScalar(value, (ScalarStyle)99));
            Assert.AreEqual(0, buffer.Length);
        }
    }

    [TestMethod]
    public void ExplicitStyles_NullAndMappingKeysRespectWriterContract()
    {
        foreach (var style in Enum.GetValues<ScalarStyle>())
        {
            var buffer = new StringBuilder();
            var writer = new YamlWriter(buffer);
            writer.WriteStartMapping();
            Assert.ThrowsExactly<InvalidOperationException>(() => writer.WriteScalar("key", style));
            writer.WritePropertyName("key");
            writer.WriteScalar(null, style);
            writer.WriteEndMapping();
            Assert.AreEqual("key: null", buffer.ToString());
        }
    }

    [TestMethod]
    [DataRow(ScalarStyle.Plain)]
    [DataRow(ScalarStyle.SingleQuoted)]
    [DataRow(ScalarStyle.DoubleQuoted)]
    public void FlowStyles_PreserveText(ScalarStyle style)
    {
        foreach (var value in new[] { "", "text", "it's quoted", "a\nb", "a\r\nb", "a\u0085b", "a\u2028b", "a\u2029b", "---", "...", "--- text", "... text", " leading ", "\u0001" })
        {
            var buffer = new StringBuilder();
            new YamlWriter(buffer).WriteScalar(value, style);
            Assert.AreEqual(value, YamlSerializer.Deserialize<string>(buffer.ToString()), buffer.ToString());
        }
    }

    [TestMethod]
    public void ExplicitStyles_EscapeNonPrintableUnicode()
    {
        foreach (var style in Enum.GetValues<ScalarStyle>())
        {
            var buffer = new StringBuilder();
            new YamlWriter(buffer).WriteScalar("a\uFFFE\uFFFFb", style);
            Assert.AreEqual("\"a\\uFFFE\\uFFFFb\"", buffer.ToString(), style.ToString());
            Assert.AreEqual("a\uFFFE\uFFFFb", YamlSerializer.Deserialize<string>(buffer.ToString()));
        }
    }

    [TestMethod]
    public void ExplicitStyles_RejectUnpairedSurrogatesAndPreserveValidPairs()
    {
        foreach (var style in Enum.GetValues<ScalarStyle>())
        {
            foreach (var value in new[] { "\uD800", "a\uDC00b", "\uD800\uD800", "\uDC00\uD800" })
            {
                var writer = new YamlWriter(new StringBuilder());
                Assert.ThrowsExactly<ArgumentException>(() => writer.WriteScalar(value, style));
            }

            var buffer = new StringBuilder();
            new YamlWriter(buffer).WriteScalar("💡", style);
            Assert.AreEqual("💡", YamlSerializer.Deserialize<string>(buffer.ToString()));
        }
    }

    [TestMethod]
    [DataRow(ScalarStyle.Literal)]
    [DataRow(ScalarStyle.Folded)]
    public void MultilinePreference_WorksWithReflectionAndGeneratedMetadata(ScalarStyle style)
    {
        var options = new YamlSerializerOptions
        {
            ScalarStylePreferences = new YamlScalarStylePreferences { MultilineStringStyle = style },
        };
        var value = new ScalarStyleModel { Text = "first\nsecond\n\n" };
        var context = new ScalarStyleContext(options);
        var reflected = YamlSerializer.Serialize(value, options);
        var generated = YamlSerializer.Serialize(value, context.ScalarStyleModel);
        Assert.AreEqual(reflected, generated);
        StringAssert.Contains(generated, style == ScalarStyle.Literal ? "|+" : ">+");
        Assert.AreEqual(value.Text, YamlSerializer.Deserialize(generated, context.ScalarStyleModel)!.Text);

        var buffer = new StringBuilder();
        new YamlWriter(buffer, options).WriteScalar(value.Text, ScalarStyle.DoubleQuoted);
        Assert.AreEqual("\"first\\nsecond\\n\\n\"", buffer.ToString());
    }
}

public sealed class ScalarStyleModel
{
    public string Text { get; set; } = "";
}

[YamlSerializable(typeof(ScalarStyleModel))]
internal partial class ScalarStyleContext : YamlSerializerContext
{
    public ScalarStyleContext(YamlSerializerOptions options) : base(options)
    {
    }
}
