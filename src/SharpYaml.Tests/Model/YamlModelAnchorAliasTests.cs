using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Model;

namespace SharpYaml.Tests;

[TestClass]
public sealed class YamlModelAnchorAliasTests
{
    [TestMethod]
    public void Load_ShouldResolveAnchorAlias_ByMaterializingACopy()
    {
        var yaml = """
field1: &data ABCD
field2: *data
""";

        var stream = YamlStream.Load(new StringReader(yaml));
        Assert.AreEqual(1, stream.Count);

        var mapping = (YamlMapping)stream[0].Contents!;

        var field1 = (YamlValue)mapping["field1"]!;
        var field2 = (YamlValue)mapping["field2"]!;

        Assert.AreEqual("data", field1.Anchor);
        Assert.AreEqual("ABCD", field1.Value);

        // The model API doesn't preserve aliases as a distinct node type: we materialize a copy.
        Assert.IsNull(field2.Anchor);
        Assert.AreEqual("ABCD", field2.Value);
    }
}
