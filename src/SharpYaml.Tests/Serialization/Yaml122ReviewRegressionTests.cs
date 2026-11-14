using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class Yaml122ReviewRegressionTests
{
    [TestMethod]
    [DataRow("key: {\n  a: 1\n}\n", "key: {\n  a: 1\n  }\n")]
    [DataRow("key: [\n  a,\n  b\n]\n", "key: [\n  a,\n  b\n  ]\n")]
    [DataRow("- [\n  a\n]\n", "- [\n  a\n  ]\n")]
    [DataRow("matrix:\n  include: [\n    {os: a},\n    {os: b}\n  ]\n", "matrix:\n  include: [\n    {os: a},\n    {os: b}\n    ]\n")]
    public void FlowClosingIndicatorsRequireIndentation(string invalid, string valid)
    {
        var exception = Assert.Throws<SyntaxErrorException>(() => Parse(invalid));
        StringAssert.Contains(exception.Message, "Flow collection");
        Parse(valid);
        Parse("[\n  a\n]\n"); // No enclosing block requires indentation at the root.
    }

    [TestMethod]
    [DataRow("123456789012345678901234567890")]
    [DataRow("-9223372036854775809")]
    [DataRow("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [DataRow("0o7777777777777777777777777777777777777777")]
    public void LargeCoreIntegersLoadExactlyAndRoundTrip(string text)
    {
        foreach (var useSchema in new[] { false, true })
        {
            var options = new YamlSerializerOptions { UseSchema = useSchema };
            var value = YamlSerializer.Deserialize<object>(text, options);
            Assert.IsInstanceOfType<BigInteger>(value);
            var yaml = YamlSerializer.Serialize(value, options);
            Assert.AreEqual(value, YamlSerializer.Deserialize<object>(yaml, options));
            if (text[0] is not '0') Assert.AreEqual(BigInteger.Parse(text, CultureInfo.InvariantCulture), value);
        }
    }

    [TestMethod]
    public void ExplicitAndImplicitIntegersHaveTheSameDefaultClrType()
    {
        var values = YamlSerializer.Deserialize<List<object?>>("[1, !!int 2, !!int 0x1F, !!int '3']")!;
        CollectionAssert.AreEqual(new object[] { 1L, 2L, 31L, 3L }, values);
    }

    [TestMethod]
    [DataRow("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [DataRow("0o7777777777777777777777777777777777777777")]
    public void NativeIntegerTryParsersReturnFalseForOverflow(string text)
    {
        Assert.IsFalse(YamlScalar.TryParseInt64(text.AsSpan(), out _));
        Assert.IsFalse(YamlScalar.TryParseUInt64(text.AsSpan(), out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TypedLargeIntegersWorkWithReflectionAndGeneratedMetadata(bool generated)
    {
        var options = Options(generated);
        var number = BigInteger.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture);
        var yaml = YamlSerializer.Serialize(number, options);
        Assert.AreEqual(number, YamlSerializer.Deserialize<BigInteger>(yaml, options));
        var model = new ReviewBigNumber { Value = number };
        Assert.AreEqual(number, YamlSerializer.Deserialize<ReviewBigNumber>(YamlSerializer.Serialize(model, options), options)!.Value);
        Assert.AreEqual(new BigInteger(3), YamlSerializer.Deserialize<BigInteger>("3", options));
        var memberOnly = ReviewMemberOnlyContext.Default.ReviewBigNumber;
        Assert.AreEqual(number, YamlSerializer.Deserialize(YamlSerializer.Serialize(model, memberOnly), memberOnly)!.Value);
    }

    [TestMethod]
    [DataRow(YamlReferenceHandling.Preserve, false)]
    [DataRow(YamlReferenceHandling.PreserveMinimal, false)]
    [DataRow(YamlReferenceHandling.Preserve, true)]
    [DataRow(YamlReferenceHandling.PreserveMinimal, true)]
    public void AnchoredMappingsInSequencesKeepTheirValuesAndIdentity(YamlReferenceHandling handling, bool generated)
    {
        var options = Options(generated) with { ReferenceHandling = handling };
        var shared = new ReviewPoco { Name = "value" };
        var typed = new List<ReviewPoco> { shared, shared };
        var typedYaml = YamlSerializer.Serialize(typed, options);
        var typedResult = YamlSerializer.Deserialize<List<ReviewPoco>>(typedYaml, options)!;
        Assert.AreEqual("value", typedResult[0].Name);
        Assert.AreSame(typedResult[0], typedResult[1]);

        var dictionary = new Dictionary<string, object?> { ["k"] = "v" };
        object graph = new List<object?> { dictionary, dictionary };
        var yaml = YamlSerializer.Serialize(graph, options);
        var result = (List<object?>)YamlSerializer.Deserialize<object>(yaml, options)!;
        Assert.IsInstanceOfType<Dictionary<string, object?>>(result[0]);
        Assert.AreEqual("v", ((Dictionary<string, object?>)result[0]!)["k"]);
        Assert.AreSame(result[0], result[1]);
    }

    [TestMethod]
    public void BomCharactersInStringsAndKeysRoundTripThroughBothWriters()
    {
        var source = new Dictionary<string, object?> { ["\uFEFFa"] = "\uFEFFvalue", ["second"] = "x\uFEFFy" };
        var yaml = YamlSerializer.Serialize(source);
        var result = YamlSerializer.Deserialize<Dictionary<string, object?>>(yaml)!;
        Assert.AreEqual("\uFEFFvalue", result["\uFEFFa"]);
        Assert.AreEqual("x\uFEFFy", result["second"]);

        using var output = new StringWriter();
        var emitter = new Emitter(output);
        var parser = Parser.CreateParser(new StringReader("'\uFEFFa': '\uFEFFvalue'"));
        while (parser.MoveNext()) emitter.Emit(parser.Current!);
        var emitted = YamlSerializer.Deserialize<Dictionary<string, object?>>(output.ToString())!;
        Assert.AreEqual("\uFEFFvalue", emitted["\uFEFFa"]);
    }

    [TestMethod]
    [DataRow(false, YamlReferenceHandling.None)]
    [DataRow(true, YamlReferenceHandling.None)]
    [DataRow(false, YamlReferenceHandling.Preserve)]
    [DataRow(true, YamlReferenceHandling.Preserve)]
    public void SkippedRecursiveAliasesAreNotExpanded(bool generated, YamlReferenceHandling handling)
    {
        var options = Options(generated) with { ReferenceHandling = handling, MaxAliasExpansion = 1 };
        var value = YamlSerializer.Deserialize<ReviewPoco>("Unknown: &u {x: *u}\nOther: *u\nName: n", options)!;
        Assert.AreEqual("n", value.Name);
        Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<ReviewPoco>("Unknown: {x: *missing}\nName: n", options));
    }

    private static YamlSerializerOptions Options(bool generated) => new()
    {
        TypeInfoResolver = generated ? ReviewContext.Default : null,
    };

    private static void Parse(string yaml)
    {
        var parser = Parser.CreateParser(new StringReader(yaml));
        while (parser.MoveNext()) { }
        var streaming = new Parser<LookAheadBuffer>(new LookAheadBuffer(new StringReader(yaml), Scanner<LookAheadBuffer>.MaxBufferLength));
        while (streaming.MoveNext()) { }
    }
}

internal sealed class ReviewPoco
{
    public string Name { get; set; } = "";
}

internal sealed class ReviewBigNumber
{
    public BigInteger Value { get; set; }
}

[YamlSerializable(typeof(ReviewPoco))]
[YamlSerializable(typeof(List<ReviewPoco>))]
[YamlSerializable(typeof(object))]
[YamlSerializable(typeof(BigInteger))]
[YamlSerializable(typeof(ReviewBigNumber))]
internal partial class ReviewContext : YamlSerializerContext;

[YamlSerializable(typeof(ReviewBigNumber))]
internal partial class ReviewMemberOnlyContext : YamlSerializerContext;
