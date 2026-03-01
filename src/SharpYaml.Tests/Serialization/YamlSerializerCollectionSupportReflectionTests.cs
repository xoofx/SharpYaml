#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlSerializerCollectionSupportReflectionTests
{
    [TestMethod]
    public void Deserialize_IReadOnlyList_ShouldReturnList()
    {
        var yaml = "- 1\n- 2\n";

        var result = YamlSerializer.Deserialize<IReadOnlyList<int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result[0]);
        Assert.AreEqual(2, result[1]);
    }

    [TestMethod]
    public void Deserialize_ISet_ShouldReturnHashSet()
    {
        var yaml = "- a\n- b\n- a\n";

        var result = YamlSerializer.Deserialize<ISet<string>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Contains("a"));
        Assert.IsTrue(result.Contains("b"));
    }

    [TestMethod]
    public void Deserialize_DictionaryWithIntKeys_ShouldParseKeys()
    {
        var yaml = "1: a\n2: b\n";

        var result = YamlSerializer.Deserialize<Dictionary<int, string>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual("a", result[1]);
        Assert.AreEqual("b", result[2]);
    }

    [TestMethod]
    public void Deserialize_IReadOnlyDictionaryWithEnumKeys_ShouldParseKeys()
    {
        var yaml = "Red: 1\nGreen: 2\n";

        var result = YamlSerializer.Deserialize<IReadOnlyDictionary<TestColor, int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result[TestColor.Red]);
        Assert.AreEqual(2, result[TestColor.Green]);
    }

    [TestMethod]
    public void Serialize_DictionaryWithGuidKeys_ShouldUseInvariantFormat()
    {
        var id = Guid.Parse("6d0c86e2-1e37-4c33-9c2f-5304a33f2c5e");
        var yaml = YamlSerializer.Serialize(new Dictionary<Guid, int> { [id] = 1 });

        StringAssert.Contains(yaml, "6d0c86e2-1e37-4c33-9c2f-5304a33f2c5e:");
        StringAssert.Contains(yaml, "1");
    }

    [TestMethod]
    public void Deserialize_ImmutableArray_ShouldRoundTripValues()
    {
        var yaml = "- 10\n- 20\n";

        var result = YamlSerializer.Deserialize<ImmutableArray<int>>(yaml);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(10, result[0]);
        Assert.AreEqual(20, result[1]);
    }

    [TestMethod]
    public void Deserialize_ImmutableList_ShouldRoundTripValues()
    {
        var yaml = "- a\n- b\n";

        var result = YamlSerializer.Deserialize<ImmutableList<string>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("b", result[1]);
    }

    [TestMethod]
    public void Deserialize_ImmutableHashSet_ShouldRoundTripValues()
    {
        var yaml = "- 1\n- 2\n- 1\n";

        var result = YamlSerializer.Deserialize<ImmutableHashSet<int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Contains(1));
        Assert.IsTrue(result.Contains(2));
    }

    [TestMethod]
    public void Deserialize_ImmutableCollections_WithAnchors_ShouldPreserveReferences()
    {
        var yaml =
            "Values: &a\n" +
            "  - 1\n" +
            "  - 2\n" +
            "Other: *a\n";

        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve };
        var result = YamlSerializer.Deserialize<ImmutableAnchorPayload>(yaml, options);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Values);
        Assert.IsNotNull(result.Other);
        Assert.IsTrue(ReferenceEquals(result.Values, result.Other));
    }

    [TestMethod]
    public void Deserialize_ImmutableArray_WithAnchors_ShouldResolveAlias()
    {
        var yaml =
            "Values: &a\n" +
            "  - 10\n" +
            "  - 20\n" +
            "Other: *a\n";

        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve };
        var result = YamlSerializer.Deserialize<ImmutableArrayAnchorPayload>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Values.Length);
        Assert.AreEqual(2, result.Other.Length);
        Assert.AreEqual(result.Values[0], result.Other[0]);
        Assert.AreEqual(result.Values[1], result.Other[1]);
    }

    internal enum TestColor
    {
        Red = 1,
        Green = 2,
    }

    private sealed class ImmutableAnchorPayload
    {
        public ImmutableList<int>? Values { get; set; }

        public ImmutableList<int>? Other { get; set; }
    }

    private sealed class ImmutableArrayAnchorPayload
    {
        public ImmutableArray<int> Values { get; set; }

        public ImmutableArray<int> Other { get; set; }
    }
}
