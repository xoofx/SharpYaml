using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Syntax;

namespace SharpYaml.Tests.Syntax;

[TestClass]
public class YamlSyntaxTreeTests
{
    public static IEnumerable<object[]> RoundTripCases()
    {
        yield return new object[]
        {
            "# comment\nroot:\n  key: value\n\nlist:\n  - a\n  - b\n",
        };
        yield return new object[]
        {
            "flow: { a: 1, b: [2, 3] }\nquoted: \"line\\nvalue\"\n",
        };
        yield return new object[]
        {
            "%TAG !e! tag:example.com,2026:\n---\nnode: &n1 !e!name value\nalias: *n1\n...\n",
        };
        yield return new object[]
        {
            "a: 1\r\nb:\r\n  - 2\r\n  - 3\r\n",
        };
    }

    [TestMethod]
    [DynamicData(nameof(RoundTripCases))]
    public void ParseAndRoundTripPreservesOriginalText(string yaml)
    {
        var tree = YamlSyntaxTree.Parse(yaml);

        Assert.AreEqual(yaml, tree.ToFullString());

        using var writer = new StringWriter();
        tree.WriteTo(writer);
        Assert.AreEqual(yaml, writer.ToString());
    }

    [TestMethod]
    public void ParseExposesExpectedSpans()
    {
        const string yaml = "key: value\n# comment\nlist:\n  - 1\n";
        var tree = YamlSyntaxTree.Parse(yaml);
        var scalarTokens = tree.Tokens.Where(token => token.Kind == YamlSyntaxKind.Scalar).ToArray();
        var commentToken = tree.Tokens.First(token => token.Kind == YamlSyntaxKind.CommentTrivia);

        Assert.IsTrue(scalarTokens.Length >= 3);
        Assert.AreEqual(0, scalarTokens[0].Span.Start.Index);
        Assert.AreEqual(0, scalarTokens[0].Span.Start.Line);
        Assert.AreEqual(0, scalarTokens[0].Span.Start.Column);

        Assert.AreEqual(5, scalarTokens[1].Span.Start.Index);
        Assert.AreEqual(0, scalarTokens[1].Span.Start.Line);
        Assert.AreEqual(5, scalarTokens[1].Span.Start.Column);

        Assert.AreEqual(11, commentToken.Span.Start.Index);
        Assert.AreEqual(1, commentToken.Span.Start.Line);
        Assert.AreEqual(0, commentToken.Span.Start.Column);
    }

    [TestMethod]
    public void ParseIncludesTriviaByDefault()
    {
        const string yaml = "a: 1\n# c\n";
        var tree = YamlSyntaxTree.Parse(yaml);

        Assert.IsTrue(tree.Tokens.Any(token => token.Kind == YamlSyntaxKind.CommentTrivia));
        Assert.IsTrue(tree.Tokens.Any(token => token.Kind == YamlSyntaxKind.NewLineTrivia));
    }

    [TestMethod]
    public void ParseCanExcludeTrivia()
    {
        const string yaml = "a: 1\n# c\n";
        var tree = YamlSyntaxTree.Parse(yaml, new YamlSyntaxOptions { IncludeTrivia = false });

        Assert.IsFalse(tree.Tokens.Any(token => token.Kind == YamlSyntaxKind.CommentTrivia));
        Assert.IsFalse(tree.Tokens.Any(token => token.Kind == YamlSyntaxKind.NewLineTrivia));
    }

    [TestMethod]
    public void ParseInvalidYamlThrowsWithMarks()
    {
        const string yaml = "a: [1, 2\n";
        YamlException ex;
        try
        {
            YamlSyntaxTree.Parse(yaml);
            Assert.Fail("Expected a YAML exception.");
            return;
        }
        catch (YamlException yamlException)
        {
            ex = yamlException;
        }

        Assert.IsTrue(ex.Start.Index >= 0);
        Assert.IsTrue(ex.End.Index >= ex.Start.Index);
    }
}
