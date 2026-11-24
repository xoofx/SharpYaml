using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    [DataRow("[a: b]", "StreamStart 0-0,DocumentStart 0-0,SequenceStart 0-1,MappingStart 1-1,Scalar 1-2,Scalar 4-5,MappingEnd 5-5,SequenceEnd 5-6,DocumentEnd 6-6,StreamEnd 6-6")]
    [DataRow("[&a]", "StreamStart 0-0,DocumentStart 0-0,SequenceStart 0-1,Scalar 1-3,SequenceEnd 3-4,DocumentEnd 4-4,StreamEnd 4-4")]
    [DataRow("{a: !t }", "StreamStart 0-0,DocumentStart 0-0,MappingStart 0-1,Scalar 1-2,Scalar 4-6,MappingEnd 7-8,DocumentEnd 8-8,StreamEnd 8-8")]
    [DataRow("[? : x]", "StreamStart 0-0,DocumentStart 0-0,SequenceStart 0-1,MappingStart 1-2,Scalar 3-3,Scalar 5-6,MappingEnd 6-6,SequenceEnd 6-7,DocumentEnd 7-7,StreamEnd 7-7")]
    [DataRow("a", "StreamStart 0-0,DocumentStart 0-0,Scalar 0-1,DocumentEnd 1-1,StreamEnd 1-1")]
    [DataRow("a:\n- b\n? c\n: d\n", "StreamStart 0-0,DocumentStart 0-0,MappingStart 0-0,Scalar 0-1,SequenceStart 3-4,Scalar 5-6,SequenceEnd 7-7,Scalar 9-10,Scalar 13-14,MappingEnd 15-15,DocumentEnd 15-15,StreamEnd 15-15")]
    [DataRow("---\n...\n", "StreamStart 0-0,DocumentStart 0-3,Scalar 4-4,DocumentEnd 4-7,StreamEnd 8-8")]
    [DataRow("[: b]", "StreamStart 0-0,DocumentStart 0-0,SequenceStart 0-1,MappingStart 1-1,Scalar 1-1,Scalar 3-4,MappingEnd 4-4,SequenceEnd 4-5,DocumentEnd 5-5,StreamEnd 5-5")]
    public void Parser_GivesEachEventTheTextItWasReadFrom(string yaml, string expected)
    {
        // An event that only marks structure, such as the end of the mapping of "[a: b]", is empty: it does not take the
        // indicator of the node around it.
        var parser = Parser.CreateParser(new StringReader(yaml));
        var spans = new List<string>();
        while (parser.MoveNext())
        {
            var current = parser.Current!;
            spans.Add($"{current.GetType().Name} {current.Start.Index}-{current.End.Index}");
        }

        Assert.AreEqual(expected, string.Join(",", spans));
    }

    [TestMethod]
    public void Parse_ByteOrderMarkBeforeSpaces_TakesNoColumn()
    {
        var tree = YamlSyntaxTree.Parse("\uFEFF a\n");

        var whitespace = tree.Tokens.First(token => token.Kind == YamlSyntaxKind.WhitespaceTrivia);
        var scalar = tree.Tokens.First(token => token.Kind == YamlSyntaxKind.Scalar);
        Assert.AreEqual(1, whitespace.Span.Start.Index);
        Assert.AreEqual(0, whitespace.Span.Start.Column);
        Assert.AreEqual(scalar.Span.Start.Index, whitespace.Span.End.Index);
        Assert.AreEqual(scalar.Span.Start.Column, whitespace.Span.End.Column);
        Assert.AreEqual(1, scalar.Span.Start.Column);
    }

    [TestMethod]
    public void Parse_ByteOrderMarkBeforeASecondDocument_TakesNoColumn()
    {
        var tree = YamlSyntaxTree.Parse("a\n...\n\uFEFF--- b ");

        var whitespace = tree.Tokens.Last(token => token.Kind == YamlSyntaxKind.WhitespaceTrivia);
        Assert.AreEqual(13, whitespace.Span.End.Index);
        Assert.AreEqual(6, whitespace.Span.End.Column);
        Assert.AreEqual(6, tree.Root.FullSpan.End.Column);
    }

    [TestMethod]
    public void Parse_ByteOrderMark_TakesNoColumnInAnyMark()
    {
        var tree = YamlSyntaxTree.Parse("\uFEFFa ");

        var whitespace = tree.Tokens.First(token => token.Kind == YamlSyntaxKind.WhitespaceTrivia);
        Assert.AreEqual(3, whitespace.Span.End.Index);
        Assert.AreEqual(whitespace.Span.End.Column, tree.Root.FullSpan.End.Column);
        Assert.AreEqual(2, tree.Root.FullSpan.End.Column);
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

