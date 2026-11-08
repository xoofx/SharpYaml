using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlInvalidValueTests
{
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

    private static YamlInvalidValue CreateValue(string path, Mark start, Mark end, string message)
    {
        var exception = new YamlException("config.yaml", start, end, message);
        return new YamlInvalidValue("config.yaml", typeof(object), null, path[(path.LastIndexOf('.') + 1)..], path, start, start, typeof(int), exception);
    }
}
