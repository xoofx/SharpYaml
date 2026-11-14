using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlInvalidValueTests
{
    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    public static IEnumerable<object[]> CollectionKinds
    {
        get
        {
            var kinds = new (string Member, Type DeclaringType, bool HasInstance)[]
            {
                ("Codes", typeof(int[]), false),
                ("Unique", typeof(HashSet<int>), true),
                ("Distinct", typeof(ISet<int>), true),
                ("Ordered", typeof(IList<int>), true),
                ("Bag", typeof(ICollection<int>), true),
                ("ReadOnlyList", typeof(IReadOnlyList<int>), true),
                ("ReadOnlyBag", typeof(IReadOnlyCollection<int>), true),
                ("Items", typeof(IEnumerable<int>), true),
                ("Frozen", typeof(ImmutableArray<int>), false),
                ("FrozenList", typeof(ImmutableList<int>), false),
                ("FrozenSet", typeof(ImmutableHashSet<int>), false),
                ("Custom", typeof(Collection<int>), true),
            };

            foreach (var (member, declaringType, hasInstance) in kinds)
            {
                yield return new object[] { member, declaringType, hasInstance, false };
                yield return new object[] { member, declaringType, hasInstance, true };
            }
        }
    }

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions options, bool generated)
        => generated ? (T?)YamlSerializer.Deserialize(yaml, typeof(T), new InvalidValueContext(options)) : YamlSerializer.Deserialize<T>(yaml, options);

    private static T? Deserialize<T>(TextReader reader, YamlSerializerOptions options, bool generated)
        => generated ? (T?)YamlSerializer.Deserialize(reader, typeof(T), new InvalidValueContext(options)) : YamlSerializer.Deserialize<T>(reader, options);

    private static bool TryDeserialize<T>(string yaml, YamlSerializerOptions options, bool generated, out T? value)
        => generated ? YamlSerializer.TryDeserialize(yaml, new InvalidValueContext(options), out value) : YamlSerializer.TryDeserialize(yaml, out value, options);

    [TestMethod]
    public void InvalidValueHandling_DefaultsToFailFast()
    {
        Assert.AreEqual(YamlInvalidValueHandling.FailFast, new YamlSerializerOptions().InvalidValueHandling);
        Assert.AreEqual(YamlInvalidValueHandling.FailFast, YamlSerializerOptions.Default.InvalidValueHandling);
    }

    [TestMethod]
    public void CreateException_IsLocatedAtTheCurrentToken()
    {
        var reader = YamlReader.Create("a: 1\n", new YamlSerializerOptions { SourceName = "config.yaml" });
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        Assert.AreEqual("1", reader.GetScalarValue());

        var exception = reader.CreateException("not a port");

        Assert.AreEqual("config.yaml", exception.SourceName);
        Assert.AreEqual(reader.Start, exception.Start);
        Assert.AreEqual(reader.End, exception.End);
        Assert.AreEqual(0, exception.Start.Line);
        Assert.AreEqual(3, exception.Start.Column);
        StringAssert.EndsWith(exception.Message, "not a port");
    }

    [TestMethod]
    public void Session_ExistsOnlyWhenAReportIsConsumed()
    {
        static void Callback(YamlInvalidValue value)
        {
        }

        Assert.IsNull(YamlReportingSession.Create(new YamlSerializerOptions()));
        Assert.IsNull(YamlReportingSession.Create(new YamlSerializerOptions { InvalidValueCallback = Callback }));
        Assert.IsNull(YamlReportingSession.Create(new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip }));
        Assert.IsNotNull(YamlReportingSession.Create(new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd }));
        Assert.IsNotNull(YamlReportingSession.Create(new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = Callback }));
    }

    [TestMethod]
    public void IsReportingUnmappedMembers_StaysFalseWithOnlyInvalidValueReporting()
    {
        var failAtEnd = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };
        var skip = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValuesFinalizer = static _ => { } };

        Assert.IsFalse(YamlReader.Create("a: 1\n", failAtEnd).IsReportingUnmappedMembers);
        Assert.IsFalse(YamlReader.Create("a: 1\n", skip).IsReportingUnmappedMembers);
    }

    [TestMethod]
    public void TryReadItem_ReportsASequenceElementByItsIndex()
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };
        var reader = YamlReader.Create("[1, abc]\n", options);
        Assert.IsTrue(reader.Read());
        Assert.IsTrue(reader.Read());
        var converter = reader.GetConverter(typeof(int));

        Assert.IsTrue(reader.TryReadItem(converter, typeof(int), typeof(List<int>), null, out var first));
        Assert.IsFalse(reader.TryReadItem(converter, typeof(int), typeof(List<int>), null, out var second));

        Assert.AreEqual(1, first);
        Assert.IsNull(second);
        Assert.AreEqual(YamlTokenType.EndSequence, reader.TokenType);
        Assert.HasCount(1, values);
        Assert.AreEqual("$[1]", values[0].Path);
        Assert.AreEqual("1", values[0].Name);
        Assert.AreEqual(4, values[0].ValueStart.Column);
        Assert.AreEqual(values[0].ValueStart.Index, values[0].KeyStart.Index);
        Assert.IsNull(values[0].Instance);
        Assert.AreEqual(typeof(List<int>), values[0].DeclaringType);
    }

    [TestMethod]
    public void Exception_ListsEveryValueAndIsLocatedAtTheFirst()
    {
        var first = CreateValue("$.port", new Mark(8, 2, 7), new Mark(11, 2, 10), "'abc' is not a port");
        var second = CreateValue("$.servers[1].timeout", new Mark(40, 5, 12), new Mark(42, 5, 14), "'-1' is negative");

        var exception = new YamlInvalidValuesException(new[] { first, second });

        Assert.HasCount(2, exception.InvalidValues);
        Assert.AreEqual("config.yaml", exception.SourceName);
        Assert.AreEqual(first.Exception.Start, exception.Start);
        Assert.AreEqual(first.Exception.End, exception.End);
        Assert.IsNull(exception.InnerException);
        StringAssert.Contains(exception.Message, "2 invalid values were skipped while deserializing:");
        StringAssert.Contains(exception.Message, "\n  $.port: " + first.Message);
        StringAssert.Contains(exception.Message, "\n  $.servers[1].timeout: " + second.Message);
    }

    [TestMethod]
    public void Exception_NamesTheErrorThatEndedTheDeserialization()
    {
        var value = CreateValue("$.port", new Mark(8, 2, 7), new Mark(11, 2, 10), "'abc' is not a port");
        var fatal = new YamlException("Required member 'Name' is missing.");

        var exception = new YamlInvalidValuesException(new[] { value }, fatal);

        Assert.AreSame(fatal, exception.InnerException);
        StringAssert.Contains(exception.Message, "1 invalid value was skipped before the deserialization failed:");
        StringAssert.EndsWith(exception.Message, "\n  " + fatal.Message);
    }

    [TestMethod]
    public void Exception_RequiresAtLeastOneValue()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new YamlInvalidValuesException((IReadOnlyList<YamlInvalidValue>)null!));
        Assert.ThrowsExactly<ArgumentException>(() => new YamlInvalidValuesException(Array.Empty<YamlInvalidValue>()));
        Assert.IsEmpty(new YamlInvalidValuesException().InvalidValues);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailFast_ThrowsAtTheFirstInvalidValueAndRunsNoDelegate(bool generated)
    {
        var reported = 0;
        var options = new YamlSerializerOptions { InvalidValueCallback = _ => reported++, InvalidValuesFinalizer = _ => reported++ };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Port: abc\nTimeout: xyz\n", options, generated));

        StringAssert.Contains(exception.Message, "Invalid integer scalar 'abc'");
        Assert.AreEqual(0, exception.Start.Line);
        Assert.AreEqual(0, reported);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsTheInitializerValueAndReportsEveryDetail(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { SourceName = "config.yaml", InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Name: a\nPort: abc\nTimeout: 5\n", options, generated)!;

        Assert.AreEqual("a", root.Name);
        Assert.AreEqual(8080, root.Port);
        Assert.AreEqual(5, root.Timeout);
        Assert.HasCount(1, values);

        var value = values[0];
        Assert.AreEqual("config.yaml", value.SourceName);
        Assert.AreEqual("$.Port", value.Path);
        Assert.AreEqual("Port", value.Name);
        Assert.AreEqual(1, value.KeyStart.Line);
        Assert.AreEqual(0, value.KeyStart.Column);
        Assert.AreEqual(1, value.ValueStart.Line);
        Assert.AreEqual(6, value.ValueStart.Column);
        Assert.AreEqual(typeof(int), value.TargetType);
        Assert.AreEqual(typeof(InvalidValueRoot), value.DeclaringType);
        Assert.AreSame(root, value.Instance);
        Assert.AreEqual("abc", ((YamlValue)value.Value!).Value);
        StringAssert.Contains(value.Message, "Invalid integer scalar 'abc'");
        Assert.AreEqual(value.ValueStart.Index, value.Exception.Start.Index);
        Assert.AreEqual(value.Exception.Reason, value.Message);
        Assert.IsFalse(value.Message.Contains("Lin:"), value.Message);
        StringAssert.EndsWith(value.Exception.Message, ": " + value.Message);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_WithoutDelegatesSucceedsSilently(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip };

        var root = Deserialize<InvalidValueRoot>("Port: abc\nName: a\n", options, generated)!;

        Assert.AreEqual(8080, root.Port);
        Assert.AreEqual("a", root.Name);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_RecoversAConverterThatRejectsBeforeConsuming(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Rejecting: nope\nName: a\n", options, generated)!;

        Assert.IsNull(root.Rejecting);
        Assert.AreEqual("a", root.Name);
        Assert.HasCount(1, values);
        Assert.AreEqual(typeof(InvalidValueRejecting), values[0].TargetType);
        StringAssert.EndsWith(values[0].Message, "rejected before consuming");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_LeavesAConverterThatThrowsAfterConsumingFatal(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Consuming: nope\nName: a\n", options, generated));

        StringAssert.EndsWith(exception.Message, "rejected after consuming");
        Assert.IsEmpty(values);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_LeavesANonYamlExceptionFatalAndWrapped(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Formatted: nope\n", options, generated));

        Assert.IsInstanceOfType<FormatException>(exception.InnerException);
        Assert.IsEmpty(values);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_SkipsAMappingWhereAScalarIsExpectedWhole(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Port:\n  nested: 1\n  more: [1, 2]\nName: after\n", options, generated)!;

        Assert.AreEqual(8080, root.Port);
        Assert.AreEqual("after", root.Name);
        Assert.HasCount(1, values);
        Assert.IsInstanceOfType<YamlMapping>(values[0].Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_CountsARequiredMemberWithAnInvalidValueAsPresent(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip };

        var root = Deserialize<InvalidValueRequired>("Port: abc\n", options, generated)!;

        Assert.AreEqual(0, root.Port);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsTheDefaultOfAConstructorParameter(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueConstructed>("Port: abc\nName: a\nTimeout: xyz\n", options, generated)!;

        Assert.AreEqual(8080, root.Port);
        Assert.AreEqual("a", root.Name);
        Assert.AreEqual(30, root.Timeout);
        Assert.HasCount(2, values);
        Assert.AreEqual("$.Port", values[0].Path);
        Assert.IsNull(values[0].Instance);
        Assert.AreEqual(typeof(InvalidValueConstructed), values[0].DeclaringType);
        Assert.AreEqual("$.Timeout", values[1].Path);
        Assert.IsNull(values[1].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_RecoversInsideAMergedMapping(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueMergeRoot>("Server:\n  <<: {Port: abc, Timeout: 7}\n  Name: a\nConstructed:\n  <<: {Port: abc, Timeout: xyz}\n  Name: c\n", options, generated)!;

        Assert.AreEqual(8080, root.Server!.Port);
        Assert.AreEqual(7, root.Server.Timeout);
        Assert.AreEqual("a", root.Server.Name);
        Assert.AreEqual("c", root.Constructed!.Name);
        Assert.AreEqual(8080, root.Constructed.Port);
        Assert.AreEqual(30, root.Constructed.Timeout);
        Assert.HasCount(3, values);
        Assert.AreEqual("$.Server['<<'].Port", values[0].Path);
        Assert.AreSame(root.Server, values[0].Instance);
        Assert.AreEqual("$.Constructed['<<'].Port", values[1].Path);
        Assert.AreEqual("$.Constructed['<<'].Timeout", values[2].Path);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsThePopulatedInstanceWhenTheReadFailsBeforeConsuming(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValuePopulateRoot>("Server: abc\n", options, generated)!;

        Assert.AreEqual("keep", root.Server.Name);
        Assert.HasCount(1, values);
        Assert.AreEqual(typeof(InvalidValueRoot), values[0].TargetType);
        Assert.AreEqual("abc", ((YamlValue)values[0].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_ReportsTheCompletePathInsideAPolymorphicNode(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var zoo = Deserialize<InvalidValueZoo>("Pets:\n  - $type: dog\n    Name: a\n    BarkVolume: loud\n", options, generated)!;

        var dog = (InvalidValueDog)zoo.Pets[0];
        Assert.AreEqual("a", dog.Name);
        Assert.AreEqual(3, dog.BarkVolume);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.Pets[0].BarkVolume", values[0].Path);
        Assert.AreEqual(typeof(InvalidValueDog), values[0].DeclaringType);
        Assert.AreSame(dog, values[0].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_RecoversAnAnchoredValueAndItsAlias(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Port: &p abc\nTimeout: *p\n", options, generated)!;

        Assert.AreEqual(8080, root.Port);
        Assert.AreEqual(30, root.Timeout);
        Assert.HasCount(2, values);
        Assert.AreEqual("abc", ((YamlValue)values[0].Value!).Value);
        Assert.AreEqual("abc", ((YamlValue)values[1].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailFast_LeavesAnInvalidElementFatal(bool generated)
    {
        var reported = 0;
        var options = new YamlSerializerOptions { InvalidValueCallback = _ => reported++, InvalidValuesFinalizer = _ => reported++ };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Retries: [1, abc]\n", options, generated));

        StringAssert.Contains(exception.Message, "Invalid integer scalar 'abc'");
        Assert.AreEqual(13, exception.Start.Column);
        Assert.AreEqual(0, reported);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAnInvalidListElementAndReportsEveryDetail(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { SourceName = "config.yaml", InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Retries: [1, abc, 3]\n", options, generated)!;

        CollectionAssert.AreEqual(new[] { 1, 3 }, root.Retries);
        Assert.HasCount(1, values);

        var value = values[0];
        Assert.AreEqual("config.yaml", value.SourceName);
        Assert.AreEqual("$.Retries[1]", value.Path);
        Assert.AreEqual("1", value.Name);
        Assert.AreEqual(0, value.ValueStart.Line);
        Assert.AreEqual(13, value.ValueStart.Column);
        Assert.AreEqual(value.ValueStart.Index, value.KeyStart.Index);
        Assert.AreEqual(typeof(int), value.TargetType);
        Assert.AreEqual(typeof(List<int>), value.DeclaringType);
        Assert.AreSame(root.Retries, value.Instance);
        Assert.AreEqual("abc", ((YamlValue)value.Value!).Value);
        StringAssert.Contains(value.Message, "Invalid integer scalar 'abc'");
        Assert.AreEqual(value.ValueStart.Index, value.Exception.Start.Index);
    }

    [TestMethod]
    [DynamicData(nameof(CollectionKinds))]
    public void Skip_DropsAnInvalidElementOfEveryCollectionKind(string member, Type declaringType, bool hasInstance, bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueCollections>($"{member}: [1, abc, 3]\n", options, generated)!;

        var items = (IEnumerable<int>)typeof(InvalidValueCollections).GetProperty(member)!.GetValue(root)!;
        CollectionAssert.AreEquivalent(new[] { 1, 3 }, items.ToArray());
        Assert.HasCount(1, values);
        Assert.AreEqual($"$.{member}[1]", values[0].Path);
        Assert.AreEqual("1", values[0].Name);
        Assert.AreEqual(declaringType, values[0].DeclaringType);
        Assert.AreEqual(typeof(int), values[0].TargetType);
        if (hasInstance)
        {
            Assert.AreSame(items, values[0].Instance);
        }
        else
        {
            Assert.IsNull(values[0].Instance);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAnInvalidElementOfANestedList(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueCollections>("Matrix: [[1, abc], [3]]\n", options, generated)!;

        Assert.HasCount(2, root.Matrix);
        CollectionAssert.AreEqual(new[] { 1 }, root.Matrix[0]);
        CollectionAssert.AreEqual(new[] { 3 }, root.Matrix[1]);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.Matrix[0][1]", values[0].Path);
        Assert.AreEqual(typeof(List<int>), values[0].DeclaringType);
        Assert.AreSame(root.Matrix[0], values[0].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAnInvalidElementOfARootList(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var list = Deserialize<List<int>>("[1, abc]\n", options, generated)!;

        CollectionAssert.AreEqual(new[] { 1 }, list);
        Assert.HasCount(1, values);
        Assert.AreEqual("$[1]", values[0].Path);
        Assert.AreEqual("1", values[0].Name);
        Assert.AreEqual(4, values[0].ValueStart.Column);
        Assert.AreEqual(4, values[0].KeyStart.Column);
        Assert.AreEqual(typeof(List<int>), values[0].DeclaringType);
        Assert.AreSame(list, values[0].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_SkipsAMappingElementWhereAScalarIsExpectedWhole(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Retries:\n  - 1\n  - nested: 1\n    more: [1, 2]\n  - 3\n", options, generated)!;

        CollectionAssert.AreEqual(new[] { 1, 3 }, root.Retries);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.Retries[1]", values[0].Path);
        Assert.IsInstanceOfType<YamlMapping>(values[0].Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_RecoversAnElementAConverterRejectsBeforeConsuming(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueCollections>("Rejected: [nope, nope]\n", options, generated)!;

        Assert.IsEmpty(root.Rejected);
        Assert.HasCount(2, values);
        Assert.AreEqual("$.Rejected[0]", values[0].Path);
        Assert.AreEqual("$.Rejected[1]", values[1].Path);
        Assert.AreEqual(typeof(InvalidValueRejecting), values[0].TargetType);
        StringAssert.EndsWith(values[0].Message, "rejected before consuming");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_LeavesAnElementConverterThatThrowsAfterConsumingFatal(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueCollections>("Consumed: [nope]\n", options, generated));

        StringAssert.EndsWith(exception.Message, "rejected after consuming");
        Assert.IsEmpty(values);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_RecoversAnAnchoredElementAndItsAlias(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueRoot>("Retries: [&r abc, *r, 2]\n", options, generated)!;

        CollectionAssert.AreEqual(new[] { 2 }, root.Retries);
        Assert.HasCount(2, values);
        Assert.AreEqual("$.Retries[0]", values[0].Path);
        Assert.AreEqual("$.Retries[1]", values[1].Path);
        Assert.AreEqual("abc", ((YamlValue)values[0].Value!).Value);
        Assert.AreEqual("abc", ((YamlValue)values[1].Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsTheExistingItemsOfPopulatedCollections(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValuePopulateRoot>("Retries: [1, abc]\nPorts: {b: abc, c: 2}\n", options, generated)!;

        CollectionAssert.AreEqual(new[] { 7, 1 }, root.Retries);
        Assert.AreEqual(2, root.Ports.Count);
        Assert.AreEqual(1, root.Ports["keep"]);
        Assert.AreEqual(2, root.Ports["c"]);
        Assert.HasCount(2, values);
        Assert.AreSame(root.Retries, values[0].Instance);
        Assert.AreEqual(typeof(List<int>), values[0].DeclaringType);
        Assert.AreSame(root.Ports, values[1].Instance);
        Assert.AreEqual(typeof(Dictionary<string, int>), values[1].DeclaringType);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_WithoutDelegatesDropsAnElementSilently(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip };

        var root = Deserialize<InvalidValueRoot>("Retries: [1, abc, 3]\n", options, generated)!;

        CollectionAssert.AreEqual(new[] { 1, 3 }, root.Retries);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_ListsItemsWithMembersInDocumentOrder(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };

        var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueCollections>("Port: abc\nRetries: [1, xyz, 3]\nPorts: {a: 1, b: q}\n", options, generated));

        Assert.IsNull(exception.InnerException);
        CollectionAssert.AreEqual(new[] { "$.Port", "$.Retries[1]", "$.Ports.b" }, exception.InvalidValues.Select(static v => v.Path).ToArray());
        Assert.IsTrue(exception.InvalidValues.All(static v => v.Value is null));
        StringAssert.Contains(exception.Message, "$.Retries[1]: ");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAnInvalidDictionaryEntryAndReportsEveryDetail(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueCollections>("Ports:\n  a: 1\n  b: abc\n  c: 3\n", options, generated)!;

        Assert.AreEqual(2, root.Ports.Count);
        Assert.AreEqual(1, root.Ports["a"]);
        Assert.AreEqual(3, root.Ports["c"]);
        Assert.HasCount(1, values);

        var value = values[0];
        Assert.AreEqual("$.Ports.b", value.Path);
        Assert.AreEqual("b", value.Name);
        Assert.AreEqual(2, value.KeyStart.Line);
        Assert.AreEqual(2, value.KeyStart.Column);
        Assert.AreEqual(2, value.ValueStart.Line);
        Assert.AreEqual(5, value.ValueStart.Column);
        Assert.AreEqual(typeof(int), value.TargetType);
        Assert.AreEqual(typeof(Dictionary<string, int>), value.DeclaringType);
        Assert.AreSame(root.Ports, value.Instance);
        Assert.AreEqual("abc", ((YamlValue)value.Value!).Value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAnInvalidEntryOfATypedKeyDictionary(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueCollections>("ByNumber: {1: abc, 2: 2}\n", options, generated)!;

        Assert.AreEqual(1, root.ByNumber.Count);
        Assert.AreEqual(2, root.ByNumber[2]);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.ByNumber.1", values[0].Path);
        Assert.AreEqual("1", values[0].Name);
        Assert.AreEqual(11, values[0].KeyStart.Column);
        Assert.AreEqual(14, values[0].ValueStart.Column);
        Assert.AreEqual(typeof(Dictionary<int, int>), values[0].DeclaringType);
        Assert.AreEqual(typeof(int), values[0].TargetType);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_LeavesAnInvalidDictionaryKeyFatal(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueCollections>("ByNumber: {abc: 1}\n", options, generated));

        StringAssert.Contains(exception.Message, "abc");
        Assert.IsEmpty(values);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_StillTreatsADroppedDictionaryKeyAsSeenForDuplicateDetection(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var droppedFirst = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueCollections>("Ports: {b: abc, b: 1}\n", options, generated));
        var droppedSecond = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueCollections>("Ports: {b: 1, b: abc}\n", options, generated));
        var typedKey = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueCollections>("ByNumber: {1: abc, 1: 2}\n", options, generated));

        Assert.AreEqual("Duplicate mapping key 'b'.", droppedFirst.Reason);
        Assert.AreEqual("Duplicate mapping key 'b'.", droppedSecond.Reason);
        Assert.AreEqual("Duplicate mapping key '1'.", typedKey.Reason);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_RecoversInsideAMergedDictionary(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var root = Deserialize<InvalidValueCollections>("Ports:\n  <<: {a: abc, b: 2}\n  c: 3\n", options, generated)!;

        Assert.AreEqual(2, root.Ports.Count);
        Assert.AreEqual(2, root.Ports["b"]);
        Assert.AreEqual(3, root.Ports["c"]);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.Ports['<<'].a", values[0].Path);
        Assert.AreEqual("a", values[0].Name);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsADroppedEntryShadowingItsMergedValue(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip };

        var root = Deserialize<InvalidValueCollections>("Ports: {a: abc, <<: {a: 1, b: 2}}\n", options, generated)!;

        Assert.AreEqual(1, root.Ports.Count);
        Assert.AreEqual(2, root.Ports["b"]);
    }

    [TestMethod]
    [DataRow(false, YamlDuplicateKeyHandling.Error)]
    [DataRow(true, YamlDuplicateKeyHandling.Error)]
    [DataRow(false, YamlDuplicateKeyHandling.FirstWins)]
    [DataRow(true, YamlDuplicateKeyHandling.FirstWins)]
    [DataRow(false, YamlDuplicateKeyHandling.LastWins)]
    [DataRow(true, YamlDuplicateKeyHandling.LastWins)]
    public void Skip_DropsAnInvalidExplicitEntryAfterItsMerge(bool generated, YamlDuplicateKeyHandling duplicateHandling)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, DuplicateKeyHandling = duplicateHandling };

        var root = Deserialize<InvalidValueCollections>("Ports: {<<: {a: 1, b: 2}, a: abc}\n", options, generated)!;

        Assert.AreEqual(1, root.Ports.Count);
        Assert.AreEqual(2, root.Ports["b"]);

        var dictionary = Deserialize<Dictionary<string, int>>("<<: {a: 1, b: 2}\na: abc\n", options, generated)!;
        Assert.AreEqual(1, dictionary.Count);
        Assert.AreEqual(2, dictionary["b"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DroppedPopulatedDictionaryKeysUseTheDictionaryComparer(bool generated)
    {
        var options = new YamlSerializerOptions
        {
            InvalidValueHandling = YamlInvalidValueHandling.Skip,
            DuplicateKeyHandling = YamlDuplicateKeyHandling.Error,
        };

        foreach (var member in new[] { "ByNumber", "NumberMap", "SortedMap", "SortedListMap", "OrderedMap" })
        {
            var yaml = member + ": {1: abc, -1: 2}\n";
            var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValuePopulateRoot>(yaml, options, generated), member);

            Assert.AreEqual("Duplicate mapping key '-1'.", exception.Reason);

            var root = Deserialize<InvalidValuePopulateRoot>(yaml, options with { DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins }, generated)!;
            var dictionary = (IDictionary<int, int>)typeof(InvalidValuePopulateRoot).GetProperty(member)!.GetValue(root)!;
            Assert.IsEmpty(dictionary, member);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsAnEarlierExplicitDictionaryValueUnderLastWins(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };

        var root = Deserialize<InvalidValueCollections>("Ports: {a: 1, a: abc}\nByNumber: {1: 1, 1: abc}\n", options, generated)!;

        Assert.AreEqual(1, root.Ports["a"]);
        Assert.AreEqual(1, root.ByNumber[1]);

        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\na: abc\n", options, generated)!;
        Assert.AreEqual(1, dictionary["a"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsAValidExplicitValueAfterOverridingAMerge(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins, PropertyNameCaseInsensitive = true };

        var root = Deserialize<InvalidValueCollections>("Ports: {<<: [{A: 1}], a: 2, A: abc}\n", options, generated)!;

        Assert.AreEqual(1, root.Ports.Count);
        Assert.AreEqual(2, root.Ports["a"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAMergedObjectDictionaryEntryInEveryReadPath(bool generated)
    {
        var options = new YamlSerializerOptions
        {
            InvalidValueHandling = YamlInvalidValueHandling.Skip,
            Converters = [new InvalidValueObjectIntConverter()],
        };

        var dictionary = Deserialize<Dictionary<string, object>>("<<: [{a: 1, b: 2}]\na: abc\n", options, generated)!;
        Assert.AreEqual(1, dictionary.Count);
        Assert.AreEqual(2, dictionary["b"]);

        var root = Deserialize<InvalidValueCollections>("ObjectPorts: {<<: {a: 1, b: 2}, a: abc}\n", options, generated)!;
        Assert.AreEqual(1, root.ObjectPorts.Count);
        Assert.AreEqual(2, root.ObjectPorts["b"]);

        var populated = Deserialize<InvalidValuePopulateRoot>("ObjectPorts: {<<: {a: 1, b: 2}, a: abc}\n", options, generated)!;
        Assert.AreEqual(2, populated.ObjectPorts.Count);
        Assert.AreEqual(2, populated.ObjectPorts["b"]);
        Assert.AreEqual(3, populated.ObjectPorts["keep"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_KeepsAnExistingEntryWhenPopulatingADictionary(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };

        var root = Deserialize<InvalidValuePopulateRoot>("Ports: {keep: abc, valid: 2}\n", options, generated)!;

        Assert.AreEqual(2, root.Ports.Count);
        Assert.AreEqual(1, root.Ports["keep"]);
        Assert.AreEqual(2, root.Ports["valid"]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_DropsAnInvalidEntryOfARootDictionary(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var dictionary = Deserialize<Dictionary<string, int>>("a: 1\nb: abc\n", options, generated)!;

        Assert.AreEqual(1, dictionary.Count);
        Assert.AreEqual(1, dictionary["a"]);
        Assert.HasCount(1, values);
        Assert.AreEqual("$.b", values[0].Path);
        Assert.AreEqual(1, values[0].KeyStart.Line);
        Assert.AreEqual(0, values[0].KeyStart.Column);
        Assert.AreEqual(3, values[0].ValueStart.Column);
        Assert.AreSame(dictionary, values[0].Instance);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_StillDisallowsUnmappedMembers(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Port: abc\nOops: 1\n", options, generated));

        StringAssert.Contains(exception.Message, "Oops");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_StillTreatsASkippedKeyAsPresentForDuplicateDetection(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, DuplicateKeyHandling = YamlDuplicateKeyHandling.Error };

        var member = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Port: abc\nPort: 1\n", options, generated));
        var parameter = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueConstructed>("Name: a\nPort: abc\nPort: 1\n", options, generated));

        StringAssert.Contains(member.Message, "Port");
        StringAssert.Contains(parameter.Message, "Port");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_ThrowsOnceListingEveryValueInDocumentOrder(bool generated)
    {
        var options = new YamlSerializerOptions { SourceName = "config.yaml", InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };
        const string yaml = "Port: abc\nName: a\nTimeout: xyz\nRejecting: nope\n";

        var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>(yaml, options, generated));

        Assert.IsNull(exception.InnerException);
        Assert.HasCount(3, exception.InvalidValues);
        CollectionAssert.AreEqual(new[] { "$.Port", "$.Timeout", "$.Rejecting" }, exception.InvalidValues.Select(static v => v.Path).ToArray());
        Assert.IsTrue(exception.InvalidValues.All(static v => v.Value is null));
        Assert.AreEqual("config.yaml", exception.SourceName);
        Assert.AreEqual(0, exception.Start.Line);
        Assert.AreEqual(6, exception.Start.Column);
        StringAssert.Contains(exception.Message, "3 invalid values were skipped while deserializing:");
        StringAssert.Contains(exception.Message, "$.Rejecting: ");
        Assert.IsFalse(TryDeserialize<InvalidValueRoot>(yaml, options, generated, out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_ThrowsFromTheTextReaderOverloadToo(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };

        using var reader = new StringReader("Port: abc\n");
        var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>(reader, options, generated));

        Assert.HasCount(1, exception.InvalidValues);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_FoldsAFatalErrorIntoTheAggregate(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };

        var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>("Port: abc\nTimeout: xyz\nConsuming: nope\n", options, generated));

        Assert.HasCount(2, exception.InvalidValues);
        Assert.IsInstanceOfType<YamlException>(exception.InnerException);
        StringAssert.EndsWith(exception.InnerException!.Message, "rejected after consuming");
        StringAssert.Contains(exception.Message, "2 invalid values were skipped before the deserialization failed:");
        StringAssert.EndsWith(exception.Message, "rejected after consuming");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_WrapsANonYamlFatalErrorBeforeFoldingIt(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };

        var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>("Port: abc\nFormatted: nope\n", options, generated));

        Assert.HasCount(1, exception.InvalidValues);
        Assert.IsInstanceOfType<FormatException>(exception.InnerException!.InnerException);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_FoldsAMissingConstructorParameterAfterItsValueWasSkipped(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };

        var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueConstructed>("Name: [1]\n", options, generated));

        Assert.HasCount(1, exception.InvalidValues);
        StringAssert.Contains(exception.InnerException!.Message, "Missing required constructor parameter 'name'");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void FailAtEnd_PropagatesAFatalErrorUnchangedWhenNothingWasRecovered(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Consuming: nope\n", options, generated));

        StringAssert.EndsWith(exception.Message, "rejected after consuming");
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_PropagatesAFatalErrorUnchangedAfterRecoveries(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var exception = Assert.ThrowsExactly<YamlException>(() => Deserialize<InvalidValueRoot>("Port: abc\nConsuming: nope\n", options, generated));

        StringAssert.EndsWith(exception.Message, "rejected after consuming");
        Assert.HasCount(1, values);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_SucceedsUnderTryDeserialize(bool generated)
    {
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip };

        Assert.IsTrue(TryDeserialize<InvalidValueRoot>("Port: abc\n", options, generated, out var value));
        Assert.AreEqual(8080, value!.Port);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Finalizer_RunsOnceWithEveryValueUnderSkipAndBeforeTheFailAtEndThrow(bool generated)
    {
        var calls = new List<string[]>();
        var skip = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValuesFinalizer = values => calls.Add(values.Select(static v => v.Path).ToArray()) };
        var failAtEnd = skip with { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd };
        const string yaml = "Port: abc\nTimeout: xyz\n";

        Deserialize<InvalidValueRoot>(yaml, skip, generated);
        Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>(yaml, failAtEnd, generated));

        Assert.HasCount(2, calls);
        CollectionAssert.AreEqual(new[] { "$.Port", "$.Timeout" }, calls[0]);
        CollectionAssert.AreEqual(new[] { "$.Port", "$.Timeout" }, calls[1]);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Finalizer_DoesNotRunWithoutInvalidValuesOrAfterAFatalError(bool generated)
    {
        var called = false;
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd, InvalidValuesFinalizer = _ => called = true };

        Deserialize<InvalidValueRoot>("Port: 1\n", options, generated);
        Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>("Port: abc\nConsuming: nope\n", options, generated));

        Assert.IsFalse(called);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Finalizer_CanReplaceTheAggregate(bool generated)
    {
        var options = new YamlSerializerOptions
        {
            InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd,
            InvalidValuesFinalizer = values => throw new InvalidOperationException($"{values.Count} invalid"),
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => Deserialize<InvalidValueRoot>("Port: abc\n", options, generated));

        Assert.AreEqual("1 invalid", exception.Message);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Handlers_AreSafeToShareAcrossConcurrentDeserializations(bool generated)
    {
        var total = 0;
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd, InvalidValueCallback = _ => Interlocked.Increment(ref total) };

        Parallel.For(0, 200, _ =>
        {
            var exception = Assert.ThrowsExactly<YamlInvalidValuesException>(() => Deserialize<InvalidValueRoot>("Port: abc\nTimeout: xyz\n", options, generated));
            Assert.HasCount(2, exception.InvalidValues);
        });

        Assert.AreEqual(400, total);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void Skip_LeavesASyntaxErrorFatal(bool generated)
    {
        var values = new List<YamlInvalidValue>();
        var options = new YamlSerializerOptions { InvalidValueHandling = YamlInvalidValueHandling.Skip, InvalidValueCallback = values.Add };

        var exception = Assert.Throws<YamlException>(() => Deserialize<InvalidValueRoot>("Port: [1, 2\nName: a\n", options, generated));

        Assert.IsTrue(exception is SyntaxErrorException or SemanticErrorException, exception.GetType().Name);
        Assert.IsEmpty(values);
    }

    private static YamlInvalidValue CreateValue(string path, Mark start, Mark end, string message)
    {
        var exception = new YamlException("config.yaml", start, end, message);
        return new YamlInvalidValue("config.yaml", typeof(object), null, path[(path.LastIndexOf('.') + 1)..], path, start, start, typeof(int), exception);
    }
}

internal sealed class InvalidValueRoot
{
    public string Name { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;
    public int Timeout { get; set; } = 30;
    public List<int> Retries { get; set; } = new();
    public InvalidValueRejecting? Rejecting { get; set; }
    public InvalidValueConsuming? Consuming { get; set; }
    public InvalidValueFormatted? Formatted { get; set; }
}

internal sealed class InvalidValueRequired
{
    [YamlRequired]
    public int Port { get; set; }
}

internal sealed class InvalidValueConstructed
{
    public InvalidValueConstructed(string name, int port = 8080, int timeout = 30)
    {
        Name = name;
        Port = port;
        Timeout = timeout;
    }

    public string Name { get; }
    public int Port { get; }
    public int Timeout { get; }
}

internal sealed class InvalidValueMergeRoot
{
    public InvalidValueRoot? Server { get; set; }
    public InvalidValueConstructed? Constructed { get; set; }
}

internal sealed class InvalidValuePopulateRoot
{
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public InvalidValueRoot Server { get; } = new() { Name = "keep" };

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<int> Retries { get; } = new() { 7 };

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, int> Ports { get; } = new() { ["keep"] = 1 };

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<int, int> ByNumber { get; } = new(InvalidValueAbsoluteKeyComparer.Instance);

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IDictionary<int, int> NumberMap { get; } = new Dictionary<int, int>(InvalidValueAbsoluteKeyComparer.Instance);

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IDictionary<int, int> SortedMap { get; } = new SortedDictionary<int, int>(InvalidValueAbsoluteKeyComparer.Instance);

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IDictionary<int, int> SortedListMap { get; } = new SortedList<int, int>(InvalidValueAbsoluteKeyComparer.Instance);

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IDictionary<int, int> OrderedMap { get; } = new OrderedDictionary<int, int>(InvalidValueAbsoluteKeyComparer.Instance);

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, object> ObjectPorts { get; } = new() { ["keep"] = 3 };
}

internal sealed class InvalidValueAbsoluteKeyComparer : IEqualityComparer<int>, IComparer<int>
{
    public static readonly InvalidValueAbsoluteKeyComparer Instance = new();

    public bool Equals(int x, int y) => Math.Abs(x) == Math.Abs(y);

    public int GetHashCode(int value) => Math.Abs(value).GetHashCode();

    public int Compare(int x, int y) => Math.Abs(x).CompareTo(Math.Abs(y));
}

internal sealed class InvalidValueCollections
{
    public int Port { get; set; } = 8080;
    public List<int> Retries { get; set; } = new();
    public int[] Codes { get; set; } = Array.Empty<int>();
    public HashSet<int> Unique { get; set; } = new();
    public ISet<int> Distinct { get; set; } = new HashSet<int>();
    public IList<int> Ordered { get; set; } = new List<int>();
    public ICollection<int> Bag { get; set; } = new List<int>();
    public IReadOnlyList<int> ReadOnlyList { get; set; } = Array.Empty<int>();
    public IReadOnlyCollection<int> ReadOnlyBag { get; set; } = Array.Empty<int>();
    public IEnumerable<int> Items { get; set; } = Array.Empty<int>();
    public ImmutableArray<int> Frozen { get; set; }
    public ImmutableList<int> FrozenList { get; set; } = ImmutableList<int>.Empty;
    public ImmutableHashSet<int> FrozenSet { get; set; } = ImmutableHashSet<int>.Empty;
    public Collection<int> Custom { get; set; } = new();
    public List<List<int>> Matrix { get; set; } = new();
    public Dictionary<string, int> Ports { get; set; } = new();
    public Dictionary<string, object> ObjectPorts { get; set; } = new();
    public Dictionary<int, int> ByNumber { get; set; } = new();
    public List<InvalidValueRejecting> Rejected { get; set; } = new();
    public List<InvalidValueConsuming> Consumed { get; set; } = new();
}

internal sealed class InvalidValueZoo
{
    public List<InvalidValueAnimal> Pets { get; set; } = new();
}

[YamlPolymorphic]
[YamlDerivedType(typeof(InvalidValueDog), "dog")]
internal abstract class InvalidValueAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class InvalidValueDog : InvalidValueAnimal
{
    public int BarkVolume { get; set; } = 3;
}

[YamlConverter(typeof(InvalidValueRejectingConverter))]
internal sealed class InvalidValueRejecting
{
}

[YamlConverter(typeof(InvalidValueConsumingConverter))]
internal sealed class InvalidValueConsuming
{
}

[YamlConverter(typeof(InvalidValueFormattedConverter))]
internal sealed class InvalidValueFormatted
{
}

internal sealed class InvalidValueRejectingConverter : YamlConverter<InvalidValueRejecting>
{
    public override InvalidValueRejecting Read(YamlReader reader) => throw reader.CreateException("rejected before consuming");

    public override void Write(YamlWriter writer, InvalidValueRejecting value) => writer.WriteScalar(string.Empty);
}

internal sealed class InvalidValueConsumingConverter : YamlConverter<InvalidValueConsuming>
{
    public override InvalidValueConsuming Read(YamlReader reader)
    {
        reader.Read();
        throw reader.CreateException("rejected after consuming");
    }

    public override void Write(YamlWriter writer, InvalidValueConsuming value) => writer.WriteScalar(string.Empty);
}

internal sealed class InvalidValueFormattedConverter : YamlConverter<InvalidValueFormatted>
{
    public override InvalidValueFormatted Read(YamlReader reader) => throw new FormatException("not formatted");

    public override void Write(YamlWriter writer, InvalidValueFormatted value) => writer.WriteScalar(string.Empty);
}

internal sealed class InvalidValueObjectIntConverter : YamlConverter
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

    public override object Read(YamlReader reader, Type typeToConvert) => reader.GetConverter(typeof(int)).Read(reader, typeof(int))!;

    public override void Write(YamlWriter writer, object value) => writer.GetConverter(typeof(int)).Write(writer, value);
}

[YamlSerializable(typeof(InvalidValueRoot))]
[YamlSerializable(typeof(InvalidValueRequired))]
[YamlSerializable(typeof(InvalidValueConstructed))]
[YamlSerializable(typeof(InvalidValueMergeRoot))]
[YamlSerializable(typeof(InvalidValuePopulateRoot))]
[YamlSerializable(typeof(InvalidValueCollections))]
[YamlSerializable(typeof(List<int>))]
[YamlSerializable(typeof(Dictionary<string, int>))]
[YamlSerializable(typeof(Dictionary<string, object>))]
[YamlSerializable(typeof(InvalidValueZoo))]
[YamlSerializable(typeof(InvalidValueRejecting))]
[YamlSerializable(typeof(InvalidValueConsuming))]
[YamlSerializable(typeof(InvalidValueFormatted))]
internal partial class InvalidValueContext : YamlSerializerContext
{
    public InvalidValueContext()
    {
    }

    public InvalidValueContext(YamlSerializerOptions options) : base(options)
    {
    }
}
