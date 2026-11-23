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
    [DataRow("k: \"a#b\"  # keep\n", "\"a#b\"", "# keep")]
    [DataRow("color: '#fff' # white\n", "'#fff'", "# white")]
    [DataRow("url: http://x/#frag # c\n", "http://x/#frag", "# c")]
    public void ParseReadsTheCommentAfterAScalarThatHoldsAHash(string yaml, string scalarText, string commentText)
    {
        var tree = YamlSyntaxTree.Parse(yaml);

        var scalar = tree.Tokens.Single(token => token.Kind == YamlSyntaxKind.Scalar && token.Text == scalarText);
        var comment = tree.Tokens.Single(token => token.Kind == YamlSyntaxKind.CommentTrivia);
        var index = yaml.LastIndexOf(commentText, StringComparison.Ordinal);
        Assert.AreEqual(commentText, comment.Text);
        Assert.AreEqual(new Mark(index, 0, index), comment.Span.Start);
        Assert.AreEqual(new Mark(index + commentText.Length, 0, index + commentText.Length), comment.Span.End);
        Assert.IsTrue(comment.Span.Start.Index > scalar.Span.End.Index);
        AssertTokensDescribeText(tree, yaml);
    }

    [TestMethod]
    [DataRow("quoted: \"line one\n  line two\" # c\n")]
    [DataRow("plain: a b\n  c d # c\n")]
    [DataRow("key: |\n  text # not a comment\n\n# c\nnext: 1\n")]
    [DataRow("node: &a#b value # c\nalias: *a#b # c\n")]
    [DataRow("[a, \"b#c\" , 'd#e'] # c\n")]
    public void ParseReadsNoTriviaInsideTheTextOfOtherTokens(string yaml)
    {
        var tree = YamlSyntaxTree.Parse(yaml);

        AssertTokensDescribeText(tree, yaml);
        var comments = tree.Tokens.Where(token => token.Kind == YamlSyntaxKind.CommentTrivia).ToArray();
        Assert.IsTrue(comments.Length > 0);
        foreach (var comment in comments)
        {
            Assert.AreEqual("# c", comment.Text);
        }
    }

    [TestMethod]
    [DataRow("{} # c\n", YamlSyntaxKind.FlowMappingStart, "{")]
    [DataRow("{} # c\n", YamlSyntaxKind.FlowMappingEnd, "}")]
    [DataRow("[a] # c\n", YamlSyntaxKind.FlowSequenceStart, "[")]
    [DataRow("[a] # c\n", YamlSyntaxKind.FlowSequenceEnd, "]")]
    [DataRow("a\n... # c\n", YamlSyntaxKind.DocumentEnd, "...")]
    [DataRow("%YAML 1.2 # c\n---\na\n", YamlSyntaxKind.VersionDirective, "%YAML 1.2")]
    [DataRow("%TAG !e! tag:example.com,2026: # c\n---\n!e!a b\n", YamlSyntaxKind.TagDirective, "%TAG !e! tag:example.com,2026:")]
    [DataRow("%FOO  bar#baz\tqux # c\n---\na\n", YamlSyntaxKind.ReservedDirective, "%FOO  bar#baz\tqux")]
    public void ParseGivesAnIndicatorOrADirectiveTheTextOfItsSource(string yaml, YamlSyntaxKind kind, string text)
    {
        var tree = YamlSyntaxTree.Parse(yaml);

        var token = tree.Tokens.Single(token => token.Kind == kind);
        Assert.AreEqual(text, token.Text);
        Assert.AreEqual(yaml.IndexOf(text, StringComparison.Ordinal), token.Span.Start.Index);
        Assert.AreEqual("# c", tree.Tokens.Single(token => token.Kind == YamlSyntaxKind.CommentTrivia).Text);
        AssertTokensDescribeText(tree, yaml);
    }

    [TestMethod]
    public void ParseListsTokensInSourceOrder()
    {
        var tree = YamlSyntaxTree.Parse("a: 1\r\n# c\r\nb: 2 # d\r\n");

        CollectionAssert.AreEqual(
            new[]
            {
                YamlSyntaxKind.StreamStart, YamlSyntaxKind.BlockMappingStart, YamlSyntaxKind.Key, YamlSyntaxKind.Scalar,
                YamlSyntaxKind.Value, YamlSyntaxKind.WhitespaceTrivia, YamlSyntaxKind.Scalar, YamlSyntaxKind.NewLineTrivia,
                YamlSyntaxKind.CommentTrivia, YamlSyntaxKind.NewLineTrivia, YamlSyntaxKind.Key, YamlSyntaxKind.Scalar,
                YamlSyntaxKind.Value, YamlSyntaxKind.WhitespaceTrivia, YamlSyntaxKind.Scalar, YamlSyntaxKind.WhitespaceTrivia,
                YamlSyntaxKind.CommentTrivia, YamlSyntaxKind.NewLineTrivia, YamlSyntaxKind.BlockEnd, YamlSyntaxKind.StreamEnd,
            },
            tree.Tokens.Select(token => token.Kind).ToArray());
        AssertTokensDescribeText(tree, tree.Text);
    }

    public static IEnumerable<object[]> ValidSuiteCases()
    {
        foreach (var data in YamlTestSuiteTests.Cases())
        {
            if (!(bool)data[2])
            {
                yield return [data[0], data[1]];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(ValidSuiteCases))]
    public void ParseTokensOfOfficialCasesDescribeTheirText(string id, string yaml)
    {
        AssertTokensDescribeText(YamlSyntaxTree.Parse(yaml), id);
    }

    /// <summary>
    /// Asserts that the tokens are in source order, that each holds the text of its span, that no two tokens with
    /// text overlap (so no trivia lies inside a scalar, an anchor or a tag), that together they hold the whole text
    /// of a tree parsed with trivia and that each trivia token holds only what its kind says.
    /// </summary>
    private static void AssertTokensDescribeText(YamlSyntaxTree tree, string message)
    {
        var previousStart = 0;
        var textEnd = 0;
        foreach (var token in tree.Tokens)
        {
            var start = token.Span.Start.Index;
            var end = token.Span.End.Index;
            Assert.IsTrue(start >= previousStart, $"{message}: {token} is out of order.");
            Assert.AreEqual(tree.Text.Substring(start, end - start), token.Text, $"{message}: {token}");
            if (end > start)
            {
                Assert.IsTrue(start >= textEnd, $"{message}: {token} overlaps the text of a previous token.");
                AssertTextBetweenTokensIsByteOrderMarks(tree.Text, textEnd, start, message);
                textEnd = end;
            }

            switch (token.Kind)
            {
                case YamlSyntaxKind.CommentTrivia:
                    Assert.IsTrue(token.Text.StartsWith('#') && token.Text.IndexOfAny(['\r', '\n']) < 0, $"{message}: {token}");
                    break;
                case YamlSyntaxKind.WhitespaceTrivia:
                    Assert.IsTrue(token.Text.Length > 0 && token.Text.Trim(' ', '\t').Length == 0, $"{message}: {token}");
                    break;
                case YamlSyntaxKind.NewLineTrivia:
                    Assert.IsTrue(token.Text is "\n" or "\r\n" or "\r", $"{message}: {token}");
                    break;
            }

            previousStart = start;
        }

        AssertTextBetweenTokensIsByteOrderMarks(tree.Text, textEnd, tree.Text.Length, message);
    }

    /// <summary>
    /// Asserts that the text from <paramref name="start"/> to <paramref name="end"/>, which no token holds, is only byte
    /// order marks: the scanner skips them and the trivia leaves them out, while every other character is in a token.
    /// </summary>
    private static void AssertTextBetweenTokensIsByteOrderMarks(string text, int start, int end, string message)
    {
        const char ByteOrderMark = (char)0xFEFF;
        for (var index = start; index < end; index++)
        {
            Assert.AreEqual(ByteOrderMark, text[index], $"{message}: the character at {index} is in no token.");
        }
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
