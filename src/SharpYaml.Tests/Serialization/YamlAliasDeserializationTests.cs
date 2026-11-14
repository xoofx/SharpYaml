#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// Aliases deserialize by default, as a copy of their anchored node, and keep object identity under
/// <see cref="YamlReferenceHandling.Preserve"/>, with the reflection-based and the source-generated serializers.
/// </summary>
[TestClass]
public sealed class YamlAliasDeserializationTests
{
    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    private static readonly YamlSerializerOptions s_preserve = new() { ReferenceHandling = YamlReferenceHandling.Preserve };

    private static T? Deserialize<T>(string yaml, bool generated, YamlSerializerOptions? options = null)
    {
        options ??= YamlSerializerOptions.Default;
        return generated
            ? (T?)YamlSerializer.Deserialize(yaml, typeof(T), new AliasContext(options))
            : YamlSerializer.Deserialize<T>(yaml, options);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ScalarAliases_AreExpandedByDefault(bool generated)
    {
        var value = Deserialize<AliasScalars>("A: &s hello\nB: *s\nN: &n 42\nM: *n\n", generated)!;

        Assert.AreEqual("hello", value.A);
        Assert.AreEqual("hello", value.B);
        Assert.AreEqual(42, value.N);
        Assert.AreEqual(42, value.M);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ScalarAliases_AreExpandedWhenPreservingReferences(bool generated)
    {
        var value = Deserialize<AliasScalars>("A: &s hello\nB: *s\nN: &n 42\nM: *n\n", generated, s_preserve)!;

        Assert.AreEqual("hello", value.B);
        Assert.AreEqual(42, value.M);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void CollectionAndObjectAliases_AreCopiedByDefault(bool generated)
    {
        const string yaml = """
            First: &item {Name: shared, Age: 7}
            Second: *item
            List: &list [1, 2, 3]
            CopyList: *list
            Array: *list
            Set: *list
            Map: &map {a: 1, b: 2}
            CopyMap: *map
            """;

        var value = Deserialize<AliasRoot>(yaml, generated)!;

        Assert.AreEqual("shared", value.Second!.Name);
        Assert.AreEqual(7, value.Second.Age);
        Assert.AreNotSame(value.First, value.Second);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, value.CopyList);
        Assert.AreNotSame(value.List, value.CopyList);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, value.Array);
        Assert.HasCount(3, value.Set!);
        Assert.AreEqual(2, value.CopyMap!["b"]);
        Assert.AreNotSame(value.Map, value.CopyMap);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ObjectAliases_KeepIdentityWhenPreservingReferences(bool generated)
    {
        var value = Deserialize<AliasRoot>("First: &item {Name: shared}\nSecond: *item\nList: &list [1]\nCopyList: *list\n", generated, s_preserve)!;

        Assert.AreSame(value.First, value.Second);
        Assert.AreSame(value.List, value.CopyList);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void AliasesToObjectsOfAnotherType_AreCopiedWhenPreservingReferences(bool generated)
    {
        // The anchored node is a List<int>; the alias is read as int[], HashSet<int> and Dictionary values: no InvalidCastException.
        var value = Deserialize<AliasRoot>("List: &list [1, 2]\nArray: *list\nSet: *list\nMap: &d {a: 1}\nCopyMap: *d\n", generated, s_preserve)!;

        CollectionAssert.AreEqual(new[] { 1, 2 }, value.Array);
        Assert.HasCount(2, value.Set!);
        Assert.AreSame(value.Map, value.CopyMap);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void CopiedAliases_ReportInvalidValuesAtThePathOfTheAlias(bool generated)
    {
        // Labels is a Dictionary<string, string>, so *m is copied into the Dictionary<string, int>, where "x" is invalid.
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions
        {
            ReferenceHandling = YamlReferenceHandling.Preserve,
            InvalidValueHandling = YamlInvalidValueHandling.Skip,
            InvalidValueCallback = values.Add,
        };

        var value = Deserialize<AliasRoot>("Labels: &m {a: x, b: '2'}\nCopyMap: *m\nTemplate: 3\n", generated, options)!;

        Assert.AreEqual(2, value.CopyMap!["b"]);
        Assert.AreEqual(3, value.Template);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.CopyMap.a", values[0].Path);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void NestedAnchorsOfACopiedAlias_KeepIdentityWhenPreservingReferences(bool generated)
    {
        // B is another type than A, so *a is copied, but the anchored Inner node inside it is the same object.
        var value = Deserialize<AliasPair>("A: &a\n  Inner: &i {Name: x}\nB: *a\n", generated, s_preserve)!;

        Assert.AreEqual("x", value.B!.Inner!.Name);
        Assert.AreSame(value.A!.Inner, value.B.Inner);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void AliasedMappingKeys_AreExpanded(bool generated)
    {
        var value = Deserialize<AliasRoot>("Labels:\n  first: &k shared\n  *k : value\n", generated)!;

        Assert.AreEqual("shared", value.Labels!["first"]);
        Assert.AreEqual("value", value.Labels["shared"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void MergeAliases_AreAppliedByDefault(bool generated)
    {
        const string yaml = """
            Base: &base {Name: base, Age: 1}
            Child:
              <<: *base
              Age: 2
            Labels:
              <<: [*labels, {extra: x}]
            """;

        var value = Deserialize<AliasMergeRoot>("Defaults: &labels {a: one}\n" + yaml, generated)!;

        Assert.AreEqual("base", value.Child!.Name);
        Assert.AreEqual(2, value.Child.Age);
        Assert.AreEqual("one", value.Labels!["a"]);
        Assert.AreEqual("x", value.Labels["extra"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void MergeAliases_AreAppliedWhenPreservingReferences(bool generated)
    {
        var value = Deserialize<AliasMergeRoot>("Base: &base {Name: base, Age: 1}\nChild:\n  <<: *base\n  Age: 2\n", generated, s_preserve)!;

        Assert.AreEqual("base", value.Child!.Name);
        Assert.AreEqual(2, value.Child.Age);
        Assert.AreNotSame(value.Base, value.Child);
    }

    [TestMethod]
    public void MergeAliases_AreAppliedToUntypedValues()
    {
        var value = (Dictionary<string, object?>)YamlSerializer.Deserialize<object>("base: &b {x: 1}\nchild:\n  <<: *b\n  y: 2\n")!;
        var child = (Dictionary<string, object?>)value["child"]!;

        Assert.AreEqual(1L, child["x"]);
        Assert.AreEqual(2L, child["y"]);
    }

    [TestMethod]
    public void UntypedScalarAliases_AreExpandedWhenPreservingReferencesWithLastWins()
    {
        var options = new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve, DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };

        var value = (Dictionary<string, object?>)YamlSerializer.Deserialize<object>("&k key: &v 1\n*k : *v\nother: [*v, *k]\n", options)!;

        Assert.HasCount(2, value);
        Assert.AreEqual(1L, value["key"]);
        CollectionAssert.AreEqual(new object?[] { 1L, "key" }, (List<object?>)value["other"]!);
    }

    [TestMethod]
    public void UntypedAliases_AreCopiedByDefault()
    {
        var value = (Dictionary<string, object?>)YamlSerializer.Deserialize<object>("a: &x [1, {k: v}]\nb: *x\nc: &s text\nd: *s\n")!;

        Assert.AreNotSame(value["a"], value["b"]);
        Assert.HasCount(2, (List<object?>)value["b"]!);
        Assert.AreEqual("text", value["d"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void AliasesToSkippedNodes_AreExpanded(bool generated)
    {
        var value = Deserialize<AliasScalars>("Unused: &u {deep: [hello]}\nTemplate: &t hello\nA: *t\n", generated)!;

        Assert.AreEqual("hello", value.A);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void UnknownAlias_Throws(bool generated)
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<AliasScalars>("A: *missing\n", generated));
        StringAssert.Contains(exception.Message, "*missing");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void UnknownAlias_ThrowsWhenPreservingReferences(bool generated)
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<AliasRoot>("First: *missing\n", generated, s_preserve));
        StringAssert.Contains(exception.Message, "*missing");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void RecursiveAlias_ThrowsByDefaultWithPreserveAdvice(bool generated)
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<AliasNode>("&n\nName: root\nNext: *n\n", generated));
        StringAssert.Contains(exception.Message, "*n");
        StringAssert.Contains(exception.Message, "ReferenceHandling");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void RecursiveAlias_KeepsIdentityWhenPreservingReferences(bool generated)
    {
        var value = Deserialize<AliasNode>("&n\nName: root\nNext: *n\n", generated, s_preserve)!;

        Assert.AreSame(value, value.Next);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void RedefinedAnchor_BindsTheLatestNode(bool generated)
    {
        var value = Deserialize<AliasScalars>("A: &x first\nB: *x\nTemplate: &x second\nC: *x\n", generated)!;

        Assert.AreEqual("first", value.B);
        Assert.AreEqual("second", value.C);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void RedefinedAnchor_BindsTheLatestObjectWhenPreservingReferences(bool generated)
    {
        var value = Deserialize<AliasRoot>("First: &x {Name: first}\nSecond: &x {Name: second}\nThird: *x\n", generated, s_preserve)!;

        Assert.AreSame(value.Second, value.Third);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void NestedAlias_UsesTheAnchorBoundWhereItAppears(bool generated)
    {
        // *x inside &l refers to the first &x even when &l is replayed after &x was redefined.
        var value = Deserialize<AliasRoot>("Template: &x 1\nList: &l [*x]\nOther: &x 2\nCopyList: *l\n", generated)!;

        CollectionAssert.AreEqual(new[] { 1 }, value.CopyList);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void NestedAliases_AreExpandedWithinTheBudget(bool generated)
    {
        var value = Deserialize<AliasRoot>("Unused1: &a [1, 2]\nUnused2: &b [*a, *a]\nGrid: [*b, *b]\n", generated)!;

        var grid = value.Grid!;
        Assert.HasCount(2, grid);
        Assert.HasCount(2, grid[1]);
        CollectionAssert.AreEqual(new[] { 1, 2 }, grid[1][1]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExponentialAliases_ExceedTheExpansionBudget(bool generated)
    {
        // Unmapped definitions are recorded without expansion; only the consumed value spends the budget.
        var builder = new StringBuilder("L0: &a0 [lol, lol, lol, lol, lol, lol, lol, lol, lol, lol]\n");
        for (var i = 1; i < 10; i++)
        {
            builder.Append("L").Append(i).Append(": &a").Append(i).Append(" [");
            for (var j = 0; j < 10; j++)
            {
                builder.Append(j == 0 ? "" : ", ").Append("*a").Append(i - 1);
            }

            builder.Append("]\n");
        }
        builder.Append("Value: *a9\n");

        var stopwatch = Stopwatch.StartNew();
        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<AliasExpansionTarget>(builder.ToString(), generated));
        stopwatch.Stop();

        StringAssert.Contains(exception.Message, nameof(YamlSerializerOptions.MaxAliasExpansion));
        Assert.IsLessThan(TimeSpan.FromSeconds(10), stopwatch.Elapsed);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void MaxAliasExpansion_LimitsTheNodesAliasesProduce(bool generated)
    {
        const string yaml = "List: &l [1, 2, 3]\nCopyList: *l\n";
        var tight = new YamlSerializerOptions { MaxAliasExpansion = 3 };
        var enough = new YamlSerializerOptions { MaxAliasExpansion = 4 };

        Assert.ThrowsExactly<YamlException>(() => Deserialize<AliasRoot>(yaml, generated, tight));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Deserialize<AliasRoot>(yaml, generated, enough)!.CopyList);
    }

    [TestMethod]
    public void MaxAliasExpansion_RejectsNegativeValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { MaxAliasExpansion = -1 });
        Assert.AreEqual(0, YamlSerializerOptions.Default.MaxAliasExpansion);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExpandedAliases_RespectMaxDepth(bool generated)
    {
        // The anchored node fits within MaxDepth, but its copy nested two levels deeper does not.
        var options = new YamlSerializerOptions { MaxDepth = 4 };
        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<AliasRoot>("Unused: &a [[1]]\nGrid: [[*a]]\n", generated, options));
        StringAssert.Contains(exception.Message, "depth");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void PolymorphicNodes_ResolveAliasesToOuterAnchors(bool generated)
    {
        const string yaml = """
            Template: &name Rex
            Pets:
            - $type: dog
              Name: *name
              Volume: 3
            First: &first {$type: dog, Name: Fido, Volume: 4}
            Second: *first
            """;

        var value = Deserialize<AliasZoo>(yaml, generated)!;

        Assert.AreEqual("Rex", value.Pets![0].Name);
        Assert.AreEqual(4, ((AliasDog)value.Second!).Volume);
        Assert.AreNotSame(value.First, value.Second);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void PolymorphicNodes_KeepIdentityWhenPreservingReferences(bool generated)
    {
        const string yaml = """
            Pets:
            - &rex {$type: dog, Name: Rex}
            - *rex
            - $type: dog
              Name: Self
              Friend: *rex
            """;

        var value = Deserialize<AliasZoo>(yaml, generated, s_preserve)!;

        Assert.AreSame(value.Pets![0], value.Pets[1]);
        Assert.AreSame(value.Pets[0], ((AliasDog)value.Pets[2]).Friend);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void PolymorphicNodes_ExpandAliasesToAnchorsTheyDefine(bool generated)
    {
        const string yaml = """
            Pets:
            - $type: dog
              Name: &inner Rex
              Friend: {$type: dog, Name: *inner}
            Template: *inner
            """;

        var value = Deserialize<AliasZoo>(yaml, generated)!;

        Assert.AreEqual("Rex", ((AliasDog)value.Pets![0]).Friend!.Name);
        Assert.AreEqual("Rex", value.Template);
    }

    [TestMethod]
    public void Reader_ReplaysAnchoredNodesWithoutTheirAnchors()
    {
        var reader = YamlReader.Create("[&a [1, &b two], *a, *b]");
        var tokens = new List<string>();
        while (reader.Read())
        {
            tokens.Add($"{reader.TokenType}:{reader.ScalarValue}:{reader.Anchor}");
        }

        CollectionAssert.AreEqual(
            new[]
            {
                "StartSequence::", "StartSequence::a", "Scalar:1:", "Scalar:two:b", "EndSequence::",
                "StartSequence::", "Scalar:1:", "Scalar:two:", "EndSequence::",
                "Scalar:two:",
                "EndSequence::",
            },
            tokens);
    }

    [TestMethod]
    public void Reader_ScopesAnchorsToTheirDocument()
    {
        var reader = YamlReader.Create("a: &x 1\n---\nb: *x\n");
        var exception = Assert.ThrowsExactly<YamlException>(() =>
        {
            while (reader.Read())
            {
            }
        });

        StringAssert.Contains(exception.Message, "*x");
    }

    [TestMethod]
    public void Reader_TryReadAliasResolvesRegisteredObjectsWhenPreservingReferences()
    {
        var reader = YamlReader.Create("[&a {x: 1}, *a]", s_preserve);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        Assert.AreEqual("a", reader.Anchor);
        var registered = new object();
        reader.RegisterAnchor("a", registered);
        reader.Skip();

        Assert.AreEqual(YamlTokenType.Alias, reader.TokenType);
        Assert.AreEqual("a", reader.Alias);
        Assert.IsTrue(reader.TryReadAlias(out var value));
        Assert.AreSame(registered, value);
        Assert.AreEqual(YamlTokenType.EndSequence, reader.TokenType);
    }

    [TestMethod]
    public void Reader_TryReadAliasOfAnotherTypeReplaysTheAnchoredNode()
    {
        var reader = YamlReader.Create("[&a {x: 1}, *a]", s_preserve);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        reader.RegisterAnchor("a", new object());
        reader.Skip();

        Assert.IsFalse(reader.TryReadAlias<string>(out var value));
        Assert.IsNull(value);
        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);
        Assert.IsNull(reader.Anchor);
    }

    [TestMethod]
    public void Reader_ExpandAliasCopiesAliasedKeysWhenPreservingReferences()
    {
        // Mapping keys compare structurally: a key that aliases a registered node is read as a copy, not as the object.
        var reader = YamlReader.Create("{k: &a [x], *a : 1}", s_preserve);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        reader.RegisterAnchor("a", new List<object?>());
        reader.Skip();

        reader.ExpandAlias();

        Assert.AreEqual(YamlTokenType.StartSequence, reader.TokenType);
        Assert.IsNull(reader.Anchor);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual("x", reader.ScalarValue);
    }

    [TestMethod]
    public void Reader_ExpandAliasDoesNothingOnOtherTokens()
    {
        var reader = YamlReader.Create("{k: &a [x], *a : 1}");
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());

        reader.ExpandAlias();

        Assert.AreEqual(YamlTokenType.Scalar, reader.TokenType);
        Assert.AreEqual("k", reader.ScalarValue);
    }

    [TestMethod]
    public void Reader_TryReplaceAnchoredObjectRefusesObjectsAnAliasResolvedTo()
    {
        var reader = YamlReader.Create("&a [*a]", s_preserve);
        Assert.IsTrue(reader.Read());
        var original = new List<object?>();
        reader.RegisterAnchor("a", original);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.TryReadAlias(out var self));
        Assert.AreSame(original, self);

        Assert.IsTrue(reader.WasResolvedByAlias(original));
        Assert.IsFalse(reader.TryReplaceAnchoredObject("a", original, new Dictionary<object, object?>()));
    }

    [TestMethod]
    public void Reader_TryReplaceAnchoredObjectRebindsObjectsNoAliasResolvedTo()
    {
        var reader = YamlReader.Create("[&a [1], *a]", s_preserve);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        var original = new List<object?>();
        reader.RegisterAnchor("a", original);
        var replacement = new List<object?>();

        Assert.IsFalse(reader.WasResolvedByAlias(original));
        Assert.IsTrue(reader.TryReplaceAnchoredObject("a", original, replacement));
        reader.Skip();
        Assert.IsTrue(reader.TryReadAlias(out var value));
        Assert.AreSame(replacement, value);
    }

    [TestMethod]
    public void Reader_TryReplaceAnchoredObjectAcceptsAnythingWithoutReferenceHandling()
    {
        var reader = YamlReader.Create("&a [1]");
        Assert.IsTrue(reader.Read());

        Assert.IsTrue(reader.TryReplaceAnchoredObject("a", new object(), new object()));
        Assert.IsFalse(reader.WasResolvedByAlias(new object()));
    }

    [TestMethod]
    public void Reader_ExpandsUnregisteredAliasesWhenPreservingReferences()
    {
        var reader = YamlReader.Create("[&a {x: 1}, *a]", s_preserve);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        reader.Skip();

        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);
        Assert.IsFalse(reader.TryReadAlias(out _));
    }
}

public sealed class AliasScalars
{
    public string? A { get; set; }

    public string? B { get; set; }

    public string? C { get; set; }

    public int N { get; set; }

    public int M { get; set; }

    public string? Template { get; set; }
}

public sealed class AliasExpansionTarget
{
    public object? Value { get; set; }
}

public sealed class AliasItem
{
    public string? Name { get; set; }

    public int Age { get; set; }
}

public sealed class AliasRoot
{
    public AliasItem? First { get; set; }

    public AliasItem? Second { get; set; }

    public AliasItem? Third { get; set; }

    public int Template { get; set; }

    public int Other { get; set; }

    public List<int>? List { get; set; }

    public List<int>? CopyList { get; set; }

    public int[]? Array { get; set; }

    public HashSet<int>? Set { get; set; }

    public Dictionary<string, int>? Map { get; set; }

    public Dictionary<string, int>? CopyMap { get; set; }

    public Dictionary<string, string>? Labels { get; set; }

    public List<List<List<int>>>? Grid { get; set; }
}

public sealed class AliasMergeRoot
{
    public Dictionary<string, string>? Defaults { get; set; }

    public AliasItem? Base { get; set; }

    public AliasItem? Child { get; set; }

    public Dictionary<string, string>? Labels { get; set; }
}

public sealed class AliasNode
{
    public string? Name { get; set; }

    public AliasNode? Next { get; set; }
}

public sealed class AliasHolder
{
    public AliasItem? Inner { get; set; }
}

public sealed class AliasOtherHolder
{
    public AliasItem? Inner { get; set; }
}

public sealed class AliasPair
{
    public AliasHolder? A { get; set; }

    public AliasOtherHolder? B { get; set; }
}

[YamlPolymorphic]
[YamlDerivedType(typeof(AliasDog), "dog")]
public abstract class AliasAnimal
{
    public string? Name { get; set; }
}

public sealed class AliasDog : AliasAnimal
{
    public int Volume { get; set; }

    public AliasAnimal? Friend { get; set; }
}

public sealed class AliasZoo
{
    public string? Template { get; set; }

    public List<AliasAnimal>? Pets { get; set; }

    public AliasAnimal? First { get; set; }

    public AliasAnimal? Second { get; set; }
}

[YamlSerializable(typeof(AliasScalars))]
[YamlSerializable(typeof(AliasExpansionTarget))]
[YamlSerializable(typeof(AliasRoot))]
[YamlSerializable(typeof(AliasMergeRoot))]
[YamlSerializable(typeof(AliasNode))]
[YamlSerializable(typeof(AliasPair))]
[YamlSerializable(typeof(AliasZoo))]
[YamlSerializable(typeof(AliasDog))]
internal partial class AliasContext : YamlSerializerContext
{
    public AliasContext()
    {
    }

    public AliasContext(YamlSerializerOptions options) : base(options)
    {
    }
}
