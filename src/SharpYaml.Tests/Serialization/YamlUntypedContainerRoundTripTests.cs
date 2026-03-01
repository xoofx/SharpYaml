#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlUntypedContainerRoundTripTests
{
    [TestMethod]
    public void DictionaryStringObject_RoundTripsNestedUntypedContainers()
    {
        var value = new Dictionary<string, object?>
        {
            ["a"] = 1,
            ["b"] = new object?[]
            {
                "x",
                2,
                new Dictionary<string, object?>
                {
                    ["c"] = true,
                },
            },
            ["c"] = new List<object?>
            {
                null,
                "y",
            },
        };

        var yaml = SharpYaml.YamlSerializer.Serialize(value);
        var roundTripped = SharpYaml.YamlSerializer.Deserialize<Dictionary<string, object?>>(yaml);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(3, roundTripped.Count);
        Assert.AreEqual(1L, roundTripped["a"]);

        var b = (List<object?>)roundTripped["b"]!;
        Assert.AreEqual(3, b.Count);
        Assert.AreEqual("x", b[0]);
        Assert.AreEqual(2L, b[1]);

        var inner = (Dictionary<string, object?>)b[2]!;
        Assert.AreEqual(true, inner["c"]);

        var c = (List<object?>)roundTripped["c"]!;
        Assert.AreEqual(2, c.Count);
        Assert.IsNull(c[0]);
        Assert.AreEqual("y", c[1]);
    }

    [TestMethod]
    public void ObjectArray_RoundTrips()
    {
        var value = new object?[] { 1, "x", null, true };
        var yaml = SharpYaml.YamlSerializer.Serialize(value);
        var roundTripped = SharpYaml.YamlSerializer.Deserialize<object[]>(yaml);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(4, roundTripped.Length);
        Assert.AreEqual(1L, roundTripped[0]);
        Assert.AreEqual("x", roundTripped[1]);
        Assert.IsNull(roundTripped[2]);
        Assert.AreEqual(true, roundTripped[3]);
    }
}
