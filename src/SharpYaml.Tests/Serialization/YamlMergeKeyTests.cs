#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Syntax;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlMergeKeyTests
{
    [TestMethod]
    public void RoundTrip_NestedMerge_PreservesDataButNotMergeSyntax()
    {
        const string yaml = """
            x-common-environment:
              environment: &common-environment
                MODE: production
                LIMIT: 70
            services:
              app:
                environment:
                  <<: *common-environment
                  LIMIT: 90
                  EXTRA: enabled
            """;
        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve };
        var root = (Dictionary<string, object?>)YamlSerializer.Deserialize<object>(yaml, options)!;
        var common = (Dictionary<string, object?>)((Dictionary<string, object?>)root["x-common-environment"]!)["environment"]!;
        var services = (Dictionary<string, object?>)root["services"]!;
        var environment = (Dictionary<string, object?>)((Dictionary<string, object?>)services["app"]!)["environment"]!;

        Assert.AreNotSame(common, environment);
        Assert.AreEqual("production", environment["MODE"]);
        Assert.AreEqual(90L, environment["LIMIT"]);
        Assert.AreEqual(70L, common["LIMIT"]);
        Assert.AreEqual("enabled", environment["EXTRA"]);
        Assert.IsFalse(environment.ContainsKey("<<"));
        var output = YamlSerializer.Serialize(root, options);
        Assert.IsFalse(output.Contains("<<:"));
        StringAssert.Contains(output, "MODE: production");

        // Source preservation is a syntax-layer operation, not CLR reference handling.
        Assert.AreEqual(yaml, YamlSyntaxTree.Parse(yaml).ToFullString());
    }

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

