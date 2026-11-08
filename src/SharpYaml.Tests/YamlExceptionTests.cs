using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests;

[TestClass]
public sealed class YamlExceptionTests
{
    [TestMethod]
    public void Reason_IsTheMessageWithoutTheLocation()
    {
        var located = new YamlException("config.yaml", new Mark(7, 1, 3), new Mark(10, 1, 6), "'abc' is not a port");
        var unnamed = new YamlException(new Mark(7, 1, 3), new Mark(10, 1, 6), "'abc' is not a port", new FormatException());

        Assert.AreEqual("'abc' is not a port", located.Reason);
        Assert.AreEqual("config.yaml: (Lin: 1, Col: 3, Chr: 7) - (Lin: 1, Col: 6, Chr: 10): 'abc' is not a port", located.Message);
        Assert.AreEqual("'abc' is not a port", unnamed.Reason);
        StringAssert.StartsWith(unnamed.Message, "(Lin: 1, Col: 3, Chr: 7)");
    }

    [TestMethod]
    public void Reason_EqualsTheMessageWithoutALocation()
    {
        var plain = new YamlException("boom");
        var wrapped = new YamlException("boom", new InvalidOperationException());
        var empty = new YamlException();

        Assert.AreEqual(plain.Message, plain.Reason);
        Assert.AreEqual(wrapped.Message, wrapped.Reason);
        Assert.AreEqual(empty.Message, empty.Reason);
    }
}
