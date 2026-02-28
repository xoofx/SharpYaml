using System;
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

