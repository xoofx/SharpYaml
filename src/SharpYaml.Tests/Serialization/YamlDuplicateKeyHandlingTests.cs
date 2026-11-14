#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlDuplicateKeyHandlingTests
{
    private sealed class DuplicateModel
    {
        public int Age { get; set; }
    }

    [TestMethod]
    public void Dictionary_DuplicateKey_Error_Throws()
    {
        var yaml = "a: 1\na: 2\n";
        var options = new SharpYaml.YamlSerializerOptions { DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.Error };

        var exception = Assert.Throws<SharpYaml.YamlException>(() => SharpYaml.YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options));
        StringAssert.Contains(exception.Message, "Duplicate");
    }

    [TestMethod]
    public void Dictionary_DuplicateKey_FirstWins()
    {
        var yaml = "a: 1\na: 2\n";
        var options = new SharpYaml.YamlSerializerOptions { DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.FirstWins };

        var result = SharpYaml.YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result["a"]);
    }

    [TestMethod]
    public void Dictionary_DuplicateKey_LastWins()
    {
        var yaml = "a: 1\na: 2\n";
        var options = new SharpYaml.YamlSerializerOptions { DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.LastWins };

        var result = SharpYaml.YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result["a"]);
    }

    [TestMethod]
    public void Dictionary_DuplicateKey_CaseInsensitive_UsesComparer()
    {
        var yaml = "A: 1\na: 2\n";
        var options = new SharpYaml.YamlSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.LastWins,
        };

        var result = SharpYaml.YamlSerializer.Deserialize<Dictionary<string, int>>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result["a"]);
    }

    [TestMethod]
    public void Object_DuplicateKey_FirstWins()
    {
        var yaml = "Age: 1\nAge: 2\n";
        var options = new SharpYaml.YamlSerializerOptions { DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.FirstWins };

        var result = SharpYaml.YamlSerializer.Deserialize<DuplicateModel>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Age);
    }

    [TestMethod]
    public void Object_DuplicateKey_LastWins()
    {
        var yaml = "Age: 1\nAge: 2\n";
        var options = new SharpYaml.YamlSerializerOptions { DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.LastWins };

        var result = SharpYaml.YamlSerializer.Deserialize<DuplicateModel>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Age);
    }

    [TestMethod]
    public void Object_DuplicateKey_Error_Throws()
    {
        var yaml = "Age: 1\nAge: 2\n";
        var options = new SharpYaml.YamlSerializerOptions { DuplicateKeyHandling = SharpYaml.YamlDuplicateKeyHandling.Error };

        var exception = Assert.Throws<SharpYaml.YamlException>(() => SharpYaml.YamlSerializer.Deserialize<DuplicateModel>(yaml, options));
        StringAssert.Contains(exception.Message, "Duplicate");
    }

    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    public static IEnumerable<object[]> SchemaModes
    {
        get
        {
            foreach (var schema in new[] { YamlSchemaKind.Core, YamlSchemaKind.Extended, YamlSchemaKind.Json, YamlSchemaKind.Failsafe })
            {
                yield return new object[] { schema, false };
                yield return new object[] { schema, true };
            }
        }
    }

    public static IEnumerable<object[]> PopulateModes
    {
        get
        {
            foreach (var handling in new[] { YamlDuplicateKeyHandling.Error, YamlDuplicateKeyHandling.FirstWins, YamlDuplicateKeyHandling.LastWins })
            {
                yield return new object[] { handling, false };
                yield return new object[] { handling, true };
            }
        }
    }

    private static readonly (string Member, string Key, string EquivalentKey)[] PopulatedDictionaries =
    [
        ("ByName", "a", "a"),
        ("ByNumber", "1", "1"),
        ("NameMap", "a", "a"),
        ("NumberMap", "1", "1"),
        ("CustomMap", "1", "-1"),
        ("SortedMap", "1", "-1"),
        ("SortedListMap", "1", "-1"),
        ("OrderedMap", "1", "-1"),
    ];

    [TestMethod]
    [DynamicData(nameof(PopulateModes))]
    public void Populate_ExistingKeysAreInitialValues(YamlDuplicateKeyHandling handling, bool generated)
    {
        var options = new YamlSerializerOptions { DuplicateKeyHandling = handling };

        foreach (var (member, key, _) in PopulatedDictionaries)
        {
            var root = Deserialize<PopulatedDuplicateKeyDictionaries>($"{member}: {{{key}: 2}}\n", options, generated)!;

            AssertPopulatedValues(root, member, 2);
        }
    }

    [TestMethod]
    [DynamicData(nameof(PopulateModes))]
    public void Populate_DuplicateDocumentKeysUseHandling(YamlDuplicateKeyHandling handling, bool generated)
    {
        var options = new YamlSerializerOptions { DuplicateKeyHandling = handling };

        foreach (var (member, key, equivalentKey) in PopulatedDictionaries)
        {
            var yaml = $"{member}: {{{key}: 2, {equivalentKey}: 3}}\n";
            if (handling == YamlDuplicateKeyHandling.Error)
            {
                var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<PopulatedDuplicateKeyDictionaries>(yaml, options, generated), member);
                Assert.AreEqual($"Duplicate mapping key '{equivalentKey}'.", exception.Reason);
                Assert.AreEqual(yaml.LastIndexOf('3'), exception.Start.Column);
            }
            else
            {
                var root = Deserialize<PopulatedDuplicateKeyDictionaries>(yaml, options, generated)!;
                AssertPopulatedValues(root, member, handling == YamlDuplicateKeyHandling.FirstWins ? 2 : 3);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(PopulateModes))]
    public void Populate_DroppedDocumentKeysStillCountAsSeen(YamlDuplicateKeyHandling handling, bool generated)
    {
        foreach (var (member, key, equivalentKey) in PopulatedDictionaries)
        {
            var values = new List<YamlInvalidValue>();
            var options = new YamlSerializerOptions { DuplicateKeyHandling = handling, InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };
            var yaml = $"{member}: {{{key}: abc, {equivalentKey}: 3}}\n";
            if (handling == YamlDuplicateKeyHandling.Error)
            {
                var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<PopulatedDuplicateKeyDictionaries>(yaml, options, generated), member);
                Assert.AreEqual($"Duplicate mapping key '{equivalentKey}'.", exception.Reason);
                Assert.AreEqual(yaml.LastIndexOf('3'), exception.Start.Column);
            }
            else
            {
                var root = Deserialize<PopulatedDuplicateKeyDictionaries>(yaml, options, generated)!;
                AssertPopulatedValues(root, member, handling == YamlDuplicateKeyHandling.FirstWins ? 1 : 3);
            }
            Assert.HasCount(1, values, member);
        }
    }

    private static void AssertPopulatedValues(PopulatedDuplicateKeyDictionaries root, string member, int expected)
    {
        var dictionary = typeof(PopulatedDuplicateKeyDictionaries).GetProperty(member)!.GetValue(root);
        if (dictionary is IDictionary<string, int> byName)
        {
            Assert.AreEqual(2, byName.Count, member);
            Assert.AreEqual(expected, byName["a"], member);
            Assert.AreEqual(7, byName["keep"], member);
        }
        else
        {
            var byNumber = (IDictionary<int, int>)dictionary!;
            Assert.AreEqual(2, byNumber.Count, member);
            Assert.AreEqual(expected, byNumber[1], member);
            Assert.AreEqual(7, byNumber[9], member);
        }
    }

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions options, bool generated)
        => generated ? (T?)YamlSerializer.Deserialize(yaml, typeof(T), new DuplicateKeyContext(options)) : YamlSerializer.Deserialize<T>(yaml, options);

    [TestMethod]
    [DynamicData(nameof(SchemaModes))]
    public void Dictionary_AllSchemas_ErrorDetectsDuplicates(YamlSchemaKind schema, bool generated)
    {
        var options = new YamlSerializerOptions { Schema = schema, DuplicateKeyHandling = YamlDuplicateKeyHandling.Error, PropertyNameCaseInsensitive = true };

        var root = Assert.ThrowsExactly<YamlException>(() => Deserialize<Dictionary<string, int>>("A: 1\na: 2\n", options, generated));
        var member = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyDictionaryMembers>("Values: {A: 1, a: 2}\n", options, generated));

        Assert.AreEqual("Duplicate mapping key 'a'.", root.Reason);
        Assert.AreEqual(1, root.Start.Line);
        Assert.AreEqual(3, root.Start.Column);
        Assert.AreEqual("Duplicate mapping key 'a'.", member.Reason);
        Assert.AreEqual(18, member.Start.Column);
    }

    [TestMethod]
    [DynamicData(nameof(SchemaModes))]
    public void Dictionary_AllSchemas_FirstWinsAndLastWins(YamlSchemaKind schema, bool generated)
    {
        var options = new YamlSerializerOptions { Schema = schema, DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins };

        var first = Deserialize<Dictionary<string, int>>("a: 1\na: [x]\n", options, generated)!;
        var firstMember = Deserialize<DuplicateKeyDictionaryMembers>("Values: {a: 1, a: [x]}\n", options, generated)!;
        Assert.AreEqual(1, first["a"]);
        Assert.AreEqual(1, firstMember.Values["a"]);

        options = options with { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
        var last = Deserialize<Dictionary<string, int>>("a: 1\na: 2\n", options, generated)!;
        var lastMember = Deserialize<DuplicateKeyDictionaryMembers>("Values: {a: 1, a: 2}\n", options, generated)!;
        Assert.AreEqual(2, last["a"]);
        Assert.AreEqual(2, lastMember.Values["a"]);
    }

    [TestMethod]
    [DynamicData(nameof(SchemaModes))]
    public void Dictionary_AllSchemas_DroppedKeysStillCountAsSeen(YamlSchemaKind schema, bool generated)
    {
        var options = new YamlSerializerOptions { Schema = schema, DuplicateKeyHandling = YamlDuplicateKeyHandling.Error, InvalidValueHandling = YamlInvalidValueHandling.Skip };

        var root = Assert.ThrowsExactly<YamlException>(() => Deserialize<Dictionary<string, int>>("a: abc\na: 2\n", options, generated));
        var member = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyDictionaryMembers>("Values: {a: abc, a: 2}\n", options, generated));
        Assert.AreEqual("Duplicate mapping key 'a'.", root.Reason);
        Assert.AreEqual("Duplicate mapping key 'a'.", member.Reason);

        options = options with { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins };
        Assert.IsEmpty(Deserialize<Dictionary<string, int>>("a: abc\na: 2\n", options, generated)!);
        Assert.IsEmpty(Deserialize<DuplicateKeyDictionaryMembers>("Values: {a: abc, a: 2}\n", options, generated)!.Values);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Member_DuplicateKey_Error_ThrowsAtTheSecondKey(bool generated)
    {
        var options = new YamlSerializerOptions { SourceName = "config.yaml", DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyMembers>("Age: 1\nName: a\nAge: 2\n", options, generated));

        Assert.AreEqual("Duplicate mapping key 'Age'.", exception.Reason);
        Assert.AreEqual("config.yaml", exception.SourceName);
        Assert.AreEqual(2, exception.Start.Line);
        Assert.AreEqual(0, exception.Start.Column);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Dictionary_DuplicateKey_Error_ThrowsAtTheSecondValue(bool generated)
    {
        var options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var byName = Assert.ThrowsExactly<YamlException>(() => Deserialize<Dictionary<string, int>>("a: 1\na: 2\n", options, generated));
        var byNumber = Assert.ThrowsExactly<YamlException>(() => Deserialize<Dictionary<int, int>>("1: 1\n1: 2\n", options, generated));

        Assert.AreEqual("Duplicate mapping key 'a'.", byName.Reason);
        Assert.AreEqual(1, byName.Start.Line);
        Assert.AreEqual(3, byName.Start.Column);
        Assert.AreEqual("Duplicate mapping key '1'.", byNumber.Reason);
        Assert.AreEqual(1, byNumber.Start.Line);
        Assert.AreEqual(3, byNumber.Start.Column);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Dictionary_DuplicateKey_FirstWins_SkipsTheSecondValueWithoutReadingIt(bool generated)
    {
        var options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins };

        var byName = Deserialize<Dictionary<string, int>>("a: 1\na: [x]\n", options, generated)!;
        var byNumber = Deserialize<Dictionary<int, int>>("1: 1\n1: [x]\n", options, generated)!;

        Assert.AreEqual(1, byName.Count);
        Assert.AreEqual(1, byName["a"]);
        Assert.AreEqual(1, byNumber.Count);
        Assert.AreEqual(1, byNumber[1]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Member_DuplicateKey_ErrorIsTheDefault(bool generated)
    {
        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyMembers>("Age: 1\nAge: 2\n", new YamlSerializerOptions(), generated));

        StringAssert.Contains(exception.Message, "Duplicate mapping key 'Age'");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Member_DuplicateKey_FirstWinsAndLastWins(bool generated)
    {
        const string yaml = "Age: 1\nName: a\nAge: 2\nName: b\n";

        var first = Deserialize<DuplicateKeyMembers>(yaml, new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins }, generated)!;
        var last = Deserialize<DuplicateKeyMembers>(yaml, new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins }, generated)!;

        Assert.AreEqual(1, first.Age);
        Assert.AreEqual("a", first.Name);
        Assert.AreEqual(2, last.Age);
        Assert.AreEqual("b", last.Name);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Member_DuplicateKey_UsesThePropertyNameComparer(bool generated)
    {
        var options = new YamlSerializerOptions { PropertyNameCaseInsensitive = true, DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyMembers>("Age: 1\nage: 2\n", options, generated));

        StringAssert.Contains(exception.Message, "Duplicate mapping key 'age'");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Member_ExplicitKeyOverridesAMergedKeyWithoutADuplicateError(bool generated)
    {
        var options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var before = Deserialize<DuplicateKeyMembers>("<<: {Age: 1, Name: m}\nAge: 2\n", options, generated)!;
        var after = Deserialize<DuplicateKeyMembers>("Age: 2\n<<: [{Age: 1}, {Age: 3, Name: m}]\n", options, generated)!;

        Assert.AreEqual(2, before.Age);
        Assert.AreEqual("m", before.Name);
        Assert.AreEqual(2, after.Age);
        Assert.AreEqual("m", after.Name);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Constructor_DuplicateKey_Error_Throws(bool generated)
    {
        var options = new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var parameter = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyConstructed>("Age: 1\nAge: 2\n", options, generated));
        var member = Assert.ThrowsExactly<YamlException>(() => Deserialize<DuplicateKeyConstructed>("Age: 1\nName: a\nName: b\n", options, generated));

        Assert.AreEqual("Duplicate mapping key 'Age'.", parameter.Reason);
        Assert.AreEqual(1, parameter.Start.Line);
        Assert.AreEqual("Duplicate mapping key 'Name'.", member.Reason);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Constructor_DuplicateKey_FirstWinsAndLastWins(bool generated)
    {
        const string yaml = "Age: 1\nName: a\nAge: 2\nName: b\n";

        var first = Deserialize<DuplicateKeyConstructed>(yaml, new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins }, generated)!;
        var last = Deserialize<DuplicateKeyConstructed>(yaml, new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins }, generated)!;

        Assert.AreEqual(1, first.Age);
        Assert.AreEqual("a", first.Name);
        Assert.AreEqual(2, last.Age);
        Assert.AreEqual("b", last.Name);
    }

    [TestMethod]
    public void Generated_CompileTimeDuplicateKeyHandlingOverridesTheRuntimeOption()
    {
        var context = new FirstWinsDuplicateKeyContext(new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.Error });

        var members = (DuplicateKeyMembers)YamlSerializer.Deserialize("Age: 1\nAge: 2\n", typeof(DuplicateKeyMembers), context)!;
        var constructed = (DuplicateKeyConstructed)YamlSerializer.Deserialize("Age: 1\nAge: 2\n", typeof(DuplicateKeyConstructed), context)!;

        Assert.AreEqual(1, members.Age);
        Assert.AreEqual(1, constructed.Age);

        var dictionary = (Dictionary<string, int>)YamlSerializer.Deserialize("a: 1\na: [x]\n", typeof(Dictionary<string, int>), context)!;
        var member = (DuplicateKeyDictionaryMembers)YamlSerializer.Deserialize("Values: {a: 1, a: [x]}\n", typeof(DuplicateKeyDictionaryMembers), context)!;
        Assert.AreEqual(1, dictionary["a"]);
        Assert.AreEqual(1, member.Values["a"]);
    }
}

internal sealed class DuplicateKeyDictionaryMembers
{
    public Dictionary<string, int> Values { get; set; } = new();
}

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
internal sealed class PopulatedDuplicateKeyDictionaries
{
    public Dictionary<string, int> ByName { get; } = new() { ["a"] = 1, ["keep"] = 7 };

    public Dictionary<int, int> ByNumber { get; } = new() { [1] = 1, [9] = 7 };

    public IDictionary<string, int> NameMap { get; } = new Dictionary<string, int> { ["a"] = 1, ["keep"] = 7 };

    public IDictionary<int, int> NumberMap { get; } = new Dictionary<int, int> { [1] = 1, [9] = 7 };

    public IDictionary<int, int> CustomMap { get; } = new Dictionary<int, int>(DuplicateKeyAbsoluteComparer.Instance) { [-1] = 1, [9] = 7 };

    public IDictionary<int, int> SortedMap { get; } = new SortedDictionary<int, int>(DuplicateKeyAbsoluteComparer.Instance) { [-1] = 1, [9] = 7 };

    public IDictionary<int, int> SortedListMap { get; } = new SortedList<int, int>(DuplicateKeyAbsoluteComparer.Instance) { [-1] = 1, [9] = 7 };

    public IDictionary<int, int> OrderedMap { get; } = new OrderedDictionary<int, int>(DuplicateKeyAbsoluteComparer.Instance) { [-1] = 1, [9] = 7 };
}

internal sealed class DuplicateKeyAbsoluteComparer : IEqualityComparer<int>, IComparer<int>
{
    public static readonly DuplicateKeyAbsoluteComparer Instance = new();

    public bool Equals(int x, int y) => Math.Abs(x) == Math.Abs(y);

    public int GetHashCode(int value) => Math.Abs(value).GetHashCode();

    public int Compare(int x, int y) => Math.Abs(x).CompareTo(Math.Abs(y));
}

internal sealed class DuplicateKeyMembers
{
    public int Age { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class DuplicateKeyConstructed
{
    public DuplicateKeyConstructed(int age)
    {
        Age = age;
    }

    public int Age { get; }

    public string Name { get; set; } = string.Empty;
}

[YamlSerializable(typeof(DuplicateKeyMembers))]
[YamlSerializable(typeof(DuplicateKeyConstructed))]
[YamlSerializable(typeof(DuplicateKeyDictionaryMembers))]
[YamlSerializable(typeof(PopulatedDuplicateKeyDictionaries))]
[YamlSerializable(typeof(Dictionary<string, int>))]
[YamlSerializable(typeof(Dictionary<int, int>))]
internal partial class DuplicateKeyContext : YamlSerializerContext
{
    public DuplicateKeyContext()
    {
    }

    public DuplicateKeyContext(YamlSerializerOptions options) : base(options)
    {
    }
}

[YamlSourceGenerationOptions(Schema = YamlSchemaKind.Failsafe, DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins)]
[YamlSerializable(typeof(DuplicateKeyMembers))]
[YamlSerializable(typeof(DuplicateKeyConstructed))]
[YamlSerializable(typeof(DuplicateKeyDictionaryMembers))]
[YamlSerializable(typeof(Dictionary<string, int>))]
internal partial class FirstWinsDuplicateKeyContext : YamlSerializerContext
{
    public FirstWinsDuplicateKeyContext()
    {
    }

    public FirstWinsDuplicateKeyContext(YamlSerializerOptions options) : base(options)
    {
    }
}
