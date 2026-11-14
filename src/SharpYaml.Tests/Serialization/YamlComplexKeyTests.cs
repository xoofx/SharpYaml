using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlComplexKeyTests
{
    [TestMethod]
    public void UntypedMappingPromotesOnlyWhenAComplexKeyOccurs()
    {
        Assert.IsInstanceOfType<Dictionary<string, object?>>(YamlSerializer.Deserialize<object>("a: b"));
        var result = YamlSerializer.Deserialize<object>("a: b\n? [x, y]\n: z");
        Assert.IsInstanceOfType<Dictionary<object, object?>>(result);
        var map = (Dictionary<object, object?>)result!;
        Assert.AreEqual("b", map["a"]);
        var key = map.Keys.OfType<List<object?>>().Single();
        CollectionAssert.AreEqual(new object[] { "x", "y" }, key);
        Assert.AreEqual("z", map[key]);
    }

    [TestMethod]
    public void ComplexKeysRoundTripWithoutBecomingTypeNames()
    {
        var source = new Dictionary<object, object?>
        {
            ["a"] = "b",
            [new List<object?> { "x", "y" }] = new Dictionary<string, object?> { ["z"] = "value" },
        };
        var yaml = YamlSerializer.Serialize(source);
        StringAssert.Contains(yaml, "?");
        var result = (Dictionary<object, object?>)YamlSerializer.Deserialize<object>(yaml)!;
        Assert.AreEqual(2, result.Count);
        var key = result.Keys.OfType<List<object?>>().Single();
        CollectionAssert.AreEqual(new object[] { "x", "y" }, key);
        Assert.AreEqual("value", ((Dictionary<string, object?>)result[key]!)["z"]);
    }

    [TestMethod]
    public void TypedSequenceKeyUsesItsConverter()
    {
        var result = YamlSerializer.Deserialize<Dictionary<List<string>, string>>("? [a, b]\n: value")!;
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Keys.Single());
        Assert.AreEqual("value", result.Values.Single());
    }

    [TestMethod]
    public void GeneratedSequenceKeysWorkAtRootAndInMembers()
    {
        var options = new YamlSerializerOptions { TypeInfoResolver = ComplexKeyContext.Default };
        const string yaml = "? [a, b]\n: value";
        var root = YamlSerializer.Deserialize<Dictionary<List<string>, string>>(yaml, options)!;
        Assert.AreEqual("value", root.Values.Single());
        var rootYaml = YamlSerializer.Serialize(root, options);
        CollectionAssert.AreEqual(new[] { "a", "b" }, YamlSerializer.Deserialize<Dictionary<List<string>, string>>(rootYaml, options)!.Keys.Single());

        var model = YamlSerializer.Deserialize<ComplexKeyModel>("Items:\n  ? [a, b]\n  : value", options)!;
        Assert.AreEqual("value", model.Items.Values.Single());
        var modelYaml = YamlSerializer.Serialize(model, options);
        CollectionAssert.AreEqual(new[] { "a", "b" }, YamlSerializer.Deserialize<ComplexKeyModel>(modelYaml, options)!.Items.Keys.Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ObjectKeysUseMappingConverters(bool generated)
    {
        var options = generated ? new YamlSerializerOptions { TypeInfoResolver = ComplexKeyContext.Default } : new YamlSerializerOptions();
        var value = new Dictionary<ComplexObjectKey, string> { [new() { Name = "key" }] = "value" };
        var yaml = YamlSerializer.Serialize(value, options);
        var result = YamlSerializer.Deserialize<Dictionary<ComplexObjectKey, string>>(yaml, options)!;
        Assert.AreEqual("key", result.Keys.Single().Name);
        Assert.AreEqual("value", result.Values.Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AliasedComplexKeysRemainDistinct(bool preserve)
    {
        var options = new YamlSerializerOptions { ReferenceHandling = preserve ? YamlReferenceHandling.Preserve : YamlReferenceHandling.None };
        var map = (Dictionary<object, object?>)YamlSerializer.Deserialize<object>("{ &a [a, &b b]: *b, *a : [c, *b, d]}", options)!;
        Assert.AreEqual(2, map.Count);
        var keys = map.Keys.OfType<List<object?>>().ToArray();
        Assert.AreEqual(2, keys.Length);
        Assert.AreNotSame(keys[0], keys[1]);
        CollectionAssert.AreEqual(keys[0], keys[1]);
    }

    [TestMethod]
    public void PromotionUpdatesUnresolvedAnchorsAndRejectsEscapedSelfReferences()
    {
        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve };
        var graph = (List<object?>)YamlSerializer.Deserialize<object>("[&m {a: b, ? [k]: v}, *m]", options)!;
        Assert.IsInstanceOfType<Dictionary<object, object?>>(graph[0]);
        Assert.AreSame(graph[0], graph[1]);
        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<object>("&m {self: *m, ? [k]: v}", options));
        StringAssert.Contains(exception.Message, "already referenced");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UntypedMergesExpandPreservedTypedAnchors(bool generated)
    {
        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve,
            TypeInfoResolver = generated ? ComplexKeyContext.Default : null };
        var result = YamlSerializer.Deserialize<ComplexMergeRoot>("Base: &b {Name: key}\nMerged: {<<: *b}", options)!;
        Assert.AreEqual("key", ((Dictionary<string, object?>)result.Merged!)["Name"]);
    }

    [TestMethod]
    public void ExplicitlyGeneratedObjectMetadataReadsAndWritesUntypedGraphs()
    {
        var context = UntypedRootContext.Default;
        var graph = YamlSerializer.Deserialize("a: &a '123'\nb: *a\n? [key]\n: value", context.Object);
        Assert.IsInstanceOfType<Dictionary<object, object?>>(graph);
        var yaml = YamlSerializer.Serialize(graph, context.Object);
        var roundTrip = (Dictionary<object, object?>)YamlSerializer.Deserialize(yaml, context.Object)!;
        Assert.AreEqual("123", roundTrip["a"]);
        Assert.AreEqual("123", roundTrip["b"]);
        Assert.AreEqual(3, roundTrip.Count);
    }

    [TestMethod]
    public void ScalarDuplicatesStillHonorPolicyAfterPromotion()
    {
        const string yaml = "a: first\n? [x]\n: complex\na: last";
        Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<object>(yaml));
        foreach (var policy in new[] { YamlDuplicateKeyHandling.FirstWins, YamlDuplicateKeyHandling.LastWins })
        {
            var result = (Dictionary<object, object?>)YamlSerializer.Deserialize<object>(yaml, new YamlSerializerOptions { DuplicateKeyHandling = policy })!;
            Assert.AreEqual(policy == YamlDuplicateKeyHandling.FirstWins ? "first" : "last", result["a"]);
        }
    }

    [TestMethod]
    public void StringKeysRemainCaseInsensitiveAfterPromotion()
    {
        var options = new YamlSerializerOptions { PropertyNameCaseInsensitive = true, DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
        var result = (Dictionary<object, object?>)YamlSerializer.Deserialize<object>("A: first\n? [x]\n: complex\na: last", options)!;
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("last", result["A"]);
    }

    [TestMethod]
    [DataRow(YamlTokenType.StartSequence, YamlTokenType.EndSequence)]
    [DataRow(YamlTokenType.StartMapping, YamlTokenType.EndMapping)]
    public void ReportingKeepsMappingStateAfterAComplexKey(YamlTokenType start, YamlTokenType end)
    {
        var session = YamlReportingSession.Create(new YamlSerializerOptions { UnmappedMemberCallback = _ => { } })!;
        var tracker = new YamlPathTracker(session, prefix: null);
        tracker.OnToken(YamlTokenType.StartMapping, null, Mark.Empty);
        tracker.OnToken(start, null, Mark.Empty);
        tracker.OnToken(YamlTokenType.Scalar, "inside", Mark.Empty);
        if (start == YamlTokenType.StartMapping) tracker.OnToken(YamlTokenType.Scalar, "key", Mark.Empty);
        tracker.OnToken(end, null, Mark.Empty);
        tracker.OnToken(YamlTokenType.Scalar, "value", Mark.Empty);
        Assert.AreEqual("$['?']", tracker.GetPath());
        tracker.OnToken(YamlTokenType.Scalar, "next", Mark.Empty);
        Assert.AreEqual("$.next", tracker.GetPath());
    }
}

internal sealed class ComplexKeyModel
{
    public Dictionary<List<string>, string> Items { get; set; } = new();
}

internal sealed class ComplexObjectKey
{
    public string Name { get; set; } = "";
}

internal sealed class ComplexMergeRoot
{
    public ComplexObjectKey Base { get; set; } = new();
    public object? Merged { get; set; }
}

[YamlSerializable(typeof(ComplexKeyModel))]
[YamlSerializable(typeof(Dictionary<List<string>, string>))]
[YamlSerializable(typeof(Dictionary<ComplexObjectKey, string>))]
[YamlSerializable(typeof(ComplexMergeRoot))]
internal partial class ComplexKeyContext : YamlSerializerContext;

[YamlSerializable(typeof(object))]
internal partial class UntypedRootContext : YamlSerializerContext;
