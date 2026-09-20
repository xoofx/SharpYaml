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
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void WithTextChange_PreservesUnchangedSourceAndRebuildsSpans(string newline)
    {
        var yaml = "# heading" + newline + "image: 'old' # inline" + newline +
                   "defaults: &env { MODE: production }" + newline + "environment: { <<: *env, EXTRA: yes }" + newline;
        var original = YamlSyntaxTree.Parse(yaml);
        var value = original.Tokens.Single(token => token.Kind == YamlSyntaxKind.Scalar && token.Text == "'old'");

        var changed = original.WithTextChange(value.Span.Start.Index, value.Span.End.Index - value.Span.Start.Index, "'new: image'");

        Assert.AreEqual(yaml, original.ToFullString());
        Assert.AreEqual(yaml.Replace("'old'", "'new: image'"), changed.ToFullString());
        var updatedValue = changed.Tokens.Single(token => token.Kind == YamlSyntaxKind.Scalar && token.Text == "'new: image'");
        Assert.AreEqual(value.Span.Start.Index, updatedValue.Span.Start.Index);
        var inline = changed.Tokens.Single(token => token.Kind == YamlSyntaxKind.CommentTrivia && token.Text == "# inline");
        Assert.AreEqual(changed.Text.IndexOf("# inline", StringComparison.Ordinal), inline.Span.Start.Index);
        Assert.AreEqual(changed.Text.Length, changed.Root.FullSpan.End.Index);
        using var writer = new StringWriter();
        changed.WriteTo(writer);
        Assert.AreEqual(changed.Text, writer.ToString());
    }

    [TestMethod]
    public void WithTextChange_MultilineReplacementUpdatesFollowingMarks()
    {
        var tree = YamlSyntaxTree.Parse("# \U0001F600\nkey: old\nnext: value\n");
        var value = tree.Tokens.Single(token => token.Kind == YamlSyntaxKind.Scalar && token.Text == "old");
        var changed = tree.WithTextChange(value.Span.Start.Index, value.Span.End.Index - value.Span.Start.Index, "|-\n  first\n  second");
        var next = changed.Tokens.Single(token => token.Kind == YamlSyntaxKind.Scalar && token.Text == "next");
        Assert.AreEqual("# \U0001F600\nkey: |-\n  first\n  second\nnext: value\n", changed.Text);
        Assert.AreEqual(4, next.Span.Start.Line);
        Assert.AreEqual(0, next.Span.Start.Column);
        Assert.AreEqual(changed.Text.IndexOf("next", StringComparison.Ordinal), next.Span.Start.Index);
    }

    [TestMethod]
    public void WithTextChange_SupportsInsertDeleteAndSuccessiveEdits()
    {
        var tree = YamlSyntaxTree.Parse(string.Empty).WithTextChange(0, 0, "a: 1\n");
        tree = tree.WithTextChange(tree.Text.Length, 0, "b: 2\n");
        tree = tree.WithTextChange(0, 5, string.Empty);
        Assert.AreEqual("b: 2\n", tree.Text);
        tree = tree.WithTextChange(0, tree.Text.Length, string.Empty);
        Assert.AreEqual(string.Empty, tree.Text);
    }

    [TestMethod]
    public void WithTextChange_PreservesTriviaOptionWithoutRetainingMutableOptions()
    {
        var options = new YamlSyntaxOptions { IncludeTrivia = false };
        var tree = YamlSyntaxTree.Parse("a: 1\n# keep\n", options);
        options.IncludeTrivia = true;
        var changed = tree.WithTextChange(3, 1, "2");
        Assert.AreEqual("a: 2\n# keep\n", changed.Text);
        Assert.IsFalse(changed.Tokens.Any(token => token.Kind == YamlSyntaxKind.CommentTrivia));
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(6, 0)]
    [DataRow(0, -1)]
    [DataRow(4, 2)]
    [DataRow(1, int.MaxValue)]
    public void WithTextChange_RejectsOutOfRangeEdits(int start, int length)
    {
        var tree = YamlSyntaxTree.Parse("a: 1\n");
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.WithTextChange(start, length, string.Empty));
    }

    [TestMethod]
    public void WithTextChange_RejectsNullAndInvalidYamlWithoutChangingOriginal()
    {
        var tree = YamlSyntaxTree.Parse("a: 1\n");
        Assert.Throws<ArgumentNullException>(() => tree.WithTextChange(0, 0, null!));
        Assert.Throws<YamlException>(() => tree.WithTextChange(3, 1, "["));
        Assert.AreEqual("a: 1\n", tree.Text);
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
