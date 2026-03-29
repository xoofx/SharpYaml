#nullable enable

using System;
using System.Globalization;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlParsableConverterTests
{
    // ---- BCL types that implement IParsable<T> ----

    [TestMethod]
    public void IPAddress_Roundtrip()
    {
        var ip = IPAddress.Parse("192.168.1.1");
        var yaml = YamlSerializer.Serialize(ip);
        StringAssert.Contains(yaml, "192.168.1.1");

        var deserialized = YamlSerializer.Deserialize<IPAddress>(yaml);
        Assert.AreEqual(ip, deserialized);
    }

    [TestMethod]
    public void IPAddress_IPv6_Roundtrip()
    {
        var ip = IPAddress.Parse("::1");
        var yaml = YamlSerializer.Serialize(ip);

        var deserialized = YamlSerializer.Deserialize<IPAddress>(yaml);
        Assert.AreEqual(ip, deserialized);
    }

    // ---- Model with IParsable properties ----

    private sealed class ServerConfig
    {
        public string Name { get; set; } = string.Empty;
        public IPAddress? BindAddress { get; set; }
        public IPAddress? DnsServer { get; set; }
    }

    [TestMethod]
    public void ModelWithIParsableProperties_Deserialize()
    {
        var yaml = """
            Name: production
            BindAddress: 0.0.0.0
            DnsServer: 1.1.1.1
            """;

        var config = YamlSerializer.Deserialize<ServerConfig>(yaml);

        Assert.IsNotNull(config);
        Assert.AreEqual("production", config.Name);
        Assert.AreEqual(IPAddress.Parse("0.0.0.0"), config.BindAddress);
        Assert.AreEqual(IPAddress.Parse("1.1.1.1"), config.DnsServer);
    }

    [TestMethod]
    public void ModelWithIParsableProperties_Roundtrip()
    {
        var config = new ServerConfig
        {
            Name = "staging",
            BindAddress = IPAddress.Loopback,
            DnsServer = IPAddress.Parse("8.8.8.8"),
        };

        var yaml = YamlSerializer.Serialize(config);
        var deserialized = YamlSerializer.Deserialize<ServerConfig>(yaml);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(config.Name, deserialized.Name);
        Assert.AreEqual(config.BindAddress, deserialized.BindAddress);
        Assert.AreEqual(config.DnsServer, deserialized.DnsServer);
    }

    // ---- Custom IParsable<T> type ----

    private readonly struct Temperature : IParsable<Temperature>, IFormattable
    {
        public double Value { get; }
        public string Unit { get; }

        public Temperature(double value, string unit)
        {
            Value = value;
            Unit = unit;
        }

        public static Temperature Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new FormatException($"Cannot parse '{s}' as Temperature.");
            }

            return result;
        }

        public static bool TryParse(string? s, IFormatProvider? provider, out Temperature result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            // Parse "36.6C" or "98.6F"
            var unitChar = s[^1];
            if (unitChar is not ('C' or 'F'))
            {
                return false;
            }

            if (!double.TryParse(s.AsSpan(0, s.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            result = new Temperature(value, unitChar.ToString());
            return true;
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return Value.ToString("G", CultureInfo.InvariantCulture) + Unit;
        }

        public override string ToString() => ToString(null, null);
    }

    [TestMethod]
    public void CustomIParsableType_Roundtrip()
    {
        var temp = new Temperature(36.6, "C");
        var yaml = YamlSerializer.Serialize(temp);
        StringAssert.Contains(yaml, "36.6C");

        var deserialized = YamlSerializer.Deserialize<Temperature>(yaml);
        Assert.AreEqual(36.6, deserialized.Value);
        Assert.AreEqual("C", deserialized.Unit);
    }

    [TestMethod]
    public void CustomIParsableType_InvalidValueThrows()
    {
        var yaml = "not-a-temperature\n";
        Assert.Throws<YamlException>(
            () => YamlSerializer.Deserialize<Temperature>(yaml));
    }

    // ---- IParsable takes lower priority than explicit converters ----

    [TestMethod]
    public void ExplicitConverterTakesPriorityOverIParsable()
    {
        // IPAddress is IParsable<IPAddress>, but if a custom converter is registered
        // it should take priority
        var options = new YamlSerializerOptions
        {
            Converters = [new AlwaysLocalhostConverter()]
        };

        var yaml = "8.8.8.8\n";
        var deserialized = YamlSerializer.Deserialize<IPAddress>(yaml, options);

        // Custom converter always returns loopback regardless of input
        Assert.AreEqual(IPAddress.Loopback, deserialized);
    }

    private sealed class AlwaysLocalhostConverter : SharpYaml.Serialization.YamlConverter<IPAddress>
    {
        public override IPAddress Read(SharpYaml.Serialization.YamlReader reader)
        {
            reader.Read(); // consume scalar
            return IPAddress.Loopback;
        }

        public override void Write(SharpYaml.Serialization.YamlWriter writer, IPAddress value)
        {
            writer.WriteScalar("127.0.0.1");
        }
    }

    // ---- IParsable does NOT activate for types that are already handled ----

    [TestMethod]
    public void BuiltInConvertersTakePriorityOverIParsable()
    {
        // int implements IParsable<int>, but should use the built-in converter
        var yaml = "42\n";
        var value = YamlSerializer.Deserialize<int>(yaml);
        Assert.AreEqual(42, value);

        var roundtrip = YamlSerializer.Serialize(42);
        StringAssert.Contains(roundtrip, "42");
    }

    // ---- Nullable IParsable properties ----

    private sealed class NullableConfig
    {
        public IPAddress? Address { get; set; }
    }

    [TestMethod]
    public void NullableIParsableProperty_WithValue()
    {
        var yaml = "Address: 10.0.0.1\n";
        var config = YamlSerializer.Deserialize<NullableConfig>(yaml);

        Assert.IsNotNull(config);
        Assert.AreEqual(IPAddress.Parse("10.0.0.1"), config.Address);
    }

    [TestMethod]
    public void NullableIParsableProperty_WithNull()
    {
        var yaml = "Address:\n";
        var config = YamlSerializer.Deserialize<NullableConfig>(yaml);

        Assert.IsNotNull(config);
        Assert.IsNull(config.Address);
    }
}
