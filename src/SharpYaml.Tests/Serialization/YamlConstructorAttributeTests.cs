using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlConstructorAttributeTests
{
    [TestMethod]
    public void Deserialize_UsesYamlConstructor()
    {
        var value = YamlSerializer.Deserialize<YamlCtorModel>("Name: Bob\nAge: 42\n")!;

        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void Deserialize_WhenConstructorParameterMissing_ThrowsYamlException()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<YamlCtorModel>("Name: Bob\n"));
        StringAssert.Contains(ex.Message, "age");
    }

    [TestMethod]
    public void Deserialize_UsesJsonConstructor()
    {
        var value = YamlSerializer.Deserialize<JsonCtorModel>("Name: Bob\nAge: 42\n")!;

        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void Deserialize_UsesPrivateYamlConstructor()
    {
        var value = YamlSerializer.Deserialize<PrivateYamlCtorModel>("Name: Bob\nAge: 42\n")!;

        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void Deserialize_UsesPrivateJsonConstructor()
    {
        var value = YamlSerializer.Deserialize<PrivateJsonCtorModel>("Name: Bob\nAge: 42\n")!;

        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void Serialize_SerializesGetOnlyProperties()
    {
        var yaml = YamlSerializer.Serialize(new YamlCtorModel("Bob", 42));

        StringAssert.Contains(yaml, "Name: Bob");
        StringAssert.Contains(yaml, "Age: 42");
    }

    private sealed class YamlCtorModel
    {
        public YamlCtorModel(string name, int age)
        {
            Name = name;
            Age = age;
        }

        [YamlConstructor]
        public YamlCtorModel(string name, int age, bool ignored = false)
        {
            this.Name = name;
            this.Age = age;
        }

        public string Name { get; }

        public int Age { get; }
    }

    private sealed class JsonCtorModel
    {
        [JsonConstructor]
        public JsonCtorModel(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }

        public int Age { get; }
    }

    private sealed class PrivateYamlCtorModel
    {
        [YamlConstructor]
        private PrivateYamlCtorModel(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }

        public int Age { get; }
    }

    private sealed class PrivateJsonCtorModel
    {
        [JsonConstructor]
        private PrivateJsonCtorModel(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }

        public int Age { get; }
    }
}
