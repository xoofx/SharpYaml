#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlUntypedObjectTests
{
    [TestMethod]
    public void DeserializeObject_InfersScalarTypes()
    {
        Assert.AreEqual(true, YamlSerializer.Deserialize<object>("true"));
        Assert.AreEqual(42L, YamlSerializer.Deserialize<object>("42"));
        Assert.AreEqual(1.5d, (double)YamlSerializer.Deserialize<object>("1.5")!, 1e-12);
        Assert.AreEqual("text", YamlSerializer.Deserialize<object>("text"));
        Assert.IsNull(YamlSerializer.Deserialize<object>("null"));
        Assert.IsNull(YamlSerializer.Deserialize<object>("~"));
    }

    [TestMethod]
    public void DeserializeObject_InfersSequenceAndMapping()
    {
        var list = (List<object?>)YamlSerializer.Deserialize<object>("- 1\n- true\n- text\n- null\n")!;
        Assert.AreEqual(4, list.Count);
        Assert.AreEqual(1L, list[0]);
        Assert.AreEqual(true, list[1]);
        Assert.AreEqual("text", list[2]);
        Assert.IsNull(list[3]);

        var dict = (Dictionary<string, object?>)YamlSerializer.Deserialize<object>("a: 1\nb: true\nc: text\n")!;
        Assert.AreEqual(3, dict.Count);
        Assert.AreEqual(1L, dict["a"]);
        Assert.AreEqual(true, dict["b"]);
        Assert.AreEqual("text", dict["c"]);
    }

    [TestMethod]
    public void UnsafeTagActivation_IsOptIn()
    {
        var yaml = "!System.Int32 42\n";

        var defaultValue = YamlSerializer.Deserialize<object>(yaml);
        Assert.AreEqual(42L, defaultValue);

        var unsafeValue = YamlSerializer.Deserialize<object>(
            yaml,
            new YamlSerializerOptions { UnsafeAllowDeserializeFromTagTypeName = true });
        Assert.AreEqual(42, unsafeValue);
        Assert.IsInstanceOfType<int>(unsafeValue);
    }

    [TestMethod]
    public void UnsafeTagActivation_HandlesMscorlibTypeNames()
    {
        var yaml = "!System.Int32,mscorlib 42\n";
        var value = YamlSerializer.Deserialize<object>(
            yaml,
            new YamlSerializerOptions { UnsafeAllowDeserializeFromTagTypeName = true });

        Assert.AreEqual(42, value);
        Assert.IsInstanceOfType<int>(value);
    }

    [TestMethod]
    public void UnsafeTagActivation_IgnoresUnknownTypes()
    {
        var yaml = "!NoSuch.Type 42\n";
        var value = YamlSerializer.Deserialize<object>(
            yaml,
            new YamlSerializerOptions { UnsafeAllowDeserializeFromTagTypeName = true });

        Assert.AreEqual(42L, value);
    }

    [TestMethod]
    public void DeserializeObject_AliasWithoutPreserve_ThrowsYamlException()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<object>("*id001\n"));
        StringAssert.Contains(ex.Message, "ReferenceHandling");
        StringAssert.Contains(ex.Message, "Preserve");
    }
}

