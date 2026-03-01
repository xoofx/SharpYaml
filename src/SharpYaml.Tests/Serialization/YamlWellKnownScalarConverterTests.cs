using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlWellKnownScalarConverterTests
{
    private sealed class Payload
    {
        public DateTime WhenUtc { get; set; }
        public DateTimeOffset WhenOffset { get; set; }
        public Guid Id { get; set; }
        public TimeSpan Duration { get; set; }
    }

    [TestMethod]
    public void RoundTrip_WellKnownScalarTypes_ShouldSucceed()
    {
        var payload = new Payload
        {
            WhenUtc = new DateTime(2026, 03, 01, 12, 34, 56, DateTimeKind.Utc),
            WhenOffset = new DateTimeOffset(2026, 03, 01, 12, 34, 56, TimeSpan.FromHours(2)),
            Id = Guid.Parse("6d0c86e2-1e37-4c33-9c2f-5304a33f2c5e"),
            Duration = TimeSpan.FromMilliseconds(1234),
        };

        var yaml = YamlSerializer.Serialize(payload);
        var roundTrip = YamlSerializer.Deserialize<Payload>(yaml);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.WhenUtc, roundTrip.WhenUtc);
        Assert.AreEqual(payload.WhenOffset, roundTrip.WhenOffset);
        Assert.AreEqual(payload.Id, roundTrip.Id);
        Assert.AreEqual(payload.Duration, roundTrip.Duration);
    }

    [TestMethod]
    public void Deserialize_InvalidGuid_ShouldThrowYamlExceptionWithContext()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Guid>("not-a-guid"));
        StringAssert.Contains(ex.Message, "Guid");
        // Marks are zero-based in SharpYaml, so line/column can be 0 for a scalar at the start of the document.
        StringAssert.Contains(ex.Message, "Lin:");
        StringAssert.Contains(ex.Message, "Col:");
    }
}
