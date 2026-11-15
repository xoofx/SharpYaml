using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// A string-keyed dictionary the reader creates tracks its explicit keys in the dictionary itself, and switches to key
/// sets when a merge key or a rejected value makes the two differ: these cover each side of that switch.
/// </summary>
[TestClass]
public sealed class YamlDictionaryKeyTrackingTests
{
    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions? options, bool generated)
    {
        if (!generated)
        {
            return YamlSerializer.Deserialize<T>(yaml, options);
        }

        var context = options is null ? DictionaryKeyTrackingContext.Default : new DictionaryKeyTrackingContext(options);
        return (T?)YamlSerializer.Deserialize(yaml, typeof(T), context);
    }

    private static YamlSerializerOptions With(YamlDuplicateKeyHandling handling, YamlInvalidValueHandling invalid = YamlInvalidValueHandling.FailFast)
        => new() { DuplicateKeyHandling = handling, InvalidValueHandling = invalid };

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void DuplicateKeyIsAnErrorByDefault(bool generated)
    {
        var exception = Assert.Throws<YamlException>(() => Deserialize<Dictionary<string, int>>("a: 1\nb: 2\na: 3\n", options: null, generated));

        StringAssert.Contains(exception.Message, "'a'");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void DuplicateKeyKeepsTheFirstOrTheLastValue(bool generated)
    {
        var first = Deserialize<Dictionary<string, int>>("a: 1\nb: 2\na: 3\n", With(YamlDuplicateKeyHandling.FirstWins), generated)!;
        var last = Deserialize<Dictionary<string, int>>("a: 1\nb: 2\na: 3\n", With(YamlDuplicateKeyHandling.LastWins), generated)!;

        Assert.AreEqual(1, first["a"]);
        Assert.AreEqual(3, last["a"]);
        Assert.HasCount(2, first);
        Assert.HasCount(2, last);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void KeysThatDifferByCaseAreDuplicatesOnlyWhenNamesIgnoreCase(bool generated)
    {
        var sensitive = Deserialize<Dictionary<string, int>>("a: 1\nA: 2\n", options: null, generated)!;
        Assert.HasCount(2, sensitive);

        var insensitive = new YamlSerializerOptions { PropertyNameCaseInsensitive = true };
        Assert.Throws<YamlException>(() => Deserialize<Dictionary<string, int>>("a: 1\nA: 2\n", insensitive, generated));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void NullValueStillMakesItsKeyADuplicate(bool generated)
    {
        Assert.Throws<YamlException>(() => Deserialize<Dictionary<string, string?>>("a: null\na: x\n", options: null, generated));

        var first = Deserialize<Dictionary<string, string?>>("a: null\na: x\n", With(YamlDuplicateKeyHandling.FirstWins), generated)!;
        Assert.IsNull(first["a"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void KeyOfARejectedValueIsStillADuplicate(bool generated)
    {
        // The rejected value is not stored, yet its key was read: a second one is a duplicate.
        var options = With(YamlDuplicateKeyHandling.Error, YamlInvalidValueHandling.Skip);

        var exception = Assert.Throws<YamlException>(() => Deserialize<Dictionary<string, int>>("a: 1\nb: oops\nb: 2\n", options, generated));

        StringAssert.Contains(exception.Message, "'b'");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void KeyOfARejectedValueKeepsTheFirstWinsRule(bool generated)
    {
        var options = With(YamlDuplicateKeyHandling.FirstWins, YamlInvalidValueHandling.Skip);

        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\nb: oops\nb: 2\nc: 3\na: 9\n", options, generated)!;

        // The first "b" was rejected and dropped; the second is a duplicate of it and is skipped.
        CollectionAssert.AreEquivalent(new[] { "a", "c" }, dictionary.Keys.ToArray());
        Assert.AreEqual(1, dictionary["a"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void KeyOfARejectedValueIsNotOverriddenByALaterMerge(bool generated)
    {
        var options = With(YamlDuplicateKeyHandling.Error, YamlInvalidValueHandling.Skip);

        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\nb: oops\n<<: {a: 7, b: 8, c: 9}\n", options, generated)!;

        Assert.AreEqual(1, dictionary["a"]);
        Assert.IsFalse(dictionary.ContainsKey("b"));
        Assert.AreEqual(9, dictionary["c"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void MergeDoesNotOverrideExplicitKeysReadBeforeOrAfterIt(bool generated)
    {
        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\n<<: {a: 7, b: 8, c: 9}\nc: 3\n", options: null, generated)!;

        Assert.AreEqual(1, dictionary["a"]);
        Assert.AreEqual(8, dictionary["b"]);
        Assert.AreEqual(3, dictionary["c"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ExplicitKeyAfterAMergeIsADuplicateOnlyOfAnExplicitKey(bool generated)
    {
        // "b" comes from the merge, so the explicit "b" is not a duplicate; the second explicit "a" is.
        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\n<<: {b: 8}\nb: 2\n", options: null, generated)!;
        Assert.AreEqual(2, dictionary["b"]);

        Assert.Throws<YamlException>(() => Deserialize<Dictionary<string, int>>("a: 1\n<<: {b: 8}\na: 2\n", options: null, generated));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void SeveralMergesKnowEveryExplicitKey(bool generated)
    {
        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\n<<: {a: 7, b: 8}\nb: 2\n<<: {a: 70, b: 80, c: 90}\n", options: null, generated)!;

        Assert.AreEqual(1, dictionary["a"]);
        Assert.AreEqual(2, dictionary["b"]);
        Assert.AreEqual(90, dictionary["c"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void EntriesOfAPopulatedDictionaryAreNotKeysOfTheMapping(bool generated)
    {
        // The dictionary of the member exists and is populated: "a" is in it, yet it was not read, so the first "a" of
        // the mapping replaces it and the second one is the duplicate.
        var options = new YamlSerializerOptions { PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate };

        var holder = Deserialize<DictionaryKeyTrackingHolder>("Map:\n  a: 5\n  c: 6\n", options, generated)!;
        Assert.AreEqual(5, holder.Map["a"]);
        Assert.AreEqual(2, holder.Map["b"]);
        Assert.AreEqual(6, holder.Map["c"]);

        Assert.Throws<YamlException>(() => Deserialize<DictionaryKeyTrackingHolder>("Map:\n  a: 5\n  a: 6\n", options, generated));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void UntypedMappingsDetectDuplicatesAndHonorMerges(bool generated)
    {
        foreach (var target in new[] { typeof(object), typeof(Dictionary<string, object>) })
        {
            object? Read(string yaml, YamlSerializerOptions? options = null)
                => generated
                    ? YamlSerializer.Deserialize(yaml, target, options is null ? DictionaryKeyTrackingContext.Default : new DictionaryKeyTrackingContext(options))
                    : YamlSerializer.Deserialize(yaml, target, options);

            Assert.Throws<YamlException>(() => Read("a: 1\nb: 2\na: 3\n"), target.Name);

            var first = (IDictionary<string, object?>)Read("a: 1\nb: 2\na: 3\n", With(YamlDuplicateKeyHandling.FirstWins))!;
            Assert.AreEqual(1, Convert.ToInt32(first["a"], System.Globalization.CultureInfo.InvariantCulture), target.Name);

            var last = (IDictionary<string, object?>)Read("a: 1\nb: 2\na: 3\n", With(YamlDuplicateKeyHandling.LastWins))!;
            Assert.AreEqual(3, Convert.ToInt32(last["a"], System.Globalization.CultureInfo.InvariantCulture), target.Name);

            var merged = (IDictionary<string, object?>)Read("a: 1\n<<: {a: 7, b: 8, c: 9}\nc: 3\n")!;
            Assert.AreEqual(1, Convert.ToInt32(merged["a"], System.Globalization.CultureInfo.InvariantCulture), target.Name);
            Assert.AreEqual(8, Convert.ToInt32(merged["b"], System.Globalization.CultureInfo.InvariantCulture), target.Name);
            Assert.AreEqual(3, Convert.ToInt32(merged["c"], System.Globalization.CultureInfo.InvariantCulture), target.Name);

            // "b" comes from the merge, so the explicit "b" is not a duplicate; the second explicit "a" is.
            var overridden = (IDictionary<string, object?>)Read("a: 1\n<<: {b: 8}\nb: 2\n")!;
            Assert.AreEqual(2, Convert.ToInt32(overridden["b"], System.Globalization.CultureInfo.InvariantCulture), target.Name);
            Assert.Throws<YamlException>(() => Read("a: 1\n<<: {b: 8}\na: 2\n"), target.Name);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void UntypedMappingWithAComplexKeyStillDetectsScalarDuplicates(bool generated)
    {
        object? Read(string yaml)
            => generated ? YamlSerializer.Deserialize(yaml, typeof(object), DictionaryKeyTrackingContext.Default) : YamlSerializer.Deserialize<object>(yaml);

        // The complex key replaces the dictionary of the mapping: the scalar keys read before and after it are still known.
        var mapping = (System.Collections.IDictionary)Read("a: 1\n? [x, y]\n: 2\nb: 3\n")!;
        Assert.AreEqual(3, mapping.Count);

        Assert.Throws<YamlException>(() => Read("a: 1\n? [x, y]\n: 2\na: 3\n"));
        Assert.Throws<YamlException>(() => Read("a: 1\n? [x, y]\n: 2\nb: 3\nb: 4\n"));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void ManyDictionariesKeepTheirOwnKeys(bool generated)
    {
        var yaml = string.Concat(Enumerable.Range(0, 50).Select(i => $"- k{i}: {i}\n  shared: {i * 2}\n"));

        var list = Deserialize<List<Dictionary<string, int>>>(yaml, options: null, generated)!;

        Assert.HasCount(50, list);
        for (var i = 0; i < list.Count; i++)
        {
            Assert.HasCount(2, list[i]);
            Assert.AreEqual(i, list[i]["k" + i]);
            Assert.AreEqual(i * 2, list[i]["shared"]);
        }
    }
}

public sealed class DictionaryKeyTrackingHolder
{
    public Dictionary<string, int> Map { get; set; } = new() { ["a"] = 1, ["b"] = 2 };
}

[YamlSerializable(typeof(Dictionary<string, int>))]
[YamlSerializable(typeof(Dictionary<string, string>))]
[YamlSerializable(typeof(List<Dictionary<string, int>>))]
[YamlSerializable(typeof(DictionaryKeyTrackingHolder))]
[YamlSerializable(typeof(object))]
[YamlSerializable(typeof(Dictionary<string, object>))]
internal partial class DictionaryKeyTrackingContext : YamlSerializerContext
{
    public DictionaryKeyTrackingContext()
    {
    }

    public DictionaryKeyTrackingContext(YamlSerializerOptions options) : base(options)
    {
    }
}
