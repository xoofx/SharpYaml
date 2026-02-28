using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlPrimitiveRoundTripTests
{
    [TestMethod]
    public void Primitives_RoundTrip()
    {
        Assert.AreEqual(true, RoundTrip(true));
        Assert.AreEqual((byte)255, RoundTrip((byte)255));
        Assert.AreEqual((sbyte)-12, RoundTrip((sbyte)-12));
        Assert.AreEqual((short)-32000, RoundTrip((short)-32000));
        Assert.AreEqual((ushort)65000, RoundTrip((ushort)65000));
        Assert.AreEqual(-123456789, RoundTrip(-123456789));
        Assert.AreEqual(4000000000u, RoundTrip(4000000000u));
        Assert.AreEqual(-1234567890123L, RoundTrip(-1234567890123L));
        Assert.AreEqual(1234567890123UL, RoundTrip(1234567890123UL));
        Assert.AreEqual('x', RoundTrip('x'));
        Assert.AreEqual(123.125m, RoundTrip(123.125m));

        Assert.AreEqual(3.5f, RoundTrip(3.5f));
        Assert.IsTrue(float.IsPositiveInfinity(RoundTrip(float.PositiveInfinity)));
        Assert.IsTrue(float.IsNaN(RoundTrip(float.NaN)));

        Assert.AreEqual(3.5d, RoundTrip(3.5d));
        Assert.IsTrue(double.IsPositiveInfinity(RoundTrip(double.PositiveInfinity)));
        Assert.IsTrue(double.IsNaN(RoundTrip(double.NaN)));

        Assert.AreEqual((nint)123, RoundTrip((nint)123));
        Assert.AreEqual((nuint)123, RoundTrip((nuint)123));
    }

    [TestMethod]
    public void Primitives_ParseUnderscoresAndBases()
    {
        Assert.AreEqual(1000, YamlSerializer.Deserialize<int>("1_000"));
        Assert.AreEqual(16, YamlSerializer.Deserialize<int>("0x10"));
        Assert.AreEqual(8, YamlSerializer.Deserialize<int>("0o10"));
        Assert.AreEqual(10u, YamlSerializer.Deserialize<uint>("0b1010"));
        Assert.AreEqual(255ul, YamlSerializer.Deserialize<ulong>("0xFF"));
        Assert.AreEqual(12.5, YamlSerializer.Deserialize<double>("1_2.5"));
        Assert.AreEqual(12.5m, YamlSerializer.Deserialize<decimal>("1_2.5"));
    }

    private static T RoundTrip<T>(T value)
    {
        var yaml = YamlSerializer.Serialize(value);
        return YamlSerializer.Deserialize<T>(yaml)!;
    }
}

