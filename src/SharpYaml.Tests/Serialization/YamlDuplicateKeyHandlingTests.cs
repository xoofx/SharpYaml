#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
