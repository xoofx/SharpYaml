using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlLifecycleCallbackTests
{
    [TestMethod]
    public void Serialize_CallsSerializingAndSerialized()
    {
        var value = new LifecycleModel { Value = 123 };

        _ = YamlSerializer.Serialize(value);

        Assert.AreEqual(2, value.Events.Count);
        Assert.AreEqual(nameof(IYamlOnSerializing.OnSerializing), value.Events[0]);
        Assert.AreEqual(nameof(IYamlOnSerialized.OnSerialized), value.Events[1]);
    }

    [TestMethod]
    public void Deserialize_CallsDeserializingAndDeserialized()
    {
        var value = YamlSerializer.Deserialize<LifecycleModel>("value: 42\n")!;

        Assert.AreEqual(2, value.Events.Count);
        Assert.AreEqual(nameof(IYamlOnDeserializing.OnDeserializing), value.Events[0]);
        Assert.AreEqual(nameof(IYamlOnDeserialized.OnDeserialized), value.Events[1]);
    }

    [TestMethod]
    public void Serialize_WhenCallbackThrows_WrapsInYamlException()
    {
        var value = new ThrowingLifecycleModel();

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Serialize(value));
        Assert.IsNotNull(exception.InnerException);
        Assert.AreEqual("boom", exception.InnerException!.Message);
    }

    private sealed class LifecycleModel : IYamlOnDeserializing, IYamlOnDeserialized, IYamlOnSerializing, IYamlOnSerialized
    {
        public List<string> Events { get; } = new();

        public int Value { get; set; }

        public void OnDeserialized() => Events.Add(nameof(IYamlOnDeserialized.OnDeserialized));

        public void OnDeserializing() => Events.Add(nameof(IYamlOnDeserializing.OnDeserializing));

        public void OnSerialized() => Events.Add(nameof(IYamlOnSerialized.OnSerialized));

        public void OnSerializing() => Events.Add(nameof(IYamlOnSerializing.OnSerializing));
    }

    private sealed class ThrowingLifecycleModel : IYamlOnSerializing
    {
        public void OnSerializing() => throw new InvalidOperationException("boom");
    }
}

