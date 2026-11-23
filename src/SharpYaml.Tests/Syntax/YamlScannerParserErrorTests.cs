using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;
using SharpYaml.Syntax;

namespace SharpYaml.Tests.Syntax;

[TestClass]
public sealed class YamlScannerParserErrorTests
{
    [TestMethod]
    public void Parse_InvalidEscape_ThrowsSyntaxErrorException()
    {
        const string yaml = "key: \"\\q\"\n";

        var ex = Assert.Throws<SyntaxErrorException>(() => YamlSyntaxTree.Parse(yaml));
        Assert.IsTrue(ex.Start.Index >= 0);
        Assert.IsTrue(ex.End.Index >= ex.Start.Index);
        StringAssert.Contains(ex.Message, "escape");
    }

    [TestMethod]
    public void Parse_InvalidFlowSequence_ThrowsSemanticErrorException()
    {
        const string yaml = "a: [1, 2\n";

        var ex = Assert.Throws<SemanticErrorException>(() => YamlSyntaxTree.Parse(yaml));
        Assert.IsTrue(ex.Start.Index >= 0);
        Assert.IsTrue(ex.End.Index >= ex.Start.Index);
        StringAssert.Contains(ex.Message, "flow sequence");
    }

    [TestMethod]
    [DataRow("%YAML 1.2\n%YAML 1.2\n---\na\n", "%YAML 1.2", "duplicate %YAML")]
    [DataRow("%TAG !e! tag:a,2026:\n%TAG !e! tag:b,2026:\n---\na\n", "%TAG !e! tag:b,2026:", "duplicate %TAG")]
    [DataRow("%YAML 2.0 # c\n---\na\n", "%YAML 2.0", "incompatible")]
    public void Parse_InvalidDirective_MarksTheTextOfTheDirective(string yaml, string text, string message)
    {
        var ex = Assert.Throws<SemanticErrorException>(() => YamlSyntaxTree.Parse(yaml));

        var start = yaml.LastIndexOf(text, StringComparison.Ordinal);
        Assert.AreEqual(start, ex.Start.Index);
        Assert.AreEqual(start + text.Length, ex.End.Index);
        StringAssert.Contains(ex.Message, message);
    }

    [TestMethod]
    public void Parser_GivesFlowCollectionsAndDocumentEndsTheSpanOfTheirIndicators()
    {
        const string yaml = "{a: [b]}\n...\n";
        var parser = Parser.CreateParser(new StringReader(yaml));

        var spans = new List<string>();
        while (parser.MoveNext())
        {
            var current = parser.Current!;
            if (current is MappingStart or MappingEnd or SequenceStart or SequenceEnd or DocumentEnd)
            {
                spans.Add($"{current.GetType().Name} '{yaml.Substring(current.Start.Index, current.End.Index - current.Start.Index)}'");
            }
        }

        CollectionAssert.AreEqual(
            new[] { "MappingStart '{'", "SequenceStart '['", "SequenceEnd ']'", "MappingEnd '}'", "DocumentEnd '...'" },
            spans);
    }

    [TestMethod]
    [DataRow("[? ]\n", 3)]
    [DataRow("[? : x]\n", 3)]
    [DataRow("[? , y]\n", 3)]
    public void Parser_PlacesAnEmptyKeyOfAFlowSequenceEntryAtTheTokenAfterIt(string yaml, int index)
    {
        var parser = Parser.CreateParser(new StringReader(yaml));

        Scalar? key = null;
        while (parser.MoveNext())
        {
            if (parser.Current is Scalar scalar)
            {
                key = scalar;
                break;
            }
        }

        Assert.IsNotNull(key);
        Assert.AreEqual(string.Empty, key!.Value);
        Assert.AreEqual(index, key.Start.Index);
        Assert.AreEqual(index, key.End.Index);
    }

    [TestMethod]
    public void Parser_ReadsUtf32Escapes()
    {
        // Scanner uses CharHelper.ConvertFromUtf32 for the \UXXXXXXXX escape sequence.
        const string yaml = "k: \"\\U0001F600\"\n";
        var parser = Parser.CreateParser(new StringReader(yaml));

        Scalar? scalar = null;
        while (parser.MoveNext())
        {
            if (parser.Current is Scalar currentScalar && currentScalar.Value != "k")
            {
                scalar = currentScalar;
                break;
            }
        }

        Assert.IsNotNull(scalar);
        Assert.AreEqual("\U0001F600", scalar!.Value);
    }
}

