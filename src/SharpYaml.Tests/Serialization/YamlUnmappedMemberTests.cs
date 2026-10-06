using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlUnmappedMemberTests
{
    private const string NestedYaml =
        "Servers:\n" +
        "  - Name: a\n" +
        "  - Name: b\n" +
        "    Tls:\n" +
        "      Enabled: true\n" +
        "      Colour: red\n" +
        "      Extra:\n" +
        "        X: 1\n";

    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions options, bool generated)
    {
        if (!generated)
        {
            return YamlSerializer.Deserialize<T>(yaml, options);
        }

        var context = new UnmappedMemberContext(options);
        return (T?)YamlSerializer.Deserialize(yaml, typeof(T), context);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsPathLocationInstanceAndValue(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { SourceName = "config.yaml", UnmappedMemberCallback = members.Add };

        var root = Deserialize<UnmappedRoot>(NestedYaml, options, generated)!;

        Assert.HasCount(2, members);

        var colour = members[0];
        Assert.AreEqual("Colour", colour.Name);
        Assert.AreEqual("$.Servers[1].Tls.Colour", colour.Path);
        Assert.AreEqual("config.yaml", colour.SourceName);
        Assert.AreEqual(typeof(UnmappedTls), colour.DeclaringType);
        Assert.AreSame(root.Servers[1].Tls, colour.Instance);
        Assert.AreEqual(5, colour.KeyStart.Line);
        Assert.AreEqual(6, colour.KeyStart.Column);
        Assert.AreEqual(5, colour.ValueStart.Line);
        Assert.IsFalse(colour.IsCapturedByExtensionData);
        Assert.AreEqual("red", ((YamlValue)colour.Value!).Value);
        Assert.AreEqual("Color", colour.SuggestedName);
        CollectionAssert.AreEquivalent(new[] { "Enabled", "Color" }, colour.KnownMemberNames.ToArray());

        var extra = members[1];
        Assert.AreEqual("$.Servers[1].Tls.Extra", extra.Path);
        Assert.AreEqual(6, extra.KeyStart.Line);
        Assert.IsInstanceOfType<YamlMapping>(extra.Value);
        Assert.IsNull(extra.SuggestedName);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_QuotesKeysThatAreNotSimpleIdentifiers(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        Deserialize<UnmappedTls>("a.b: 1\nit's: 2\n", options, generated);

        Assert.AreEqual("$['a.b']", members[0].Path);
        Assert.AreEqual("$['it\\'s']", members[1].Path);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsExtensionDataMembersAndStillStoresThem(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedWithExtensionData>("Known: 1\nOther:\n  Nested: [1, 2]\nPlain: text\n", options, generated)!;

        Assert.HasCount(2, value.Extra!);
        Assert.HasCount(2, members);
        Assert.IsTrue(members.All(static m => m.IsCapturedByExtensionData));
        Assert.AreSame(value, members[0].Instance);
        Assert.AreEqual("$.Other", members[0].Path);
        Assert.AreEqual(1, members[0].KeyStart.Line);
        Assert.IsInstanceOfType<YamlMapping>(members[0].Value);
        Assert.AreEqual("text", ((YamlValue)members[1].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsMappingExtensionDataMembers(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedWithMappingExtensionData>("Known: 1\nOther: 2\n", options, generated)!;

        Assert.HasCount(1, value.Extra!);
        Assert.HasCount(1, members);
        Assert.IsTrue(members[0].IsCapturedByExtensionData);
        Assert.AreEqual("2", ((YamlValue)members[0].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_IsNotInvokedWhenUnmappedMembersAreDisallowed(bool generated)
    {
        var count = 0;
        var options = new YamlSerializerOptions
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            UnmappedMemberCallback = _ => count++,
            UnmappedMembersFinalizer = _ => count++,
        };

        Assert.Throws<YamlException>(() => Deserialize<UnmappedTls>("Enabled: true\nNope: 1\n", options, generated));
        Assert.AreEqual(0, count);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsMembersOfConstructorBoundTypesWithoutInstance(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedConstructorModel>("Id: 5\nOops: 1\n", options, generated)!;

        Assert.AreEqual(5, value.Id);
        Assert.HasCount(1, members);
        Assert.IsNull(members[0].Instance);
        Assert.AreEqual(typeof(UnmappedConstructorModel), members[0].DeclaringType);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsMembersFromMergeKeyAgainstTheOwner(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedMergeRoot>("Tls:\n  <<: {Enabled: true, Oops: 1}\n", options, generated)!;

        Assert.IsTrue(value.Tls!.Enabled);
        Assert.HasCount(1, members);
        Assert.AreEqual("Oops", members[0].Name);
        Assert.AreSame(value.Tls, members[0].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsMembersInsideUnmappedContentWithAliases(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedTls>("Enabled: true\nBase: &x {A: 1}\nCopy: *x\n", options, generated)!;

        Assert.IsTrue(value.Enabled);
        CollectionAssert.AreEqual(new[] { "Base", "Copy" }, members.Select(static m => m.Name).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsMembersOfPolymorphicTypesWithTheFullPath(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedZoo>("Pets:\n  - Name: a\n    Oops: 1\n    $type: dog\n", options, generated)!;

        Assert.IsInstanceOfType<UnmappedDog>(value.Pets[0]);
        Assert.HasCount(1, members);
        Assert.AreEqual("Oops", members[0].Name);
        Assert.AreEqual("$.Pets[0].Oops", members[0].Path);
        Assert.AreSame(value.Pets[0], members[0].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_DoesNotReportDiscriminatorMatchedIgnoringCase(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberCallback = members.Add };

        var value = Deserialize<UnmappedZoo>("Pets:\n  - $TYPE: dog\n    Name: a\n    Oops: 1\n", options, generated)!;

        Assert.IsInstanceOfType<UnmappedDog>(value.Pets[0]);
        CollectionAssert.AreEqual(new[] { "Oops" }, members.Select(static m => m.Name).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsPathsThroughNestedPolymorphicNodes(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        Deserialize<UnmappedZoo>("Pets:\n  - $type: dog\n    Friend:\n      $type: dog\n      Oops: 1\n    Sub: {$type: x}\nTail: 1\n", options, generated);

        // A '$type' key is only the discriminator on the root mapping of a polymorphic node.
        CollectionAssert.AreEqual(
            new[] { "$.Pets[0].Friend.Oops", "$.Pets[0].Sub", "$.Tail" },
            members.Select(static m => m.Path).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Callback_ReportsPathsThroughNestedSequencesAndDictionaries(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        Deserialize<UnmappedContainers>("Matrix:\n  - - Enabled: true\n      Oops: 1\n    - Oops: 2\nMap:\n  a: {Oops: 3}\n  'b c': {Oops: 4}\n", options, generated);

        CollectionAssert.AreEqual(
            new[] { "$.Matrix[0][0].Oops", "$.Matrix[0][1].Oops", "$.Map.a.Oops", "$.Map['b c'].Oops" },
            members.Select(static m => m.Path).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Finalizer_ReceivesAllMembersOnceInDocumentOrder(bool generated)
    {
        var calls = new List<string[]>();
        var options = new YamlSerializerOptions
        {
            UnmappedMembersFinalizer = members => calls.Add(members.Select(static m => m.Path).ToArray()),
        };

        Deserialize<UnmappedRoot>(NestedYaml + "Zzz: 1\n", options, generated);

        Assert.HasCount(1, calls);
        CollectionAssert.AreEqual(new[] { "$.Servers[1].Tls.Colour", "$.Servers[1].Tls.Extra", "$.Zzz" }, calls[0]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Finalizer_IsNotInvokedWithoutUnmappedMembers(bool generated)
    {
        var called = false;
        var options = new YamlSerializerOptions { UnmappedMembersFinalizer = _ => called = true };

        Deserialize<UnmappedTls>("Enabled: true\n", options, generated);

        Assert.IsFalse(called);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Finalizer_CanFailTheDeserialization(bool generated)
    {
        var options = new YamlSerializerOptions
        {
            SourceName = "config.yaml",
            UnmappedMembersFinalizer = members => throw new YamlException(members[0].SourceName, members[0].KeyStart, members[0].ValueStart, $"{members.Count} unknown"),
        };

        var exception = Assert.Throws<YamlException>(() => Deserialize<UnmappedTls>("Enabled: true\nA: 1\nB: 2\n", options, generated));
        StringAssert.Contains(exception.Message, "2 unknown");
        Assert.IsFalse(YamlSerializer.TryDeserialize<UnmappedTls>("A: 1\n", out _, options));
    }

    [TestMethod]
    public void Handlers_AreSafeToShareAcrossConcurrentDeserializations()
    {
        var total = 0;
        var options = new YamlSerializerOptions { UnmappedMemberCallback = _ => Interlocked.Increment(ref total) };

        Parallel.For(0, 200, _ => YamlSerializer.Deserialize<UnmappedRoot>(NestedYaml, options));

        Assert.AreEqual(400, total);
    }

    [TestMethod]
    public void Suggestions_IgnoreUnrelatedNamesAndCaseDifferences()
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        YamlSerializer.Deserialize<UnmappedTls>("enabled: true\nEnbled: true\nCoolr: red\nSomethingElseEntirely: 1\n", options);

        CollectionAssert.AreEqual(new[] { "Enabled", "Enabled", "Color", null }, members.Select(static m => m.SuggestedName).ToArray());
    }

    [TestMethod]
    public void DefaultOptions_DoNotReportAnything()
    {
        var value = YamlSerializer.Deserialize<UnmappedTls>("Enabled: true\nNope: 1\n")!;

        Assert.IsTrue(value.Enabled);
    }
}

internal sealed class UnmappedRoot
{
    public List<UnmappedServer> Servers { get; set; } = new();
}

internal sealed class UnmappedServer
{
    public string Name { get; set; } = string.Empty;
    public UnmappedTls? Tls { get; set; }
}

internal sealed class UnmappedTls
{
    public bool Enabled { get; set; }
    public string? Color { get; set; }
}

internal sealed class UnmappedMergeRoot
{
    public UnmappedTls? Tls { get; set; }
}

internal sealed class UnmappedWithExtensionData
{
    public int Known { get; set; }

    [YamlExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

internal sealed class UnmappedWithMappingExtensionData
{
    public int Known { get; set; }

    [YamlExtensionData]
    public YamlMapping? Extra { get; set; }
}

internal sealed class UnmappedContainers
{
    public List<List<UnmappedTls>> Matrix { get; set; } = new();
    public Dictionary<string, UnmappedTls> Map { get; set; } = new();
}

internal sealed class UnmappedConstructorModel
{
    public UnmappedConstructorModel(int id)
    {
        Id = id;
    }

    public int Id { get; }
}

internal sealed class UnmappedZoo
{
    public List<UnmappedAnimal> Pets { get; set; } = new();
}

[YamlPolymorphic]
[YamlDerivedType(typeof(UnmappedDog), "dog")]
internal abstract class UnmappedAnimal
{
    public string Name { get; set; } = string.Empty;
    public UnmappedAnimal? Friend { get; set; }
}

internal sealed class UnmappedDog : UnmappedAnimal
{
    public int BarkVolume { get; set; }
}

[YamlSerializable(typeof(UnmappedRoot))]
[YamlSerializable(typeof(UnmappedTls))]
[YamlSerializable(typeof(UnmappedMergeRoot))]
[YamlSerializable(typeof(UnmappedWithExtensionData))]
[YamlSerializable(typeof(UnmappedWithMappingExtensionData))]
[YamlSerializable(typeof(UnmappedConstructorModel))]
[YamlSerializable(typeof(UnmappedZoo))]
[YamlSerializable(typeof(UnmappedContainers))]
internal partial class UnmappedMemberContext : YamlSerializerContext
{
    public UnmappedMemberContext()
    {
    }

    public UnmappedMemberContext(YamlSerializerOptions options) : base(options)
    {
    }
}
