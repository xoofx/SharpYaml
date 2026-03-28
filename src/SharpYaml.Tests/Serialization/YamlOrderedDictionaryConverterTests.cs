#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

internal sealed class OrderedDictionaryModel
{
    public OrderedDictionary<string, int> Scores { get; set; } = new();
}

internal sealed class OrderedDictionaryObjectModel
{
    public OrderedDictionary<string, object> Items { get; set; } = new();
}

internal sealed class OrderedDictionaryReferenceModel
{
    public OrderedDictionary<string, int>? Primary { get; set; }

    public OrderedDictionary<string, int>? Secondary { get; set; }
}

internal sealed class OrderedDictionaryGenericReferenceModel
{
    public OrderedDictionary<int, string>? Primary { get; set; }

    public OrderedDictionary<int, string>? Secondary { get; set; }
}

[YamlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[YamlSerializable(typeof(OrderedDictionary<string, int>))]
[YamlSerializable(typeof(OrderedDictionary<string, string>))]
[YamlSerializable(typeof(OrderedDictionary<int, string>))]
[YamlSerializable(typeof(OrderedDictionaryModel))]
internal partial class OrderedDictionaryTestContext : YamlSerializerContext
{
}

[TestClass]
public sealed class YamlOrderedDictionaryConverterTests
{
    [TestMethod]
    public void Reflection_Deserialize_StringKeyOrderedDictionary()
    {
        const string yaml = """
            alice: 100
            bob: 200
            charlie: 300
            """;

        var result = YamlSerializer.Deserialize<OrderedDictionary<string, int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(100, result["alice"]);
        Assert.AreEqual(200, result["bob"]);
        Assert.AreEqual(300, result["charlie"]);

        // Verify order is preserved
        var keys = new List<string>(result.Keys);
        CollectionAssert.AreEqual(new[] { "alice", "bob", "charlie" }, keys);
    }

    [TestMethod]
    public void Reflection_Deserialize_GenericKeyOrderedDictionary()
    {
        const string yaml = """
            1: one
            2: two
            3: three
            """;

        var result = YamlSerializer.Deserialize<OrderedDictionary<int, string>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("one", result[1]);
        Assert.AreEqual("two", result[2]);
        Assert.AreEqual("three", result[3]);
    }

    [TestMethod]
    public void Reflection_Serialize_StringKeyOrderedDictionary()
    {
        var dict = new OrderedDictionary<string, int>
        {
            { "alice", 100 },
            { "bob", 200 },
            { "charlie", 300 },
        };

        var yaml = YamlSerializer.Serialize(dict);

        Assert.IsNotNull(yaml);
        StringAssert.Contains(yaml, "alice: 100");
        StringAssert.Contains(yaml, "bob: 200");
        StringAssert.Contains(yaml, "charlie: 300");
    }

    [TestMethod]
    public void Reflection_Serialize_GenericKeyOrderedDictionary()
    {
        var dict = new OrderedDictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" },
        };

        var yaml = YamlSerializer.Serialize(dict);

        Assert.IsNotNull(yaml);
        StringAssert.Contains(yaml, "1: one");
        StringAssert.Contains(yaml, "2: two");
        StringAssert.Contains(yaml, "3: three");
    }

    [TestMethod]
    public void Reflection_RoundTrip_PreservesOrder()
    {
        var original = new OrderedDictionary<string, int>
        {
            { "zebra", 1 },
            { "apple", 2 },
            { "mango", 3 },
        };

        var yaml = YamlSerializer.Serialize(original);
        var result = YamlSerializer.Deserialize<OrderedDictionary<string, int>>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);

        var originalKeys = new List<string>(original.Keys);
        var resultKeys = new List<string>(result.Keys);
        CollectionAssert.AreEqual(originalKeys, resultKeys);
    }

    [TestMethod]
    public void Reflection_RoundTrip_StringKeyOrderedDictionary_ShouldPreserveSharedReferences()
    {
        var shared = new OrderedDictionary<string, int>
        {
            { "zebra", 1 },
            { "apple", 2 },
        };

        var payload = new OrderedDictionaryReferenceModel
        {
            Primary = shared,
            Secondary = shared,
        };

        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve };
        var yaml = YamlSerializer.Serialize(payload, options);

        var anchor = ExtractAnchor(yaml, "Primary: &");
        StringAssert.Contains(yaml, $"Secondary: *{anchor}");

        var result = YamlSerializer.Deserialize<OrderedDictionaryReferenceModel>(yaml, options);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Primary);
        Assert.IsNotNull(result.Secondary);
        Assert.IsTrue(ReferenceEquals(result.Primary, result.Secondary));

        var keys = new List<string>(result.Primary.Keys);
        CollectionAssert.AreEqual(new[] { "zebra", "apple" }, keys);
    }

    [TestMethod]
    public void Reflection_RoundTrip_GenericKeyOrderedDictionary_ShouldPreserveSharedReferences()
    {
        var shared = new OrderedDictionary<int, string>
        {
            { 3, "three" },
            { 1, "one" },
        };

        var payload = new OrderedDictionaryGenericReferenceModel
        {
            Primary = shared,
            Secondary = shared,
        };

        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve };
        var yaml = YamlSerializer.Serialize(payload, options);

        var anchor = ExtractAnchor(yaml, "Primary: &");
        StringAssert.Contains(yaml, $"Secondary: *{anchor}");

        var result = YamlSerializer.Deserialize<OrderedDictionaryGenericReferenceModel>(yaml, options);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Primary);
        Assert.IsNotNull(result.Secondary);
        Assert.IsTrue(ReferenceEquals(result.Primary, result.Secondary));

        var keys = new List<int>(result.Primary.Keys);
        CollectionAssert.AreEqual(new[] { 3, 1 }, keys);
    }

    [TestMethod]
    public void Reflection_Deserialize_NullValue()
    {
        const string yaml = "~";

        var result = YamlSerializer.Deserialize<OrderedDictionary<string, int>>(yaml);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Reflection_Serialize_NullValue()
    {
        OrderedDictionary<string, int>? dict = null;

        var yaml = YamlSerializer.Serialize(dict);

        Assert.IsNotNull(yaml);
    }

    [TestMethod]
    public void Reflection_Deserialize_AsObjectProperty()
    {
        const string yaml = """
            Scores:
              alice: 100
              bob: 200
            """;

        var result = YamlSerializer.Deserialize<OrderedDictionaryModel>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Scores.Count);
        Assert.AreEqual(100, result.Scores["alice"]);
        Assert.AreEqual(200, result.Scores["bob"]);
    }

    [TestMethod]
    public void SourceGen_Deserialize_StringKeyOrderedDictionary()
    {
        const string yaml = """
            alice: 100
            bob: 200
            charlie: 300
            """;

        var context = OrderedDictionaryTestContext.Default;
        var result = YamlSerializer.Deserialize<OrderedDictionary<string, int>>(yaml, context);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(100, result["alice"]);
        Assert.AreEqual(200, result["bob"]);
        Assert.AreEqual(300, result["charlie"]);

        // Verify order is preserved
        var keys = new List<string>(result.Keys);
        CollectionAssert.AreEqual(new[] { "alice", "bob", "charlie" }, keys);
    }

    [TestMethod]
    public void SourceGen_Serialize_StringKeyOrderedDictionary()
    {
        var dict = new OrderedDictionary<string, int>
        {
            { "alice", 100 },
            { "bob", 200 },
            { "charlie", 300 },
        };

        var context = OrderedDictionaryTestContext.Default;
        var yaml = YamlSerializer.Serialize(dict, context);

        Assert.IsNotNull(yaml);
        StringAssert.Contains(yaml, "alice: 100");
        StringAssert.Contains(yaml, "bob: 200");
        StringAssert.Contains(yaml, "charlie: 300");
    }

    [TestMethod]
    public void SourceGen_Deserialize_GenericKeyOrderedDictionary()
    {
        const string yaml = """
            1: one
            2: two
            3: three
            """;

        var context = OrderedDictionaryTestContext.Default;
        var result = YamlSerializer.Deserialize<OrderedDictionary<int, string>>(yaml, context);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("one", result[1]);
        Assert.AreEqual("two", result[2]);
        Assert.AreEqual("three", result[3]);
    }

    [TestMethod]
    public void SourceGen_RoundTrip_PreservesOrder()
    {
        var original = new OrderedDictionary<string, string>
        {
            { "zebra", "first" },
            { "apple", "second" },
            { "mango", "third" },
        };

        var context = OrderedDictionaryTestContext.Default;
        var yaml = YamlSerializer.Serialize(original, context);
        var result = YamlSerializer.Deserialize<OrderedDictionary<string, string>>(yaml, context);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);

        var originalKeys = new List<string>(original.Keys);
        var resultKeys = new List<string>(result.Keys);
        CollectionAssert.AreEqual(originalKeys, resultKeys);

        var originalValues = new List<string>(original.Values);
        var resultValues = new List<string>(result.Values);
        CollectionAssert.AreEqual(originalValues, resultValues);
    }

    [TestMethod]
    public void SourceGen_Deserialize_AsObjectProperty()
    {
        const string yaml = """
            scores:
              alice: 100
              bob: 200
            """;

        var context = OrderedDictionaryTestContext.Default;
        var result = YamlSerializer.Deserialize<OrderedDictionaryModel>(yaml, context);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Scores.Count);
        Assert.AreEqual(100, result.Scores["alice"]);
        Assert.AreEqual(200, result.Scores["bob"]);
    }

    [TestMethod]
    public void SourceGen_Deserialize_WithCamelCaseNaming()
    {
        const string yaml = """
            alice: 100
            bob: 200
            """;

        var context = OrderedDictionaryTestContext.Default;
        var result = YamlSerializer.Deserialize<OrderedDictionary<string, int>>(yaml, context);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
    }

    private static string ExtractAnchor(string yaml, string prefix)
    {
        var anchorStart = yaml.IndexOf(prefix, StringComparison.Ordinal);
        Assert.IsTrue(anchorStart >= 0, $"Expected '{prefix}' in YAML.");
        anchorStart += prefix.Length;

        var anchorEnd = yaml.IndexOf('\n', anchorStart);
        Assert.IsTrue(anchorEnd > anchorStart, $"Expected an anchor after '{prefix}'.");

        var anchor = yaml.Substring(anchorStart, anchorEnd - anchorStart).Trim();
        Assert.AreNotEqual(string.Empty, anchor);
        return anchor;
    }
}
