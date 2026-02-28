#nullable enable

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlExceptionFormattingTests
{
    [TestMethod]
    public void YamlException_FormatsMessageWithoutSourceName()
    {
        var ex = new YamlException(new Mark(1, 2, 3), new Mark(4, 5, 6), "boom");

        StringAssert.Contains(ex.Message, "(Lin: 2, Col: 3, Chr: 1)");
        StringAssert.Contains(ex.Message, "(Lin: 5, Col: 6, Chr: 4)");
        StringAssert.Contains(ex.Message, "boom");
        Assert.IsNull(ex.SourceName);
        Assert.AreEqual(1, ex.Start.Index);
        Assert.AreEqual(4, ex.End.Index);
    }

    [TestMethod]
    public void YamlException_FormatsMessageWithSourceNameAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new YamlException("file.yaml", new Mark(0, 0, 0), new Mark(1, 0, 1), "outer", inner);

        StringAssert.Contains(ex.Message, "file.yaml:");
        StringAssert.Contains(ex.Message, "outer");
        Assert.AreSame(inner, ex.InnerException);
        Assert.AreEqual("file.yaml", ex.SourceName);
    }

    [TestMethod]
    public void SyntaxAndSemanticErrorExceptions_AreYamlExceptions()
    {
        var syntax = new SyntaxErrorException(new Mark(0, 0, 0), new Mark(1, 0, 1), "syntax");
        Assert.IsInstanceOfType<YamlException>(syntax);

        var semantic = new SemanticErrorException(new Mark(0, 0, 0), new Mark(1, 0, 1), "semantic");
        Assert.IsInstanceOfType<YamlException>(semantic);
    }
}

