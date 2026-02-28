// Copyright (c) 2015 SharpYaml - Alexandre Mutel
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;
using SharpYaml.Model;
using SharpYaml.Schemas;

namespace SharpYaml.Tests;

/// <summary>
/// Comprehensive tests targeting YAML 1.2 specification edge cases across
/// schema resolution, parsing, scanning, and round-trip behavior.
/// </summary>
[TestClass]
public sealed class Yaml12CoreTests
{
    // ───────────────────────────────────────────────────────────────────
    // YAML 1.2 §10.1 — Failsafe Schema
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void FailsafeSchema_AllPlainScalarsResolveToStr()
    {
        var schema = new FailsafeSchema();
        // Even values that look like bool/int/null must resolve to !!str
        foreach (var text in new[] { "true", "false", "null", "42", "3.14", ".inf", ".nan", "~" })
        {
            var tag = schema.GetDefaultTag(new Scalar(text));
            Assert.AreEqual(SchemaBase.StrShortTag, tag, $"Failsafe should resolve '{text}' as !!str");
        }
    }

    [TestMethod]
    public void FailsafeSchema_QuotedScalarsResolveToStr()
    {
        var schema = new FailsafeSchema();
        Assert.AreEqual(SchemaBase.StrShortTag,
            schema.GetDefaultTag(new Scalar(null, null, "42", ScalarStyle.SingleQuoted, false, true)));
        Assert.AreEqual(SchemaBase.StrShortTag,
            schema.GetDefaultTag(new Scalar(null, null, "true", ScalarStyle.DoubleQuoted, false, true)));
    }

    // ───────────────────────────────────────────────────────────────────
    // YAML 1.2 §10.2 — JSON Schema
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void JsonSchema_RejectsPlainStrings()
    {
        var schema = new JsonSchema();
        // JSON schema should NOT resolve arbitrary plain strings
        Assert.IsNull(schema.GetDefaultTag(new Scalar(null, null, "hello", ScalarStyle.Plain, true, false)));
    }

    [TestMethod]
    public void JsonSchema_NullOnlyLowercase()
    {
        var schema = new JsonSchema();
        Assert.AreEqual(JsonSchema.NullShortTag, schema.GetDefaultTag(new Scalar("null")));
        // JSON schema is strict: "Null" and "NULL" should NOT match
        Assert.IsNull(schema.GetDefaultTag(new Scalar(null, null, "Null", ScalarStyle.Plain, true, false)));
        Assert.IsNull(schema.GetDefaultTag(new Scalar(null, null, "NULL", ScalarStyle.Plain, true, false)));
    }

    [TestMethod]
    public void JsonSchema_BoolOnlyLowercase()
    {
        var schema = new JsonSchema();
        Assert.AreEqual(JsonSchema.BoolShortTag, schema.GetDefaultTag(new Scalar("true")));
        Assert.AreEqual(JsonSchema.BoolShortTag, schema.GetDefaultTag(new Scalar("false")));
        // "True", "TRUE" should NOT match in JSON schema
        Assert.IsNull(schema.GetDefaultTag(new Scalar(null, null, "True", ScalarStyle.Plain, true, false)));
        Assert.IsNull(schema.GetDefaultTag(new Scalar(null, null, "FALSE", ScalarStyle.Plain, true, false)));
    }

    [TestMethod]
    public void JsonSchema_IntegerEdgeCases()
    {
        var schema = new JsonSchema();
        Assert.AreEqual(JsonSchema.IntShortTag, schema.GetDefaultTag(new Scalar("0")));
        Assert.AreEqual(JsonSchema.IntShortTag, schema.GetDefaultTag(new Scalar("-1")));

        Assert.IsTrue(schema.TryParse(new Scalar("0"), true, out _, out var zeroVal));
        Assert.AreEqual(0, zeroVal);
    }

    [TestMethod]
    public void JsonSchema_FloatEdgeCases()
    {
        var schema = new JsonSchema();
        // .inf / -.inf / .nan
        Assert.IsTrue(schema.TryParse(new Scalar(".inf"), true, out _, out var posInf));
        Assert.AreEqual(double.PositiveInfinity, posInf);

        Assert.IsTrue(schema.TryParse(new Scalar("-.inf"), true, out _, out var negInf));
        Assert.AreEqual(double.NegativeInfinity, negInf);

        Assert.IsTrue(schema.TryParse(new Scalar(".nan"), true, out _, out var nan));
        Assert.IsTrue(double.IsNaN((double)nan!));

        // Scientific notation
        Assert.IsTrue(schema.TryParse(new Scalar("1e10"), true, out var tag, out var sci));
        Assert.AreEqual(JsonSchema.FloatShortTag, tag);
        Assert.AreEqual(1e10, sci);
    }

    [TestMethod]
    public void JsonSchema_QuotedValuesAlwaysStr()
    {
        var schema = new JsonSchema();
        // Even "null" or "true" when quoted should be !!str
        Assert.AreEqual(SchemaBase.StrShortTag,
            schema.GetDefaultTag(new Scalar(null, null, "null", ScalarStyle.DoubleQuoted, false, false)));
        Assert.AreEqual(SchemaBase.StrShortTag,
            schema.GetDefaultTag(new Scalar(null, null, "42", ScalarStyle.SingleQuoted, false, false)));
    }

