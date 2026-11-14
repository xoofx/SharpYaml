using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;

namespace SharpYaml.Tests;

[TestClass]
public sealed class Yaml122SyntaxTests
{
    [TestMethod]
    [DataRow("%YAML1.1\n---\nvalue")]
    [DataRow("%***\n---\nvalue")]
    [DataRow("%YAML 1.12345\n---\nvalue")]
    public void ReservedNamesAndFutureMinorVersionsAreAccepted(string yaml)
    {
        Assert.AreEqual("value", ReadScalar(yaml));
    }

    [TestMethod]
    [DataRow("a\u0085b")]
    [DataRow("a\u2028b")]
    [DataRow("a\u2029b")]
    public void NonAsciiBreaksAreContent(string value)
    {
        foreach (var yaml in new[] { value, "'" + value + "'", "\"" + value + "\"", "|-\n " + value })
        {
            Assert.AreEqual(value, ReadScalar(yaml));
        }
    }

    [TestMethod]
    [DataRow("\"\\UFFFFFFFF\"")]
    [DataRow("\"\\U80000000\"")]
    [DataRow("\"\\uDC00\\uD800\"")]
    [DataRow("!<tag:%C0%AF> value")]
    [DataRow("!<tag:%ED%A0%80> value")]
    [DataRow("!<tag:%F4%90%80%80> value")]
    [DataRow("!<tag:%F0%80%80%AF> value")]
    [DataRow("%TAG !a_b! tag:example.org,2026:\n---\n!a_b!value x")]
    [DataRow("%TAG !h! tag:example.org,2026:\n---\n!h!foo!bar value")]
    [DataRow("[!foo{}]")]
    [DataRow("...\n... invalid")]
    public void InvalidUnicodeAndTagSyntaxThrowYamlException(string yaml)
    {
        AssertInvalid(yaml);
    }

    [TestMethod]
    public void RawControlsAndUnpairedSurrogatesAreRejected()
    {
        foreach (var c in new[] { '\0', '\u0001', '\u0007', '\u000b', '\u000c', '\u001f', '\u007f', '\u0080', '\u009f', '\ud800', '\udfff', '\ufffe', '\uffff' })
        {
            AssertInvalid("a" + c + "b");
            AssertInvalid("\"a" + c + "b\"");
            AssertInvalid("# a" + c + "b");
            AssertInvalid("|\n a" + c + "b");
        }
    }

    [TestMethod]
    public void SupplementaryUnicodeIsAcceptedInScalarsAndAnchors()
    {
        Assert.AreEqual("😁", ReadScalar("😁"));
        Assert.AreEqual("😁", ReadScalar("\"\\U0001F601\""));
        Assert.AreEqual("value", ReadScalar("&😁 value"));
    }

    [TestMethod]
    [DataRow("|+\n x\n  ", "x\n \n")]
    [DataRow("|+\n x\n", "x\n")]
    [DataRow("|+\n x", "x")]
    [DataRow("|+\n  ", "\n")]
    public void BlockScalarEofDoesNotInventExtraEmptyLines(string yaml, string expected)
    {
        Assert.AreEqual(expected, ReadScalar(yaml));
    }

    private static void AssertInvalid(string yaml)
    {
        Assert.Throws<YamlException>(() => ReadAll(Parser.CreateParser(new StringReader(yaml))));
        Assert.Throws<YamlException>(() => ReadAll(new Parser<LookAheadBuffer>(new LookAheadBuffer(new StringReader(yaml), Scanner<LookAheadBuffer>.MaxBufferLength))));
    }

    private static string ReadScalar(string yaml)
    {
        var direct = ReadAll(Parser.CreateParser(new StringReader(yaml)));
        var streaming = ReadAll(new Parser<LookAheadBuffer>(new LookAheadBuffer(new StringReader(yaml), Scanner<LookAheadBuffer>.MaxBufferLength)));
        Assert.AreEqual(direct, streaming);
        return direct;
    }

    private static string ReadAll(IParser parser)
    {
        var scalars = new System.Collections.Generic.List<string>();
        while (parser.MoveNext())
        {
            if (parser.Current is Scalar scalar) scalars.Add(scalar.Value);
        }
        return string.Join("|", scalars);
    }
}
