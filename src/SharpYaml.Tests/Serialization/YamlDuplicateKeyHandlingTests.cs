#nullable enable

using System;
using System.Collections.Generic;
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

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions options, bool generated)
        => generated ? (T?)YamlSerializer.Deserialize(yaml, typeof(T), new DuplicateKeyContext(options)) : YamlSerializer.Deserialize<T>(yaml, options);

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
    }
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

[YamlSourceGenerationOptions(DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins)]
[YamlSerializable(typeof(DuplicateKeyMembers))]
[YamlSerializable(typeof(DuplicateKeyConstructed))]
internal partial class FirstWinsDuplicateKeyContext : YamlSerializerContext
{
    public FirstWinsDuplicateKeyContext()
    {
    }

    public FirstWinsDuplicateKeyContext(YamlSerializerOptions options) : base(options)
    {
    }
}
