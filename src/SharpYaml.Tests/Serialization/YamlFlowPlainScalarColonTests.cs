using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlFlowPlainScalarColonTests
{
    [TestMethod]
    public void FlowSequence_WithColonInPlainScalar_ShouldDeserializeAsString()
    {
        var yaml = "[x:x]\n";

        var list = YamlSerializer.Deserialize<List<string>>(yaml);

        Assert.IsNotNull(list);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("x:x", list[0]);
    }
}

