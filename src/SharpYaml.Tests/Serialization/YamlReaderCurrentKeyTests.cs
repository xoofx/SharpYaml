#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlReaderCurrentKeyTests
{
    // ---- Custom converter that captures CurrentKey ----

    private sealed class KeyCapturingConverter : YamlConverter<string>
    {
        public override string? Read(YamlReader reader)
        {
            var currentKey = reader.CurrentKey;
            var scalarValue = reader.ScalarValue;
            reader.Read();

            // Replace ${KEY} placeholder with the actual key
            if (scalarValue is not null && scalarValue.Contains("${KEY}", StringComparison.Ordinal))
            {
                return scalarValue.Replace("${KEY}", currentKey ?? string.Empty, StringComparison.Ordinal);
            }

            return scalarValue;
        }

        public override void Write(YamlWriter writer, string? value)
        {
            writer.WriteScalar(value ?? string.Empty);
        }
    }

    // ---- Dictionary value gets CurrentKey ----

    [TestMethod]
    public void DictionaryValueConverterReceivesCurrentKey()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = "alpha: name is ${KEY}\nbeta: name is ${KEY}\n";
        var result = YamlSerializer.Deserialize<Dictionary<string, string>>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual("name is alpha", result["alpha"]);
        Assert.AreEqual("name is beta", result["beta"]);
    }

    // ---- Object property gets CurrentKey ----

    private sealed class Config
    {
        public string Host { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
    }

    [TestMethod]
    public void ObjectPropertyConverterReceivesCurrentKey()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = "Host: prop=${KEY}\nPort: prop=${KEY}\n";
        var result = YamlSerializer.Deserialize<Config>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual("prop=Host", result.Host);
        Assert.AreEqual("prop=Port", result.Port);
    }

    // ---- Nested dictionary restores CurrentKey after inner read ----

    [TestMethod]
    public void NestedDictionaryRestoresCurrentKeyAfterInnerRead()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = """
            outer1:
              inner1: ${KEY}
              inner2: ${KEY}
            outer2:
              inner3: ${KEY}
            """;

        var result = YamlSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual("inner1", result["outer1"]["inner1"]);
        Assert.AreEqual("inner2", result["outer1"]["inner2"]);
        Assert.AreEqual("inner3", result["outer2"]["inner3"]);
    }

    // ---- CurrentKey is null at the top level ----

    [TestMethod]
    public void TopLevelScalarHasNullCurrentKey()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = "hello world\n";
        var result = YamlSerializer.Deserialize<string>(yaml, options);

        // No key context at top level, so ${KEY} stays (replaced with empty)
        Assert.AreEqual("hello world", result);
    }

    // ---- Object with nested object verifies CurrentKey tracking ----

    private sealed class Outer
    {
        public Inner Details { get; set; } = new();
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Inner
    {
        public string Value { get; set; } = string.Empty;
    }

    [TestMethod]
    public void NestedObjectPropertySetsCurrentKey()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = """
            Details:
              Value: ${KEY}
            Name: ${KEY}
            """;

        var result = YamlSerializer.Deserialize<Outer>(yaml, options);

        Assert.IsNotNull(result);
        // Inside Details mapping, current key is "Value"
        Assert.AreEqual("Value", result.Details.Value);
        // Back at outer level, current key is "Name"
        Assert.AreEqual("Name", result.Name);
    }

    // ---- Dictionary<string, object> also sets CurrentKey ----

    [TestMethod]
    public void DictionaryStringObjectSetsCurrentKey()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = "x: ${KEY}\ny: ${KEY}\n";
        var result = YamlSerializer.Deserialize<Dictionary<string, string>>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual("x", result["x"]);
        Assert.AreEqual("y", result["y"]);
    }

    // ---- Object converter properly restores key after nested mapping ----

    [TestMethod]
    public void ObjectConverterRestoresKeyAfterNestedMapping()
    {
        var options = new YamlSerializerOptions
        {
            Converters = [new KeyCapturingConverter()]
        };

        var yaml = """
            Details:
              Value: inner-${KEY}
            Name: outer-${KEY}
            """;

        var result = YamlSerializer.Deserialize<Outer>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual("inner-Value", result.Details.Value);
        Assert.AreEqual("outer-Name", result.Name);
    }
}
