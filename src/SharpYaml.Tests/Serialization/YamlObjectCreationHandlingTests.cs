#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlObjectCreationHandlingTests
{
    private sealed class ChildModel
    {
        public int Existing { get; set; }

        public int Added { get; set; }
    }

    private struct StructChildModel
    {
        public int Existing { get; set; }

        public int Added { get; set; }
    }

    private sealed class ReplaceByDefaultModel
    {
        public ChildModel Child { get; } = new() { Existing = 1 };

        public List<int> Numbers { get; } = [1, 2];
    }

    private sealed class PopulateViaOptionsModel
    {
        public ChildModel Child { get; } = new() { Existing = 1 };

        public List<int> Numbers { get; } = [1, 2];
    }

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    private sealed class PopulateViaTypeAttributeModel
    {
        public ChildModel Child { get; } = new() { Existing = 1 };

        [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
        public List<int> Numbers { get; } = [1, 2];
    }

    private sealed class PopulateStructModel
    {
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public StructChildModel Child { get; set; } = new() { Existing = 1 };
    }

    private sealed class ReadOnlyPopulateStructModel
    {
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public StructChildModel Child { get; } = new() { Existing = 1 };
    }

    [TestMethod]
    public void ReadOnlyMembers_ReplaceByDefault()
    {
        var yaml = """
            Child:
              Added: 2
            Numbers:
              - 3
              - 4
            """;

        var model = YamlSerializer.Deserialize<ReplaceByDefaultModel>(yaml);

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.Child.Existing);
        Assert.AreEqual(0, model.Child.Added);
        CollectionAssert.AreEqual(new[] { 1, 2 }, model.Numbers);
    }

    [TestMethod]
    public void PreferredObjectCreationHandlingPopulate_PopulatesReadOnlyMembers()
    {
        var yaml = """
            Child:
              Added: 2
            Numbers:
              - 3
              - 4
            """;

        var model = YamlSerializer.Deserialize<PopulateViaOptionsModel>(
            yaml,
            new YamlSerializerOptions
            {
                PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            });

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.Child.Existing);
        Assert.AreEqual(2, model.Child.Added);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, model.Numbers);
    }

    [TestMethod]
    public void JsonObjectCreationHandlingAttribute_OnTypeCanPopulate_AndPropertyCanOverrideToReplace()
    {
        var yaml = """
            Child:
              Added: 2
            Numbers:
              - 3
              - 4
            """;

        var model = YamlSerializer.Deserialize<PopulateViaTypeAttributeModel>(yaml);

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.Child.Existing);
        Assert.AreEqual(2, model.Child.Added);
        CollectionAssert.AreEqual(new[] { 1, 2 }, model.Numbers);
    }

    [TestMethod]
    public void Populate_OnStructPropertyWithSetter_ModifiesCopyAndAssignsBack()
    {
        var yaml = """
            Child:
              Added: 2
            """;

        var model = YamlSerializer.Deserialize<PopulateStructModel>(yaml);

        Assert.IsNotNull(model);
        Assert.AreEqual(1, model.Child.Existing);
        Assert.AreEqual(2, model.Child.Added);
    }

    [TestMethod]
    public void Populate_OnReadOnlyStructProperty_Throws()
    {
        var yaml = """
            Child:
              Added: 2
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize<ReadOnlyPopulateStructModel>(yaml));

        StringAssert.Contains(exception.Message, "value type");
        StringAssert.Contains(exception.Message, "doesn't have a setter");
    }
}
