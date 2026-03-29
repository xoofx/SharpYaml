#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlTagOnlyDerivedTypeTests
{
    // ---- Model types: tag-only entries (no discriminator) via attributes ----

    [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
    [YamlDerivedType(typeof(AttrCheckin), Tag = "!checkin")]
    [YamlDerivedType(typeof(AttrDns), Tag = "!dns")]
    [YamlDerivedType(typeof(AttrHttp), Tag = "!http")]
    private class AttrMonitor
    {
        public string Interval { get; set; } = string.Empty;
    }

    private sealed class AttrCheckin : AttrMonitor
    {
        public string Endpoint { get; set; } = string.Empty;
    }

    private sealed class AttrDns : AttrMonitor
    {
        public string Host { get; set; } = string.Empty;
    }

    private sealed class AttrHttp : AttrMonitor
    {
        public string Url { get; set; } = string.Empty;
    }

    // ---- Model types: runtime tag-only entries ----

    private class RuntimeMonitor
    {
        public string Interval { get; set; } = string.Empty;
    }

    private sealed class RuntimeCheckin : RuntimeMonitor
    {
        public string Endpoint { get; set; } = string.Empty;
    }

    private sealed class RuntimeDns : RuntimeMonitor
    {
        public string Host { get; set; } = string.Empty;
    }

    private sealed class RuntimeHttp : RuntimeMonitor
    {
        public string Url { get; set; } = string.Empty;
    }

    // ---- Attribute-based: tag-only entries should NOT set default ----

    [TestMethod]
    public void TagOnlyAttribute_NoTagDeserializesAsBaseType()
    {
        // All derived types have tags but no discriminators.
        // An untagged mapping should deserialize as the base type, not the first entry.
        var yaml = "Interval: 00:00:10\n";
        var value = YamlSerializer.Deserialize<AttrMonitor>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<AttrMonitor>(value);
        Assert.IsFalse(value is AttrCheckin, "Should not be AttrCheckin — tag-only entries must not become default");
        Assert.IsFalse(value is AttrDns);
        Assert.IsFalse(value is AttrHttp);
        Assert.AreEqual("00:00:10", value.Interval);
    }

    [TestMethod]
    public void TagOnlyAttribute_TaggedDeserializesAsDerivedType()
    {
        var yaml = "!dns\nHost: google.com\nInterval: 00:01:00\n";
        var value = YamlSerializer.Deserialize<AttrMonitor>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<AttrDns>(value);
        Assert.AreEqual("google.com", ((AttrDns)value).Host);
    }

    [TestMethod]
    public void TagOnlyAttribute_AllTagsWork()
    {
        var checkinYaml = "!checkin\nEndpoint: /health\nInterval: 00:00:30\n";
        var dnsYaml = "!dns\nHost: dns.google\nInterval: 00:01:00\n";
        var httpYaml = "!http\nUrl: https://example.com\nInterval: 00:05:00\n";

        var checkin = YamlSerializer.Deserialize<AttrMonitor>(checkinYaml);
        var dns = YamlSerializer.Deserialize<AttrMonitor>(dnsYaml);
        var http = YamlSerializer.Deserialize<AttrMonitor>(httpYaml);

        Assert.IsInstanceOfType<AttrCheckin>(checkin);
        Assert.IsInstanceOfType<AttrDns>(dns);
        Assert.IsInstanceOfType<AttrHttp>(http);
        Assert.AreEqual("/health", ((AttrCheckin)checkin).Endpoint);
        Assert.AreEqual("dns.google", ((AttrDns)dns).Host);
        Assert.AreEqual("https://example.com", ((AttrHttp)http).Url);
    }

    [TestMethod]
    public void TagOnlyAttribute_DictionaryWithMixedTagsAndUntagged()
    {
        var yaml = """
            google: !http
              Url: https://google.com
              Interval: 00:05:00
            router: !dns
              Host: 192.168.1.1
              Interval: 00:01:00
            base:
              Interval: 00:00:10
            """;

        var dict = YamlSerializer.Deserialize<Dictionary<string, AttrMonitor>>(yaml);

        Assert.IsNotNull(dict);
        Assert.AreEqual(3, dict.Count);
        Assert.IsInstanceOfType<AttrHttp>(dict["google"]);
        Assert.IsInstanceOfType<AttrDns>(dict["router"]);
        Assert.IsInstanceOfType<AttrMonitor>(dict["base"]);
        Assert.IsFalse(dict["base"] is AttrCheckin, "Untagged entries must not resolve to first tag-only entry");
    }

    // ---- Runtime: tag-only entries should NOT set default ----

    [TestMethod]
    public void TagOnlyRuntime_NoTagDeserializesAsBaseType()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeMonitor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeCheckin)) { Tag = "!checkin" },
                        new YamlDerivedType(typeof(RuntimeDns)) { Tag = "!dns" },
                        new YamlDerivedType(typeof(RuntimeHttp)) { Tag = "!http" },
                    }
                }
            }
        };

        var yaml = "Interval: 00:00:10\n";
        var value = YamlSerializer.Deserialize<RuntimeMonitor>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<RuntimeMonitor>(value);
        Assert.IsFalse(value is RuntimeCheckin, "Should not be RuntimeCheckin — tag-only entries must not become default");
        Assert.IsFalse(value is RuntimeDns);
        Assert.IsFalse(value is RuntimeHttp);
        Assert.AreEqual("00:00:10", value.Interval);
    }

    [TestMethod]
    public void TagOnlyRuntime_TaggedDeserializesAsDerivedType()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeMonitor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeCheckin)) { Tag = "!checkin" },
                        new YamlDerivedType(typeof(RuntimeHttp)) { Tag = "!http" },
                    }
                }
            }
        };

        var yaml = "!http\nUrl: https://example.com\nInterval: 00:05:00\n";
        var value = YamlSerializer.Deserialize<RuntimeMonitor>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<RuntimeHttp>(value);
        Assert.AreEqual("https://example.com", ((RuntimeHttp)value).Url);
    }

    [TestMethod]
    public void TagOnlyRuntime_DictionaryWithMixedTagsAndUntagged()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeMonitor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeCheckin)) { Tag = "!checkin" },
                        new YamlDerivedType(typeof(RuntimeDns)) { Tag = "!dns" },
                    }
                }
            }
        };

        var yaml = """
            health: !checkin
              Endpoint: /ping
              Interval: 00:00:30
            plain:
              Interval: 00:00:10
            """;

        var dict = YamlSerializer.Deserialize<Dictionary<string, RuntimeMonitor>>(yaml, options);

        Assert.IsNotNull(dict);
        Assert.AreEqual(2, dict.Count);
        Assert.IsInstanceOfType<RuntimeCheckin>(dict["health"]);
        Assert.IsInstanceOfType<RuntimeMonitor>(dict["plain"]);
        Assert.IsFalse(dict["plain"] is RuntimeCheckin);
    }

    // ---- Explicit default still works when a separate no-tag no-discriminator entry exists ----

    [TestMethod]
    public void ExplicitDefaultDerivedTypeWithTagOnlyEntries()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeMonitor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeCheckin)) { Tag = "!checkin" },
                        new YamlDerivedType(typeof(RuntimeDns)) { Tag = "!dns" },
                        new YamlDerivedType(typeof(RuntimeHttp)),  // no tag, no discriminator → explicit default
                    }
                }
            }
        };

        // Untagged entries should resolve to RuntimeHttp (the explicit default)
        var yaml = "Url: https://fallback.com\nInterval: 00:01:00\n";
        var value = YamlSerializer.Deserialize<RuntimeMonitor>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<RuntimeHttp>(value);
        Assert.AreEqual("https://fallback.com", ((RuntimeHttp)value).Url);
    }

    // ---- Serialization roundtrip for tag-only entries ----

    [TestMethod]
    public void TagOnlyAttribute_RoundtripSerialization()
    {
        AttrMonitor monitor = new AttrDns { Host = "google.com", Interval = "00:01:00" };
        var yaml = YamlSerializer.Serialize(monitor, typeof(AttrMonitor));

        StringAssert.Contains(yaml, "!dns");
        StringAssert.Contains(yaml, "Host: google.com");

        var deserialized = YamlSerializer.Deserialize<AttrMonitor>(yaml);
        Assert.IsInstanceOfType<AttrDns>(deserialized);
        Assert.AreEqual("google.com", ((AttrDns)deserialized).Host);
    }

    [TestMethod]
    public void TagOnlyRuntime_RoundtripSerialization()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeMonitor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeCheckin)) { Tag = "!checkin" },
                        new YamlDerivedType(typeof(RuntimeDns)) { Tag = "!dns" },
                    }
                }
            }
        };

        RuntimeMonitor monitor = new RuntimeDns { Host = "dns.google", Interval = "00:01:00" };
        var yaml = YamlSerializer.Serialize(monitor, typeof(RuntimeMonitor), options);

        StringAssert.Contains(yaml, "!dns");
        StringAssert.Contains(yaml, "Host: dns.google");

        var deserialized = YamlSerializer.Deserialize<RuntimeMonitor>(yaml, options);
        Assert.IsInstanceOfType<RuntimeDns>(deserialized);
        Assert.AreEqual("dns.google", ((RuntimeDns)deserialized).Host);
    }
}
