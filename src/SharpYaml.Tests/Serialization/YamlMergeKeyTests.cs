#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlMergeKeyTests
{
    [TestMethod]
    public void Deserialize_Object_ShouldApplyMergeKey()
    {
        var yaml =
            "<<: { A: 1, B: 2 }\n" +
            "B: 3\n";

        var result = YamlSerializer.Deserialize<MergePayload>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.A);
        Assert.AreEqual(3, result.B);
    }

    [TestMethod]
    public void Deserialize_Dictionary_ShouldApplyMergeKey()
    {
        var yaml =
            "<<: { a: 1, b: 2 }\n" +
            "b: 5\n";

        var result = YamlSerializer.Deserialize<Dictionary<string, int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result["a"]);
        Assert.AreEqual(5, result["b"]);
    }

    [TestMethod]
    public void Deserialize_Dictionary_ShouldApplyMergeSequenceInOrder()
    {
        var yaml =
            "<<:\n" +
            "  - { a: 1 }\n" +
            "  - { a: 2, b: 3 }\n" +
            "c: 4\n";

        var result = YamlSerializer.Deserialize<Dictionary<string, int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result["a"]);
        Assert.AreEqual(3, result["b"]);
        Assert.AreEqual(4, result["c"]);
    }

    [TestMethod]
    public void Deserialize_MergeKey_ShouldBeIgnoredForJsonSchema()
    {
        var yaml =
            "<<: { A: 1, B: 2 }\n" +
            "B: 3\n";

        var result = YamlSerializer.Deserialize<MergePayload>(yaml, new YamlSerializerOptions { Schema = YamlSchemaKind.Json });

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.A);
        Assert.AreEqual(3, result.B);
    }

    private sealed class MergePayload
    {
        public int A { get; set; }

        public int B { get; set; }
    }
}

