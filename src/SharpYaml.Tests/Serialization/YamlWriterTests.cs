using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlWriterTests
{
    [TestMethod]
    public void RootScalar_QuotesAndEscapesAsNeeded()
    {
        var cases = new (string Value, string ExpectedYaml)[]
        {
            ("plain", "plain"),
            ("", "''"),
            (" leading", "\" leading\""),
            ("trailing ", "\"trailing \""),
            ("a:b", "\"a:b\""),
            ("a#b", "\"a#b\""),
            ("a\nb", "\"a\\nb\""),
            ("\u0001", "\"\\u0001\""),
        };

        foreach (var @case in cases)
        {
            var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);
            writer.WriteScalar(@case.Value);

            Assert.AreEqual(@case.ExpectedYaml, buffer.ToString());
        }
    }

    [TestMethod]
    public void PropertyName_WithDash_IsQuoted()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("-");
        writer.WriteScalar("x");
        writer.WriteEndMapping();

        Assert.AreEqual("\"-\": x", buffer.ToString());
    }

    [TestMethod]
    public void RootScalars_ForNumbersAndSpecialFloats_AreEmittedPlain()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteStartSequence();
        writer.WriteScalar(42);
        writer.WriteScalar(1.5);
        writer.WriteScalar(double.PositiveInfinity);
        writer.WriteScalar(double.NegativeInfinity);
        writer.WriteScalar(double.NaN);
        writer.WriteEndSequence();

        Assert.AreEqual("- 42\n- 1.5\n- .inf\n- -.inf\n- .nan", buffer.ToString());
    }

    [TestMethod]
    public void Mapping_WithScalar_WritesOnSingleLine()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("a");
        writer.WriteScalar("1");
        writer.WriteEndMapping();

        Assert.AreEqual("a: 1", buffer.ToString());
    }

    [TestMethod]
    public void Mapping_WithNestedMapping_WritesIndentedBlock()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("parent");
        writer.WriteStartMapping();
        writer.WritePropertyName("child");
        writer.WriteScalar("x");
        writer.WriteEndMapping();
        writer.WriteEndMapping();

        Assert.AreEqual("parent:\n  child: x", buffer.ToString());
    }

    [TestMethod]
    public void Sequence_WithScalars_WritesDashLines()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartSequence();
        writer.WriteScalar("a");
        writer.WriteScalar("b");
        writer.WriteEndSequence();

        Assert.AreEqual("- a\n- b", buffer.ToString());
    }

    [TestMethod]
    public void Constructor_WithStringBuilder_WritesExpectedYaml()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var buffer = new System.Text.StringBuilder();
        var writer = new YamlWriter(buffer, options);

        writer.WriteStartMapping();
        writer.WritePropertyName("enabled");
        writer.WriteScalar(true);
        writer.WritePropertyName("port");
        writer.WriteScalar(5432);
        writer.WriteEndMapping();

        Assert.AreEqual("enabled: true\nport: 5432", buffer.ToString());
    }

    [TestMethod]
    public void CharacterScalar_WithSpecialCharacter_IsQuoted()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartSequence();
        writer.WriteScalar(':');
        writer.WriteEndSequence();

        Assert.AreEqual("- \":\"", buffer.ToString());
    }

    [TestMethod]
    public void EmptyContainers_AreWrittenInline()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("emptyMap");
        writer.WriteStartMapping();
        writer.WriteEndMapping();
        writer.WritePropertyName("emptySeq");
        writer.WriteStartSequence();
        writer.WriteEndSequence();
        writer.WriteEndMapping();

        Assert.AreEqual("emptyMap: {}\nemptySeq: []", buffer.ToString());
    }

    [TestMethod]
    public void SequenceItem_EmptyMapping_WritesInline()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartSequence();
        writer.WriteStartMapping();
        writer.WriteEndMapping();
        writer.WriteEndSequence();

        Assert.AreEqual("- {}", buffer.ToString());
    }

    [TestMethod]
    public void WriteScalar_WithAnyStyle_MatchesDefaultWriteScalar()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("a:b", ScalarStyle.Any);

        Assert.AreEqual("\"a:b\"", buffer.ToString());
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_WritesBlockScalarAndRoundTrips()
    {
        var writer = CreateWriter(new YamlSerializerOptions { IndentSize = 2 }, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("description");
        writer.WriteScalar("line1\nline2\n", ScalarStyle.Literal);
        writer.WriteEndMapping();

        // This is the only/last value in the mapping, so the clip-mandated trailing break must be written
        // explicitly here rather than relying on a following sibling to supply it.
        Assert.AreEqual("description: |\n  line1\n  line2\n", buffer.ToString());

        var result = YamlSerializer.Deserialize<Dictionary<string, string>>(buffer.ToString());
        Assert.AreEqual("line1\nline2\n", result!["description"]);
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_StripChomping_RoundTrips()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        // No trailing newline at all -> strip ("-") chomping.
        writer.WriteScalar("line1\nline2", ScalarStyle.Literal);

        var yaml = buffer.ToString();
        StringAssert.Contains(yaml, "|-");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("line1\nline2", result);
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_KeepChomping_RoundTrips()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        // Two trailing newlines -> keep ("+") chomping; every trailing break must survive round-tripping.
        writer.WriteScalar("line1\nline2\n\n", ScalarStyle.Literal);

        var yaml = buffer.ToString();
        StringAssert.Contains(yaml, "|+");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("line1\nline2\n\n", result);
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_LeadingSpaceOnFirstLine_RoundTrips()
    {
        var writer = CreateWriter(new YamlSerializerOptions { IndentSize = 2 }, out var buffer);

        // Ambiguous leading whitespace on the first content line requires an explicit indentation indicator.
        writer.WriteScalar("  indented first line\nsecond line\n", ScalarStyle.Literal);

        var result = YamlSerializer.Deserialize<string>(buffer.ToString());
        Assert.AreEqual("  indented first line\nsecond line\n", result);
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_LeadingSpaceOnFirstLine_NestedDepth_RoundTrips()
    {
        // The indentation indicator digit is a relative increment over the parent's indent, not this
        // scalar's absolute column - a regression test for a bug where the absolute column (4, at depth 2
        // with IndentSize 2) was written instead of the correct relative increment (2), desyncing the
        // reader's expected body indentation from what was actually written.
        var writer = CreateWriter(new YamlSerializerOptions { IndentSize = 2 }, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("outer");
        writer.WriteStartMapping();
        writer.WritePropertyName("description");
        writer.WriteScalar("  indented first line\nsecond line\n", ScalarStyle.Literal);
        writer.WriteEndMapping();
        writer.WriteEndMapping();

        var result = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(buffer.ToString());
        Assert.AreEqual("  indented first line\nsecond line\n", result!["outer"]["description"]);
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_LeadingSpaceOnFirstLine_IndentSizeTooLargeForIndicator_FallsBackToDoubleQuoted()
    {
        // The indentation indicator is a single digit (1-9); IndentSize 10 cannot be represented by one,
        // so this must safely fall back to double-quoted rather than emit an unparsable indicator.
        var writer = CreateWriter(new YamlSerializerOptions { IndentSize = 10 }, out var buffer);

        writer.WriteScalar("  indented first line\nsecond line\n", ScalarStyle.Literal);

        var yaml = buffer.ToString();
        Assert.IsTrue(yaml.StartsWith('"'), $"Expected fallback to double-quoted, got: {yaml}");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("  indented first line\nsecond line\n", result);
    }

    [TestMethod]
    public void WriteScalar_WithLiteralStyle_FollowedBySibling_DoesNotInsertBlankLine()
    {
        var writer = CreateWriter(new YamlSerializerOptions { IndentSize = 2 }, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("description");
        writer.WriteScalar("line1\nline2\n", ScalarStyle.Literal); // clip - self-writes its trailing break
        writer.WritePropertyName("next");
        writer.WriteScalar("value");
        writer.WriteEndMapping();

        // The block scalar's own chomped newline must not be followed by the framework's usual separator
        // newline too, or a stray blank line would appear before "next".
        Assert.AreEqual("description: |\n  line1\n  line2\nnext: value", buffer.ToString());
    }

    [TestMethod]
    public void WriteScalar_WithFoldedStyle_SafeContent_WritesFoldedIndicatorAndRoundTrips()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        // Paragraphs separated by a blank line are always safe to fold (folding never applies around a
        // blank line), so this should keep the requested '>' indicator.
        writer.WriteScalar("paragraph one\n\nparagraph two\n", ScalarStyle.Folded);

        var yaml = buffer.ToString();
        Assert.IsTrue(yaml.StartsWith('>'), $"Expected folded indicator, got: {yaml}");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("paragraph one\n\nparagraph two\n", result);
    }

    [TestMethod]
    public void WriteScalar_WithFoldedStyle_UnsafeContent_FallsBackToLiteralAndRoundTrips()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        // A single break between two non-blank lines would be folded into a space on read - must fall back
        // to literal style to preserve the value exactly.
        writer.WriteScalar("line1\nline2\n", ScalarStyle.Folded);

        var yaml = buffer.ToString();
        Assert.IsTrue(yaml.StartsWith('|'), $"Expected fallback to literal indicator, got: {yaml}");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("line1\nline2\n", result);
    }

    [TestMethod]
    public void WriteScalar_WithBlockStyle_UnprintableCharacter_FallsBackToDoubleQuoted()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("line1\u0001\nline2\n", ScalarStyle.Literal);

        var yaml = buffer.ToString();
        Assert.IsTrue(yaml.StartsWith('"'), $"Expected fallback to double-quoted, got: {yaml}");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("line1\u0001\nline2\n", result);
    }

    [TestMethod]
    public void WriteScalar_WithPlainStyle_SafeValue_WritesUnquoted()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("plain", ScalarStyle.Plain);

        Assert.AreEqual("plain", buffer.ToString());
    }

    [TestMethod]
    public void WriteScalar_WithPlainStyle_UnsafeValue_FallsBackToDoubleQuoted()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("a:b", ScalarStyle.Plain);

        var result = YamlSerializer.Deserialize<string>(buffer.ToString());
        Assert.AreEqual("\"a:b\"", buffer.ToString());
        Assert.AreEqual("a:b", result);
    }

    [TestMethod]
    public void WriteScalar_WithSingleQuotedStyle_EscapesEmbeddedQuote()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("it's a test", ScalarStyle.SingleQuoted);

        Assert.AreEqual("'it''s a test'", buffer.ToString());

        var result = YamlSerializer.Deserialize<string>(buffer.ToString());
        Assert.AreEqual("it's a test", result);
    }

    [TestMethod]
    public void WriteScalar_WithSingleQuotedStyle_ControlCharacter_FallsBackToDoubleQuoted()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("line1\nline2", ScalarStyle.SingleQuoted);

        var yaml = buffer.ToString();
        Assert.IsTrue(yaml.StartsWith('"'), $"Expected fallback to double-quoted, got: {yaml}");

        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("line1\nline2", result);
    }

    [TestMethod]
    public void WriteScalar_WithDoubleQuotedStyle_ForcesQuotingEvenWhenPlainSafe()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteScalar("plain", ScalarStyle.DoubleQuoted);

        Assert.AreEqual("\"plain\"", buffer.ToString());
    }

    [TestMethod]
    public void MultilineStringStyle_Literal_AppliesToDefaultStringWrites()
    {
        var options = new YamlSerializerOptions
        {
            IndentSize = 2,
            ScalarStylePreferences = new YamlScalarStylePreferences { MultilineStringStyle = ScalarStyle.Literal }
        };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("description");
        writer.WriteString("line1\nline2\n");
        writer.WriteEndMapping();

        Assert.AreEqual("description: |\n  line1\n  line2\n", buffer.ToString());
    }

    [TestMethod]
    public void MultilineStringStyle_Default_LeavesMultilineStringsDoubleQuoted()
    {
        var writer = CreateWriter(new YamlSerializerOptions(), out var buffer);

        writer.WriteString("line1\nline2\n");

        Assert.AreEqual("\"line1\\nline2\\n\"", buffer.ToString());
    }

    [TestMethod]
    public void MultilineStringStyle_Literal_DoesNotAffectSingleLineStrings()
    {
        var options = new YamlSerializerOptions
        {
            ScalarStylePreferences = new YamlScalarStylePreferences { MultilineStringStyle = ScalarStyle.Literal }
        };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteString("single line");

        Assert.AreEqual("single line", buffer.ToString());
    }

    [TestMethod]
    public void MultilineStringStyle_Literal_DoesNotAffectPropertyNames()
    {
        var options = new YamlSerializerOptions
        {
            ScalarStylePreferences = new YamlScalarStylePreferences { MultilineStringStyle = ScalarStyle.Literal }
        };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        // Property names go through WriteScalarCore, not WriteStringCore, so this must stay unaffected
        // regardless of MultilineStringStyle.
        writer.WritePropertyName("key\nwith break");
        writer.WriteScalar("value");
        writer.WriteEndMapping();

        StringAssert.Contains(buffer.ToString(), "\"key\\nwith break\"");
    }

    [TestMethod]
    public void ScalarStylePreferences_MultilineStringStyle_RejectsInvalidValue()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new YamlScalarStylePreferences { MultilineStringStyle = ScalarStyle.DoubleQuoted }
        );
    }

    private static YamlWriter CreateWriter(YamlSerializerOptions options, out StringWriter buffer)
    {
        buffer = new StringWriter();
        return new YamlWriter(buffer, options);
    }
}