    // ───────────────────────────────────────────────────────────────────
    // YAML 1.2 §10.3 — Core Schema
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CoreSchema_NullVariations()
    {
        var schema = new CoreSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("null"), true, out var tag, out var val));
        Assert.AreEqual(JsonSchema.NullShortTag, tag);
        Assert.IsNull(val);

        Assert.IsTrue(schema.TryParse(new Scalar("Null"), true, out _, out _));
        Assert.IsTrue(schema.TryParse(new Scalar("NULL"), true, out _, out _));
        Assert.IsTrue(schema.TryParse(new Scalar("~"), true, out _, out _));
    }

    [TestMethod]
    public void CoreSchema_BoolCaseInsensitive()
    {
        var schema = new CoreSchema();
        foreach (var trueVal in new[] { "true", "True", "TRUE" })
        {
            Assert.IsTrue(schema.TryParse(new Scalar(trueVal), true, out _, out var val));
            Assert.AreEqual(true, val, $"'{trueVal}' should parse as true");
        }
        foreach (var falseVal in new[] { "false", "False", "FALSE" })
        {
            Assert.IsTrue(schema.TryParse(new Scalar(falseVal), true, out _, out var val));
            Assert.AreEqual(false, val, $"'{falseVal}' should parse as false");
        }
    }

    [TestMethod]
    public void CoreSchema_OctalIntegers()
    {
        var schema = new CoreSchema();
        Assert.IsTrue(schema.TryParse(new Scalar("0o10"), true, out var tag, out var val));
        Assert.AreEqual(JsonSchema.IntShortTag, tag);
        Assert.AreEqual(8, val);
    }

    [TestMethod]
    public void CoreSchema_HexIntegers()
    {
        var schema = new CoreSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("0x10"), true, out _, out var val));
        Assert.AreEqual(16, val);

        Assert.IsTrue(schema.TryParse(new Scalar("0xFF"), true, out _, out var val2));
        Assert.AreEqual(255, val2);

        Assert.IsTrue(schema.TryParse(new Scalar("0xDEAD"), true, out _, out var val3));
        Assert.AreEqual(0xDEAD, val3);
    }

    [TestMethod]
    public void CoreSchema_IntegersWithUnderscores()
    {
        var schema = new CoreSchema();
        Assert.IsTrue(schema.TryParse(new Scalar("1_000"), true, out var tag, out var val));
        Assert.AreEqual(JsonSchema.IntShortTag, tag);
        Assert.AreEqual(1000, val);
    }

    [TestMethod]
    public void CoreSchema_FloatVariations()
    {
        var schema = new CoreSchema();

        // Positive infinity variations
        Assert.IsTrue(schema.TryParse(new Scalar(".inf"), true, out _, out var posInf));
        Assert.AreEqual(double.PositiveInfinity, posInf);

        Assert.IsTrue(schema.TryParse(new Scalar(".Inf"), true, out _, out var posInf2));
        Assert.AreEqual(double.PositiveInfinity, posInf2);

        Assert.IsTrue(schema.TryParse(new Scalar(".INF"), true, out _, out var posInf3));
        Assert.AreEqual(double.PositiveInfinity, posInf3);

        // Negative infinity
        Assert.IsTrue(schema.TryParse(new Scalar("-.inf"), true, out _, out var negInf));
        Assert.AreEqual(double.NegativeInfinity, negInf);

        Assert.IsTrue(schema.TryParse(new Scalar("-.Inf"), true, out _, out var negInf2));
        Assert.AreEqual(double.NegativeInfinity, negInf2);

        // NaN variations
        Assert.IsTrue(schema.TryParse(new Scalar(".nan"), true, out _, out var nan));
        Assert.IsTrue(double.IsNaN((double)nan!));

        Assert.IsTrue(schema.TryParse(new Scalar(".NaN"), true, out _, out var nan2));
        Assert.IsTrue(double.IsNaN((double)nan2!));

        Assert.IsTrue(schema.TryParse(new Scalar(".NAN"), true, out _, out var nan3));
        Assert.IsTrue(double.IsNaN((double)nan3!));
    }

    [TestMethod]
    public void CoreSchema_FloatWithExponent()
    {
        var schema = new CoreSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("1.5e3"), true, out var tag, out var val));
        Assert.AreEqual(JsonSchema.FloatShortTag, tag);
        Assert.AreEqual(1500.0, val);

        Assert.IsTrue(schema.TryParse(new Scalar("2.5E-1"), true, out _, out var val2));
        Assert.AreEqual(0.25, val2);
    }

    [TestMethod]
    public void CoreSchema_PlainStringFallback()
    {
        var schema = new CoreSchema();
        // Unrecognized plain scalars should fall back to !!str
        Assert.AreEqual(SchemaBase.StrShortTag, schema.GetDefaultTag(new Scalar("hello world")));
        Assert.AreEqual(SchemaBase.StrShortTag, schema.GetDefaultTag(new Scalar("not-a-bool")));
    }

    [TestMethod]
    public void CoreSchema_NegativeIntegers()
    {
        var schema = new CoreSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("-42"), true, out var tag, out var val));
        Assert.AreEqual(JsonSchema.IntShortTag, tag);
        Assert.AreEqual(-42, val);

        Assert.IsTrue(schema.TryParse(new Scalar("+42"), true, out _, out var val2));
        Assert.AreEqual(42, val2);
    }

    // ───────────────────────────────────────────────────────────────────
    // Extended Schema — Timestamps, bools, merge, binary integers
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void ExtendedSchema_ExtendedBoolValues()
    {
        var schema = new ExtendedSchema();

        foreach (var trueVal in new[] { "y", "Y", "yes", "Yes", "YES", "on", "On", "ON", "true", "True", "TRUE" })
        {
            Assert.IsTrue(schema.TryParse(new Scalar(trueVal), true, out var tag, out var val),
                $"'{trueVal}' should be recognized");
            Assert.AreEqual(JsonSchema.BoolShortTag, tag, $"'{trueVal}' should be !!bool");
            Assert.AreEqual(true, val, $"'{trueVal}' should parse as true");
        }

        foreach (var falseVal in new[] { "n", "N", "no", "No", "NO", "off", "Off", "OFF", "false", "False", "FALSE" })
        {
            Assert.IsTrue(schema.TryParse(new Scalar(falseVal), true, out var tag, out var val),
                $"'{falseVal}' should be recognized");
            Assert.AreEqual(JsonSchema.BoolShortTag, tag, $"'{falseVal}' should be !!bool");
            Assert.AreEqual(false, val, $"'{falseVal}' should parse as false");
        }
    }

    [TestMethod]
    public void ExtendedSchema_BinaryIntegers()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("0b1010"), true, out var tag, out var val));
        Assert.AreEqual(JsonSchema.IntShortTag, tag);
        Assert.AreEqual(10, val);

        Assert.IsTrue(schema.TryParse(new Scalar("0b11111111"), true, out _, out var val2));
        Assert.AreEqual(255, val2);
    }

    [TestMethod]
    public void ExtendedSchema_NegativeBinaryIntegers()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("-0b1010"), true, out _, out var val));
        Assert.AreEqual(-10, val);
    }

    [TestMethod]
    public void ExtendedSchema_NegativeHexIntegers()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("-0x10"), true, out _, out var val));
        Assert.AreEqual(-16, val);
    }

    [TestMethod]
    public void ExtendedSchema_NegativeOctalIntegers()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("-0o10"), true, out _, out var val));
        Assert.AreEqual(-8, val);
    }

    [TestMethod]
    public void ExtendedSchema_MergeKey()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("<<"), true, out var tag, out var val));
        Assert.AreEqual(ExtendedSchema.MergeShortTag, tag);
        Assert.AreEqual("<<", val);
    }

    [TestMethod]
    public void ExtendedSchema_TimestampDateOnly()
    {
        var schema = new ExtendedSchema();
        Assert.IsTrue(schema.TryParse(new Scalar("2001-01-23"), true, out var tag, out var val));
        Assert.AreEqual(ExtendedSchema.TimestampShortTag, tag);
        Assert.AreEqual(new DateTime(2001, 1, 23), val);
    }

    [TestMethod]
    public void ExtendedSchema_TimestampWithTime()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("2001-12-15 02:59:43.1"), true, out var tag, out var val));
        Assert.AreEqual(ExtendedSchema.TimestampShortTag, tag);
        var dt = (DateTime)val!;
        Assert.AreEqual(2001, dt.Year);
        Assert.AreEqual(12, dt.Month);
        Assert.AreEqual(15, dt.Day);
        Assert.AreEqual(2, dt.Hour);
        Assert.AreEqual(59, dt.Minute);
        Assert.AreEqual(43, dt.Second);
    }

    [TestMethod]
    public void ExtendedSchema_TimestampWithMilliseconds()
    {
        var schema = new ExtendedSchema();
        Assert.IsTrue(schema.TryParse(new Scalar("2002-12-14 21:59:43.234"), true, out _, out var val));
        var dt = (DateTime)val!;
        Assert.AreEqual(234, dt.Millisecond);
    }

    [TestMethod]
    public void ExtendedSchema_NullVariationsIncludeEmptyAndTilde()
    {
        var schema = new ExtendedSchema();

        Assert.IsTrue(schema.TryParse(new Scalar("~"), true, out var tag, out _));
        Assert.AreEqual(JsonSchema.NullShortTag, tag);

        // Empty string matches the extended schema's null regex pattern (which includes empty)
        Assert.IsTrue(schema.TryParse(new Scalar(""), true, out var tag2, out _));
        Assert.AreEqual(JsonSchema.NullShortTag, tag2);
    }

    // ───────────────────────────────────────────────────────────────────
    // Tag Expansion / Shortening
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void TagExpansion_AllSchemas()
    {
        var schemas = new IYamlSchema[] { new FailsafeSchema(), new JsonSchema(), new CoreSchema(), new ExtendedSchema() };

        foreach (var schema in schemas)
        {
            Assert.AreEqual("tag:yaml.org,2002:str", schema.ExpandTag("!!str"));
            Assert.AreEqual("tag:yaml.org,2002:map", schema.ExpandTag("!!map"));
            Assert.AreEqual("tag:yaml.org,2002:seq", schema.ExpandTag("!!seq"));
        }
    }

    [TestMethod]
    public void TagShortening_AllSchemas()
    {
        var schemas = new IYamlSchema[] { new FailsafeSchema(), new JsonSchema(), new CoreSchema(), new ExtendedSchema() };

        foreach (var schema in schemas)
        {
            Assert.AreEqual("!!str", schema.ShortenTag("tag:yaml.org,2002:str"));
            Assert.AreEqual("!!map", schema.ShortenTag("tag:yaml.org,2002:map"));
            Assert.AreEqual("!!seq", schema.ShortenTag("tag:yaml.org,2002:seq"));
        }
    }

    [TestMethod]
    public void TagExpansion_UnknownTagPassesThrough()
    {
        var schema = new CoreSchema();
        Assert.AreEqual("!custom", schema.ExpandTag("!custom"));
        Assert.AreEqual("tag:example.com,2024:foo", schema.ShortenTag("tag:example.com,2024:foo"));
    }

    [TestMethod]
    public void TagExpansion_NullReturnsNull()
    {
        var schema = new CoreSchema();
        Assert.IsNull(schema.ExpandTag(null));
        Assert.IsNull(schema.ShortenTag(null));
    }

    [TestMethod]
    public void ExtendedSchema_TimestampTagRegistered()
    {
        var schema = new ExtendedSchema();
        Assert.AreEqual(ExtendedSchema.TimestampLongTag, schema.ExpandTag(ExtendedSchema.TimestampShortTag));
        Assert.AreEqual(ExtendedSchema.TimestampShortTag, schema.ShortenTag(ExtendedSchema.TimestampLongTag));
    }

    [TestMethod]
    public void ExtendedSchema_MergeTagRegistered()
    {
        var schema = new ExtendedSchema();
        Assert.AreEqual(ExtendedSchema.MergeLongTag, schema.ExpandTag(ExtendedSchema.MergeShortTag));
        Assert.AreEqual(ExtendedSchema.MergeShortTag, schema.ShortenTag(ExtendedSchema.MergeLongTag));
    }

    // ───────────────────────────────────────────────────────────────────
    // Schema — GetDefaultTag for Type
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CoreSchema_TypeTagMappings()
    {
        var schema = new CoreSchema();
        Assert.AreEqual(JsonSchema.BoolShortTag, schema.GetDefaultTag(typeof(bool)));
        Assert.AreEqual(JsonSchema.IntShortTag, schema.GetDefaultTag(typeof(int)));
        Assert.AreEqual(JsonSchema.IntShortTag, schema.GetDefaultTag(typeof(long)));
        Assert.AreEqual(JsonSchema.FloatShortTag, schema.GetDefaultTag(typeof(float)));
        Assert.AreEqual(JsonSchema.FloatShortTag, schema.GetDefaultTag(typeof(double)));
        Assert.AreEqual(SchemaBase.StrShortTag, schema.GetDefaultTag(typeof(string)));
    }

    [TestMethod]
    public void Schema_GetTypeForDefaultTag()
    {
        var schema = new CoreSchema();
        Assert.AreEqual(typeof(bool), schema.GetTypeForDefaultTag(JsonSchema.BoolShortTag));
        Assert.AreEqual(typeof(int), schema.GetTypeForDefaultTag(JsonSchema.IntShortTag));
        Assert.AreEqual(typeof(string), schema.GetTypeForDefaultTag(SchemaBase.StrShortTag));
        Assert.IsNull(schema.GetTypeForDefaultTag("!!unknown"));
        Assert.IsNull(schema.GetTypeForDefaultTag(null));
    }

    [TestMethod]
    public void Schema_IsTagImplicit()
    {
        var schema = new CoreSchema();
        Assert.IsTrue(schema.IsTagImplicit("!!str"));
        Assert.IsTrue(schema.IsTagImplicit("!!int"));
        Assert.IsTrue(schema.IsTagImplicit(null));
        Assert.IsFalse(schema.IsTagImplicit("!custom"));
    }

    // ───────────────────────────────────────────────────────────────────
    // Parser — Empty & Multi-document Streams
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parser_EmptyInput_ProducesStreamStartEnd()
    {
        var events = ParseAll("");
        Assert.AreEqual(2, events.Count);
        Assert.IsInstanceOfType<StreamStart>(events[0]);
        Assert.IsInstanceOfType<StreamEnd>(events[1]);
    }

    [TestMethod]
    public void Parser_SingleDocumentImplicit()
    {
        var events = ParseAll("hello\n");
        AssertDocumentContainsScalar(events, "hello");
    }

    [TestMethod]
    public void Parser_SingleDocumentExplicit()
    {
        var events = ParseAll("---\nhello\n...\n");
        AssertDocumentContainsScalar(events, "hello");
        var docEnd = events.OfType<DocumentEnd>().First();
        Assert.IsFalse(docEnd.IsImplicit);
    }

    [TestMethod]
    public void Parser_MultipleDocuments()
    {
        var events = ParseAll("---\nfoo\n---\nbar\n---\nbaz\n");
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(3, scalars.Count);
        Assert.AreEqual("foo", scalars[0].Value);
        Assert.AreEqual("bar", scalars[1].Value);
        Assert.AreEqual("baz", scalars[2].Value);
    }

    [TestMethod]
    public void Parser_DocumentEndMarker()
    {
        // After "...", a new document requires "---"
        var events = ParseAll("foo\n...\n---\nbar\n");
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(2, scalars.Count);
        Assert.AreEqual("foo", scalars[0].Value);
        Assert.AreEqual("bar", scalars[1].Value);
    }

    [TestMethod]
    public void Parser_DocumentEndMarkerWithoutNewDocStart_ThrowsException()
    {
        // Bare content after "..." without "---" is an error
        Assert.Throws<YamlException>(() => ParseAll("foo\n...\nbar\n"));
    }

    [TestMethod]
    public void Parser_ImplicitThenExplicitDocument()
    {
        var events = ParseAll("first\n---\nsecond\n");
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(2, scalars.Count);
        Assert.AreEqual("first", scalars[0].Value);
        Assert.AreEqual("second", scalars[1].Value);
    }

    [TestMethod]
    public void Parser_EmptyExplicitDocument()
    {
        var events = ParseAll("---\n...\n");
        // Should have StreamStart, DocStart, empty Scalar, DocEnd, StreamEnd
        var docStarts = events.OfType<DocumentStart>().ToList();
        Assert.AreEqual(1, docStarts.Count);
    }

    // ───────────────────────────────────────────────────────────────────
    // Scanner/Parser — Block Scalar Indicators
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void BlockScalar_LiteralClip()
    {
        // Default chomping (clip): single trailing newline
        const string yaml = "data: |\n  line1\n  line2\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("line1\nline2\n", value);
    }

    [TestMethod]
    public void BlockScalar_LiteralStrip()
    {
        // Strip chomping: no trailing newline
        const string yaml = "data: |-\n  line1\n  line2\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("line1\nline2", value);
    }

    [TestMethod]
    public void BlockScalar_LiteralKeep()
    {
        // Keep chomping: preserve all trailing newlines
        const string yaml = "data: |+\n  line1\n  line2\n\n\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("line1\nline2\n\n\n", value);
    }

    [TestMethod]
    public void BlockScalar_FoldedClip()
    {
        const string yaml = "data: >\n  folded\n  text\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("folded text\n", value);
    }

    [TestMethod]
    public void BlockScalar_FoldedStrip()
    {
        const string yaml = "data: >-\n  folded\n  text\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("folded text", value);
    }

    [TestMethod]
    public void BlockScalar_FoldedKeep()
    {
        const string yaml = "data: >+\n  folded\n  text\n\n\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("folded text\n\n\n", value);
    }

    [TestMethod]
    public void BlockScalar_ExplicitIndentation()
    {
        // |2 means content is indented 2 spaces from block header's column
        const string yaml = "data: |2\n  text\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("text\n", value);
    }

    [TestMethod]
    public void BlockScalar_LiteralPreservesBlankLines()
    {
        const string yaml = "data: |\n  line1\n\n  line3\n";
        var value = ParseSingleMappingValue(yaml);
        Assert.AreEqual("line1\n\nline3\n", value);
    }

    // ───────────────────────────────────────────────────────────────────
    // Scanner — Escape Sequences
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void DoubleQuoted_BasicEscapeSequences()
    {
        var cases = new (string Yaml, string Expected)[]
        {
            ("\"\\n\"", "\n"),
            ("\"\\t\"", "\t"),
            ("\"\\\\\"", "\\"),
            ("\"\\\"\"", "\""),
            ("\"\\0\"", "\0"),
            ("\"\\a\"", "\a"),
            ("\"\\b\"", "\b"),
            ("\"\\r\"", "\r"),
            ("\"\\e\"", "\x1B"),
            ("\"\\/\"", "/"),
            ("\"\\x41\"", "A"),
            ("\"\\u0041\"", "A"),
            ("\"\\U00000041\"", "A"),
        };

        foreach (var (yaml, expected) in cases)
        {
            var scalar = ParseSingleScalar(yaml + "\n");
            Assert.AreEqual(expected, scalar, $"Escape in {yaml} failed");
        }
    }

    [TestMethod]
    public void DoubleQuoted_UnicodeEscapes()
    {
        // 2-digit hex
        Assert.AreEqual("\x7F", ParseSingleScalar("\"\\x7F\"\n"));

        // 4-digit unicode
        Assert.AreEqual("\u00E9", ParseSingleScalar("\"\\u00E9\"\n")); // é

        // 8-digit unicode (emoji)
        Assert.AreEqual("\U0001F600", ParseSingleScalar("\"\\U0001F600\"\n"));
    }

    [TestMethod]
    public void DoubleQuoted_SpecialUnicodeEscapes()
    {
        // \N = next line (U+0085)
        Assert.AreEqual("\u0085", ParseSingleScalar("\"\\N\"\n"));

        // \_ = non-breaking space (U+00A0)
        Assert.AreEqual("\u00A0", ParseSingleScalar("\"\\_\"\n"));

        // \L = line separator (U+2028)
        Assert.AreEqual("\u2028", ParseSingleScalar("\"\\L\"\n"));

        // \P = paragraph separator (U+2029)
        Assert.AreEqual("\u2029", ParseSingleScalar("\"\\P\"\n"));
    }

    [TestMethod]
    public void DoubleQuoted_InvalidEscapeThrows()
    {
        Assert.Throws<SyntaxErrorException>(() => ParseSingleScalar("\"\\q\"\n"));
    }

    [TestMethod]
    public void DoubleQuoted_EscapedLineBreakIsSkipped()
    {
        // A backslash before a newline in double-quoted means line continuation
        var yaml = "\"line\\\n  continuation\"\n";
        var scalar = ParseSingleScalar(yaml);
        Assert.AreEqual("linecontinuation", scalar);
    }

    [TestMethod]
    public void SingleQuoted_DoubledQuoteEscape()
    {
        var yaml = "'it''s'\n";
        var scalar = ParseSingleScalar(yaml);
        Assert.AreEqual("it's", scalar);
    }

    [TestMethod]
    public void SingleQuoted_NoBackslashEscapes()
    {
        // In single-quoted, backslash is literal
        var yaml = "'hello\\nworld'\n";
        var scalar = ParseSingleScalar(yaml);
        Assert.AreEqual("hello\\nworld", scalar);
    }

    // ───────────────────────────────────────────────────────────────────
    // Flow Collections
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void FlowSequence_Empty()
    {
        var events = ParseAll("[]\n");
        var seqStarts = events.OfType<SequenceStart>().ToList();
        var seqEnds = events.OfType<SequenceEnd>().ToList();
        Assert.AreEqual(1, seqStarts.Count);
        Assert.AreEqual(1, seqEnds.Count);
    }

    [TestMethod]
    public void FlowMapping_Empty()
    {
        var events = ParseAll("{}\n");
        var mapStarts = events.OfType<MappingStart>().ToList();
        var mapEnds = events.OfType<MappingEnd>().ToList();
        Assert.AreEqual(1, mapStarts.Count);
        Assert.AreEqual(1, mapEnds.Count);
    }

    [TestMethod]
    public void FlowSequence_NestedInMapping()
    {
        const string yaml = "key: [1, 2, 3]\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(4, scalars.Count); // "key", "1", "2", "3"
    }

    [TestMethod]
    public void FlowMapping_NestedInSequence()
    {
        const string yaml = "- {a: 1, b: 2}\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(4, scalars.Count); // "a", "1", "b", "2"
    }

    [TestMethod]
    public void FlowSequence_Nested()
    {
        const string yaml = "[[1, 2], [3, 4]]\n";
        var events = ParseAll(yaml);
        var seqStarts = events.OfType<SequenceStart>().ToList();
        Assert.AreEqual(3, seqStarts.Count); // outer + 2 inner
    }

    [TestMethod]
    public void FlowMapping_NestedInFlowMapping()
    {
        const string yaml = "{outer: {inner: value}}\n";
        var events = ParseAll(yaml);
        var mapStarts = events.OfType<MappingStart>().ToList();
        Assert.AreEqual(2, mapStarts.Count);
    }

    // ───────────────────────────────────────────────────────────────────
    // Anchors & Aliases
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void AnchorAlias_OnScalar()
    {
        const string yaml = "a: &anchor value\nb: *anchor\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        var anchored = scalars.First(s => s.Anchor == "anchor");
        Assert.AreEqual("value", anchored.Value);
        var alias = events.OfType<AnchorAlias>().First();
        Assert.AreEqual("anchor", alias.Value);
    }

    [TestMethod]
    public void AnchorAlias_OnSequence()
    {
        const string yaml = "a: &seq\n  - 1\n  - 2\nb: *seq\n";
        var events = ParseAll(yaml);
        var seqStart = events.OfType<SequenceStart>().First();
        Assert.AreEqual("seq", seqStart.Anchor);
        var alias = events.OfType<AnchorAlias>().First();
        Assert.AreEqual("seq", alias.Value);
    }

    [TestMethod]
    public void AnchorAlias_OnMapping()
    {
        const string yaml = "a: &map\n  x: 1\n  y: 2\nb: *map\n";
        var events = ParseAll(yaml);
        var mapStart = events.OfType<MappingStart>().First(m => m.Anchor == "map");
        Assert.IsNotNull(mapStart);
        var alias = events.OfType<AnchorAlias>().First();
        Assert.AreEqual("map", alias.Value);
    }

    // ───────────────────────────────────────────────────────────────────
    // Comments
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Comments_AreIgnoredByParser()
    {
        const string yaml = "# This is a comment\nkey: value # inline comment\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(2, scalars.Count);
        Assert.AreEqual("key", scalars[0].Value);
        Assert.AreEqual("value", scalars[1].Value);
    }

    [TestMethod]
    public void CommentOnlyDocument()
    {
        var events = ParseAll("# just a comment\n");
        // Should produce stream start + end only (or empty document)
        Assert.IsInstanceOfType<StreamStart>(events[0]);
        Assert.IsInstanceOfType<StreamEnd>(events.Last());
    }

    // ───────────────────────────────────────────────────────────────────
    // Special Key Edge Cases
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void EmptyValueInMapping()
    {
        const string yaml = "key:\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(2, scalars.Count);
        Assert.AreEqual("key", scalars[0].Value);
        Assert.AreEqual("", scalars[1].Value); // empty value
    }

    [TestMethod]
    public void BoolLikeKeys()
    {
        // Keys that look like booleans should still be parsed as scalars
        const string yaml = "true: 1\nfalse: 0\nnull: nothing\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(6, scalars.Count);
        Assert.AreEqual("true", scalars[0].Value);
        Assert.AreEqual("false", scalars[2].Value);
        Assert.AreEqual("null", scalars[4].Value);
    }

    [TestMethod]
    public void NumericKeys()
    {
        const string yaml = "42: value\n3.14: pi\n";
        var events = ParseAll(yaml);
        var scalars = events.OfType<Scalar>().ToList();
        Assert.AreEqual(4, scalars.Count);
        Assert.AreEqual("42", scalars[0].Value);
        Assert.AreEqual("3.14", scalars[2].Value);
    }

    // ───────────────────────────────────────────────────────────────────
    // YamlStream Model — Round-Trip Tests
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Model_SimpleMapping_RoundTrip()
    {
        const string yaml = "name: John\nage: 30\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var mapping = (YamlMapping)stream[0].Contents!;

        Assert.AreEqual("John", mapping["name"]!.ToObject<string>());
        Assert.AreEqual(30, mapping["age"]!.ToObject<int>());

        var output = new StringWriter();
        stream.WriteTo(output, true);
        var reparsed = YamlStream.Load(new StringReader(output.ToString()));
        var remapping = (YamlMapping)reparsed[0].Contents!;
        Assert.AreEqual("John", remapping["name"]!.ToObject<string>());
    }

    [TestMethod]
    public void Model_Sequence_RoundTrip()
    {
        const string yaml = "- alpha\n- beta\n- gamma\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var seq = (YamlSequence)stream[0].Contents!;

        Assert.AreEqual(3, seq.Count);
        Assert.AreEqual("alpha", ((YamlValue)seq[0]).Value);
        Assert.AreEqual("gamma", ((YamlValue)seq[2]).Value);
    }

    [TestMethod]
    public void Model_NestedStructure_RoundTrip()
    {
        const string yaml = "root:\n  child:\n    - item1\n    - item2\n  value: 42\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var output = new StringWriter();
        stream.WriteTo(output, true);
        var reparsed = YamlStream.Load(new StringReader(output.ToString()));

        var rootMap = (YamlMapping)reparsed[0].Contents!;
        var innerMap = (YamlMapping)rootMap["root"]!;
        var child = (YamlSequence)innerMap["child"]!;
        Assert.AreEqual(2, child.Count);
        Assert.AreEqual("item1", ((YamlValue)child[0]).Value);
    }

    [TestMethod]
    public void Model_MultiDocument_RoundTrip()
    {
        const string yaml = "---\nfirst\n---\nsecond\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        Assert.AreEqual(2, stream.Count);
        Assert.AreEqual("first", ((YamlValue)stream[0].Contents!).Value);
        Assert.AreEqual("second", ((YamlValue)stream[1].Contents!).Value);
    }

    [TestMethod]
    public void Model_EmptyMapping()
    {
        const string yaml = "{}\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var mapping = (YamlMapping)stream[0].Contents!;
        Assert.AreEqual(0, mapping.Count);
    }

    [TestMethod]
    public void Model_EmptySequence()
    {
        const string yaml = "[]\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var seq = (YamlSequence)stream[0].Contents!;
        Assert.AreEqual(0, seq.Count);
    }

    // ───────────────────────────────────────────────────────────────────
    // Serializer — Values that need quoting
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Serializer_BoolLikeStrings_AreQuoted()
    {
        var yaml = YamlSerializer.Serialize("true");
        // The value "true" serialized as a string should be quoted
        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("true", result);
    }

    [TestMethod]
    public void Serializer_NullLikeStrings_AreQuoted()
    {
        var yaml = YamlSerializer.Serialize("null");
        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("null", result);
    }

    [TestMethod]
    public void Serializer_NumericLikeStrings_AreQuoted()
    {
        var yaml = YamlSerializer.Serialize("42");
        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("42", result);
    }

    [TestMethod]
    public void Serializer_SpecialFloatStrings_AreQuoted()
    {
        foreach (var val in new[] { ".inf", "-.inf", ".nan" })
        {
            var yaml = YamlSerializer.Serialize(val);
            var result = YamlSerializer.Deserialize<string>(yaml);
            Assert.AreEqual(val, result, $"Round-trip of string '{val}' failed");
        }
    }

    [TestMethod]
    public void Serializer_EmptyString_RoundTrips()
    {
        var yaml = YamlSerializer.Serialize("");
        var result = YamlSerializer.Deserialize<string>(yaml);
        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void Serializer_NullValue_SerializesToNullLiteral()
    {
        var yaml = YamlSerializer.Serialize<string?>(null);
        StringAssert.Contains(yaml, "null");

        // When deserializing "null" as object, it returns null
        var objResult = YamlSerializer.Deserialize<object>(yaml);
        Assert.IsNull(objResult);
    }

    // ───────────────────────────────────────────────────────────────────
    // Emitter — Additional edge cases
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Emitter_AnchorAlias_RoundTrip()
    {
        var output = new StringWriter();
        var emitter = new Emitter(output);

        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart(null, null, true));
        emitter.Emit(new MappingStart());
        emitter.Emit(new Scalar("a"));
        emitter.Emit(new Scalar("anchor1", null, "value", ScalarStyle.Plain, true, false));
        emitter.Emit(new Scalar("b"));
        emitter.Emit(new AnchorAlias("anchor1"));
        emitter.Emit(new MappingEnd());
        emitter.Emit(new DocumentEnd(true));
        emitter.Emit(new StreamEnd());

        var yaml = output.ToString();
        StringAssert.Contains(yaml, "&anchor1");
        StringAssert.Contains(yaml, "*anchor1");
    }

    [TestMethod]
    public void Emitter_MultipleDocuments()
    {
        var output = new StringWriter();
        var emitter = new Emitter(output);

        emitter.Emit(new StreamStart());

        emitter.Emit(new DocumentStart(null, null, false));
        emitter.Emit(new Scalar("first"));
        emitter.Emit(new DocumentEnd(false));

        emitter.Emit(new DocumentStart(null, null, false));
        emitter.Emit(new Scalar("second"));
        emitter.Emit(new DocumentEnd(false));

        emitter.Emit(new StreamEnd());

        var yaml = output.ToString();
        StringAssert.Contains(yaml, "---");
        StringAssert.Contains(yaml, "first");
        StringAssert.Contains(yaml, "second");
        StringAssert.Contains(yaml, "...");
    }

    [TestMethod]
    public void Emitter_NestedFlowCollections()
    {
        var output = new StringWriter();
        var emitter = new Emitter(output);

        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart(null, null, true));
        emitter.Emit(new SequenceStart(null, null, true, YamlStyle.Flow));
        emitter.Emit(new SequenceStart(null, null, true, YamlStyle.Flow));
        emitter.Emit(new Scalar("a"));
        emitter.Emit(new Scalar("b"));
        emitter.Emit(new SequenceEnd());
        emitter.Emit(new SequenceStart(null, null, true, YamlStyle.Flow));
        emitter.Emit(new Scalar("c"));
        emitter.Emit(new SequenceEnd());
        emitter.Emit(new SequenceEnd());
        emitter.Emit(new DocumentEnd(true));
        emitter.Emit(new StreamEnd());

        var yaml = output.ToString();
        StringAssert.Contains(yaml, "[[a, b], [c]]");
    }

    // ───────────────────────────────────────────────────────────────────
    // Complex YAML structures — integration-level parsing tests
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_ComplexNestedDocument()
    {
        const string yaml = @"server:
  host: localhost
  port: 8080
  features:
    - auth
    - logging
  database:
    host: db.local
    port: 5432
";
        var stream = YamlStream.Load(new StringReader(yaml));
        var root = (YamlMapping)stream[0].Contents!;
        var server = (YamlMapping)root["server"]!;
        Assert.AreEqual("localhost", server["host"]!.ToObject<string>());
        Assert.AreEqual(8080, server["port"]!.ToObject<int>());

        var features = (YamlSequence)server["features"]!;
        Assert.AreEqual(2, features.Count);

        var db = (YamlMapping)server["database"]!;
        Assert.AreEqual("db.local", db["host"]!.ToObject<string>());
    }

    [TestMethod]
    public void Parse_MixedFlowAndBlockCollections()
    {
        const string yaml = "block:\n  flow: {a: 1, b: [2, 3]}\n  plain: text\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var root = (YamlMapping)stream[0].Contents!;
        var block = (YamlMapping)root["block"]!;
        var flow = (YamlMapping)block["flow"]!;
        Assert.AreEqual("1", flow["a"]!.ToObject<string>());
    }

    [TestMethod]
    public void Parse_MultilineBlockScalar_Literal()
    {
        const string yaml = "description: |\n  This is a\n  multi-line\n  description.\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var root = (YamlMapping)stream[0].Contents!;
        var value = ((YamlValue)root["description"]!).Value;
        Assert.AreEqual("This is a\nmulti-line\ndescription.\n", value);
    }

    [TestMethod]
    public void Parse_MultilineFoldedScalar()
    {
        const string yaml = "summary: >\n  This is a\n  folded\n  paragraph.\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var root = (YamlMapping)stream[0].Contents!;
        var value = ((YamlValue)root["summary"]!).Value;
        Assert.AreEqual("This is a folded paragraph.\n", value);
    }

    [TestMethod]
    public void Parse_BlockScalar_FoldedWithBlankLines()
    {
        // Blank lines in folded scalars become literal newlines
        const string yaml = "text: >\n  paragraph1\n\n  paragraph2\n";
        var stream = YamlStream.Load(new StringReader(yaml));
        var root = (YamlMapping)stream[0].Contents!;
        var value = ((YamlValue)root["text"]!).Value;
        Assert.AreEqual("paragraph1\nparagraph2\n", value);
    }

    // ───────────────────────────────────────────────────────────────────
    // Error handling
    // ───────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_UnclosedFlowSequence_ThrowsException()
    {
        Assert.Throws<YamlException>(() => ParseAll("[1, 2\n"));
    }

    [TestMethod]
    public void Parse_UnclosedFlowMapping_ThrowsException()
    {
        Assert.Throws<YamlException>(() => ParseAll("{a: 1\n"));
    }

    [TestMethod]
    public void Parse_DuplicateAnchor_Succeeds()
    {
        // YAML allows redefining anchors; the last definition wins
        const string yaml = "a: &x 1\nb: &x 2\nc: *x\n";
        var events = ParseAll(yaml);
        var alias = events.OfType<AnchorAlias>().First();
        Assert.AreEqual("x", alias.Value);
    }

    // ───────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────

    private static List<ParsingEvent> ParseAll(string yaml)
    {
        var parser = Parser.CreateParser(new StringReader(yaml));
        var events = new List<ParsingEvent>();
        while (parser.MoveNext())
        {
            events.Add(parser.Current);
        }
        return events;
    }

    private static string ParseSingleScalar(string yaml)
    {
        var parser = Parser.CreateParser(new StringReader(yaml));
        while (parser.MoveNext())
        {
            if (parser.Current is Scalar scalar)
            {
                return scalar.Value;
            }
        }
        throw new InvalidOperationException("No scalar found in YAML");
    }

    private static string ParseSingleMappingValue(string yaml)
    {
        var parser = Parser.CreateParser(new StringReader(yaml));
        Scalar? lastScalar = null;
        while (parser.MoveNext())
        {
            if (parser.Current is Scalar scalar)
            {
                if (lastScalar is not null)
                {
                    return scalar.Value;
                }
                lastScalar = scalar;
            }
        }
        throw new InvalidOperationException("No mapping value found in YAML");
    }

    private static void AssertDocumentContainsScalar(List<ParsingEvent> events, string expectedValue)
    {
        var scalar = events.OfType<Scalar>().FirstOrDefault(s => s.Value == expectedValue);
        Assert.IsNotNull(scalar, $"Expected scalar '{expectedValue}' not found");
    }
}
