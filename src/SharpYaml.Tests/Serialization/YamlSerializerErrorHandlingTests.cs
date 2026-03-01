#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlSerializerErrorHandlingTests
{
    [TestMethod]
    public void Reflection_ThrowsOnIntegerOverflow_WithLocation()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("999999999999999999999"));
        StringAssert.Contains(ex.Message, "Lin:");
        StringAssert.Contains(ex.Message, "Col:");
    }

    [TestMethod]
    public void Reflection_ThrowsOnTypeMismatch_MappingToScalar()
    {
        _ = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("a: 1"));
    }

    [TestMethod]
    public void Reflection_ThrowsOnTypeMismatch_ScalarToMapping()
    {
        _ = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Dictionary<string, int>>("1"));
    }

    [TestMethod]
    public void Reflection_ThrowsOnTypeMismatch_MappingToSequence()
    {
        _ = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<List<int>>("a: 1"));
    }

    [TestMethod]
    public void SourceGen_ThrowsOnIntegerOverflow_WithLocation()
    {
        var context = TestYamlSerializerContext.Default;
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("999999999999999999999", context.Int32));
        StringAssert.Contains(ex.Message, "Lin:");
        StringAssert.Contains(ex.Message, "Col:");
    }

    [TestMethod]
    public void SourceGen_ThrowsOnTypeMismatch_MappingToScalar()
    {
        var context = TestYamlSerializerContext.Default;
        _ = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("a: 1", context.Int32));
    }

    [TestMethod]
    public void SourceGen_ThrowsOnTypeMismatch_ScalarToMapping()
    {
        var context = TestYamlSerializerContext.Default;
        _ = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("1", context.DictionaryStringInt32));
    }

    [TestMethod]
    public void SourceGen_ThrowsOnTypeMismatch_MappingToSequence()
    {
        var context = TestYamlSerializerContext.Default;
        _ = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("a: 1", context.ListInt32));
    }

    [TestMethod]
    public void SourceGen_PropagatesSourceName()
    {
        var context = new TestYamlSerializerContext(new YamlSerializerOptions { SourceName = "input.yml" });
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("a: 1", context.Int32));
        Assert.AreEqual("input.yml", ex.SourceName);
    }
}

