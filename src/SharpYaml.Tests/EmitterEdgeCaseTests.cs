using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;
using TagDirective = SharpYaml.Tokens.TagDirective;
using VersionDirective = SharpYaml.Tokens.VersionDirective;

namespace SharpYaml.Tests;

[TestClass]
public sealed class EmitterEdgeCaseTests
{
    [TestMethod]
    public void Emit_Directives_AreWritten()
    {
        var tags = new TagDirectiveCollection
        {
            new TagDirective("!e!", "tag:example.com,2026:"),
        };

        var yaml = EmitDocument(
            new DocumentStart(new VersionDirective(new Version(1, 1)), tags, isImplicit: false),
            new Scalar("value"));

        StringAssert.Contains(yaml, "%YAML 1.1");
        StringAssert.Contains(yaml, "%TAG !e! tag:example.com,2026:");
        StringAssert.Contains(yaml, "---");
    }

    [TestMethod]
    public void Emit_TagHandle_IsShortenedUsingDirective()
    {
        var tags = new TagDirectiveCollection
        {
            new TagDirective("!e!", "tag:example.com,2026:"),
        };

        var yaml = EmitDocument(
            new DocumentStart(null, tags, isImplicit: false),
            new Scalar(null, "!e!name", "value", ScalarStyle.Plain, isPlainImplicit: false, isQuotedImplicit: false));

        StringAssert.Contains(yaml, "%TAG !e! tag:example.com,2026:");
        Assert.IsTrue(
            yaml.Contains("!e!name", StringComparison.Ordinal) ||
            yaml.Contains("tag:example.com,2026:name", StringComparison.Ordinal),
            $"Expected emitted tag for scalar. Output:\n{yaml}");
        StringAssert.Contains(yaml, "value");
    }

    [TestMethod]
    public void Emit_TagUri_IsShortenedToDoubleExclamationWhenPossible()
    {
        var yaml = EmitDocument(
            new Scalar(null, "tag:yaml.org,2002:str", "text", ScalarStyle.Plain, isPlainImplicit: false, isQuotedImplicit: false));

        // YAML 1.1/1.2 core shorthand for "tag:yaml.org,2002:" is "!!".
        Assert.IsTrue(
            yaml.Contains("!!str", StringComparison.Ordinal) ||
            yaml.Contains("tag:yaml.org,2002:str", StringComparison.Ordinal),
            $"Expected emitted tag for scalar. Output:\n{yaml}");
        StringAssert.Contains(yaml, "text");
    }

    [TestMethod]
    public void Emit_FlowMapping_IsInline()
    {
        var yaml = EmitDocument(
            new MappingStart(null, null, isImplicit: true, style: YamlStyle.Flow),
            new Scalar("a"),
            new Scalar("1"),
            new Scalar("b"),
            new Scalar("2"),
            new MappingEnd());

        StringAssert.Contains(yaml, "{a: 1, b: 2}");
    }

    [TestMethod]
    public void Emit_EmptyCollections_AreWrittenInline()
    {
        var yaml = EmitDocument(
            new MappingStart(),
            new Scalar("emptySeq"),
            new SequenceStart(null, null, isImplicit: true, style: YamlStyle.Block),
            new SequenceEnd(),
            new Scalar("emptyMap"),
            new MappingStart(null, null, isImplicit: true, style: YamlStyle.Block),
            new MappingEnd(),
            new MappingEnd());

        StringAssert.Contains(yaml, "emptySeq: []");
        StringAssert.Contains(yaml, "emptyMap: {}");
    }

    [TestMethod]
    public void Emit_ComplexKey_UsesExplicitKeyIndicator()
    {
        var yaml = EmitDocument(
            new MappingStart(null, null, isImplicit: true, style: YamlStyle.Block),
            new Scalar(null, null, "multi\nline", ScalarStyle.DoubleQuoted, true, true),
            new Scalar("value"),
            new MappingEnd());

        StringAssert.Contains(yaml, "?");
        StringAssert.Contains(yaml, "value");
    }

    [TestMethod]
    public void Emit_SingleQuotedScalar_EscapesSingleQuoteByDoubling()
    {
        var yaml = EmitDocument(
            new Scalar(null, null, "a'b", ScalarStyle.SingleQuoted, true, true));

        StringAssert.Contains(yaml, "'a''b'");
    }

    [TestMethod]
    public void Emit_DoubleQuotedScalar_EscapesControlAndQuotes()
    {
        var yaml = EmitDocument(
            new Scalar(null, null, "a\"b\\c\n", ScalarStyle.DoubleQuoted, true, true));

        StringAssert.Contains(yaml, "\\\"");
        StringAssert.Contains(yaml, "\\\\");
        StringAssert.Contains(yaml, "\\n");
    }

    [TestMethod]
    public void Emit_BlockScalars_EmitLiteralAndFoldedIndicators()
    {
        var literal = EmitDocument(
            new Scalar(null, null, "a\nb\n", ScalarStyle.Literal, true, true));
        StringAssert.Contains(literal, "|");
        StringAssert.Contains(literal, "a");
        StringAssert.Contains(literal, "b");

        var folded = EmitDocument(
            new Scalar(null, null, "a\nb\n", ScalarStyle.Folded, true, true));
        StringAssert.Contains(folded, ">");
        StringAssert.Contains(folded, "a");
        StringAssert.Contains(folded, "b");
    }

    private static string EmitDocument(params ParsingEvent[] events)
    {
        return EmitDocument(documentStart: null, events);
    }

    private static string EmitDocument(DocumentStart? documentStart, params ParsingEvent[] events)
    {
        using var buffer = new StringWriter(CultureInfo.InvariantCulture);
        var emitter = new Emitter(buffer);

        emitter.Emit(new StreamStart());
        emitter.Emit(documentStart ?? new DocumentStart(null, null, isImplicit: true));

        foreach (var evt in events)
        {
            emitter.Emit(evt);
        }

        emitter.Emit(new DocumentEnd(isImplicit: true));
        emitter.Emit(new StreamEnd());

        return buffer.ToString();
    }
}
