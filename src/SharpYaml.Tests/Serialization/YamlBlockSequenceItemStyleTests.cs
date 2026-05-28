#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlBlockSequenceItemStyleTests
{
    [TestMethod]
    public void SequenceMappings_DefaultToCompactStyle()
    {
        var yaml = YamlSerializer.Serialize(CreateConfig(), new YamlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.AreEqual(
            "contexts:\n  - name: default\n    target: localhost\n    credential: localhost-admin\n",
            yaml);
    }

    [TestMethod]
    public void SequenceMappings_CanBeExpandedGlobally()
    {
        var yaml = YamlSerializer.Serialize(
            CreateConfig(),
            new YamlSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                BlockSequenceMappingStyle = YamlSequenceItemStyle.Expanded,
            });

        Assert.AreEqual(
            "contexts:\n  -\n    name: default\n    target: localhost\n    credential: localhost-admin\n",
            yaml);
    }

    [TestMethod]
    public void SequenceMappings_CanBeExpandedForOneMember()
    {
        var yaml = YamlSerializer.Serialize(new MixedStyleConfig(), new YamlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        StringAssert.Contains(yaml, "compact:\n  - name: compact\n    target: localhost\n    credential: token\n");
        StringAssert.Contains(yaml, "expanded:\n  -\n    name: expanded\n    target: localhost\n    credential: token\n");
    }

    [TestMethod]
    public void SequenceMappings_CanBeCompactedForOneMemberWhenGlobalDefaultIsExpanded()
    {
        var yaml = YamlSerializer.Serialize(
            new MemberCompactConfig(),
            new YamlSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                BlockSequenceMappingStyle = YamlSequenceItemStyle.Expanded,
            });

        Assert.AreEqual(
            "contexts:\n  - name: compact\n    target: localhost\n    credential: token\n",
            yaml);
    }

    [TestMethod]
    public void SequenceSequences_CanBeCompactedGlobally()
    {
        var yaml = YamlSerializer.Serialize(
            new RowsConfig { Rows = new List<List<string>> { new() { "a", "b" } } },
            new YamlSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                BlockSequenceSequenceStyle = YamlSequenceItemStyle.Compact,
            });

        Assert.AreEqual("rows:\n  - - a\n    - b\n", yaml);
    }

    [TestMethod]
    public void SequenceSequences_CanBeCompactedForOneMember()
    {
        var yaml = YamlSerializer.Serialize(new MemberCompactRowsConfig(), new YamlSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.AreEqual("rows:\n  - - a\n    - b\n", yaml);
    }

    [TestMethod]
    public void SourceGeneratedMemberOverride_UsesBlockSequenceItemStyleAttribute()
    {
        var context = new BlockSequenceItemStyleYamlContext(new YamlSerializerOptions
        {
            BlockSequenceMappingStyle = YamlSequenceItemStyle.Expanded,
        });
        var value = new GeneratedMemberCompactConfig
        {
            Contexts =
            [
                new GeneratedContextItem { Name = "compact", Target = "localhost", Credential = "token" },
            ],
        };

        var yaml = YamlSerializer.Serialize(value, typeof(GeneratedMemberCompactConfig), context);

        Assert.AreEqual(
            "Contexts:\n  - Name: compact\n    Target: localhost\n    Credential: token\n",
            yaml);
    }

    private static Config CreateConfig() => new()
    {
        Contexts = new List<Context>
        {
            new()
            {
                Name = "default",
                Target = "localhost",
                Credential = "localhost-admin",
            },
        },
    };

    private sealed class Config
    {
        public List<Context> Contexts { get; set; } = new();
    }

    private sealed class MixedStyleConfig
    {
        public List<Context> Compact { get; set; } =
        [
            new Context { Name = "compact", Target = "localhost", Credential = "token" },
        ];

        [YamlBlockSequenceItemStyle(YamlSequenceItemStyle.Expanded)]
        public List<Context> Expanded { get; set; } =
        [
            new Context { Name = "expanded", Target = "localhost", Credential = "token" },
        ];
    }

    private sealed class MemberCompactConfig
    {
        [YamlBlockSequenceItemStyle(YamlSequenceItemStyle.Compact)]
        public List<Context> Contexts { get; set; } =
        [
            new Context { Name = "compact", Target = "localhost", Credential = "token" },
        ];
    }

    private sealed class RowsConfig
    {
        public List<List<string>> Rows { get; set; } = new();
    }

    private sealed class MemberCompactRowsConfig
    {
        [YamlBlockSequenceItemStyle(SequenceStyle = YamlSequenceItemStyle.Compact)]
        public List<List<string>> Rows { get; set; } =
        [
            ["a", "b"],
        ];
    }

    private sealed class Context
    {
        public string Name { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string Credential { get; set; } = string.Empty;
    }
}

[YamlSerializable(typeof(GeneratedMemberCompactConfig))]
[YamlSerializable(typeof(GeneratedContextItem))]
internal partial class BlockSequenceItemStyleYamlContext : YamlSerializerContext
{
    public BlockSequenceItemStyleYamlContext(YamlSerializerOptions options)
        : base(options)
    {
    }
}

internal sealed class GeneratedMemberCompactConfig
{
    [YamlBlockSequenceItemStyle(YamlSequenceItemStyle.Compact)]
    public List<GeneratedContextItem> Contexts { get; set; } = new();
}

internal sealed class GeneratedContextItem
{
    public string Name { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Credential { get; set; } = string.Empty;
}
