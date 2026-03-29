#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlSerializerContextCreateOptionsTests
{
    [TestMethod]
    public void CreateOptions_OverridesSourceName()
    {
        var context = new TestYamlSerializerContext();
        var options = context.CreateOptions(o => o with { SourceName = "config.yaml" });

        Assert.AreEqual("config.yaml", options.SourceName);
        Assert.AreSame(context, options.TypeInfoResolver);
    }

    [TestMethod]
    public void CreateOptions_PreservesTypeInfoResolver()
    {
        var context = new TestYamlSerializerContext();
        var options = context.CreateOptions(o => o with { WriteIndented = false });

        Assert.AreSame(context, options.TypeInfoResolver);
        Assert.IsFalse(options.WriteIndented);
    }

    [TestMethod]
    public void CreateOptions_OverwritesResolverIfDifferent()
    {
        var context1 = new TestYamlSerializerContext();
        var context2 = new TestYamlSerializerContext();

        // Even if configure tries to set a different resolver, CreateOptions overwrites it
        var options = context1.CreateOptions(o => o with { TypeInfoResolver = context2 });

        Assert.AreSame(context1, options.TypeInfoResolver);
    }

    [TestMethod]
    public void CreateOptions_PreservesOriginalConverters()
    {
        var converter = new DummyConverter();
        var baseOptions = new YamlSerializerOptions { Converters = [converter] };
        var context = new TestYamlSerializerContext(baseOptions);

        var options = context.CreateOptions(o => o with { SourceName = "test.yaml" });

        Assert.AreEqual(1, options.Converters.Count);
        Assert.AreSame(converter, options.Converters[0]);
        Assert.AreEqual("test.yaml", options.SourceName);
    }

    [TestMethod]
    public void CreateOptions_CanAddConverters()
    {
        var context = new TestYamlSerializerContext();
        var newConverter = new DummyConverter();

        var options = context.CreateOptions(o => o with
        {
            Converters = [newConverter],
            SourceName = "extra.yaml"
        });

        Assert.AreEqual(1, options.Converters.Count);
        Assert.AreSame(newConverter, options.Converters[0]);
        Assert.AreEqual("extra.yaml", options.SourceName);
        Assert.AreSame(context, options.TypeInfoResolver);
    }

    [TestMethod]
    public void CreateOptions_CanOverrideMultipleProperties()
    {
        var context = new TestYamlSerializerContext();
        var options = context.CreateOptions(o => o with
        {
            SourceName = "multi.yaml",
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            Schema = YamlSchemaKind.Extended,
        });

        Assert.AreEqual("multi.yaml", options.SourceName);
        Assert.IsFalse(options.WriteIndented);
        Assert.IsTrue(options.PropertyNameCaseInsensitive);
        Assert.AreEqual(YamlSchemaKind.Extended, options.Schema);
        Assert.AreSame(context, options.TypeInfoResolver);
    }

    [TestMethod]
    public void CreateOptions_IdentityReturnsOptionsWithSameResolver()
    {
        var context = new TestYamlSerializerContext();
        var options = context.CreateOptions(o => o);

        // Identity transform preserves the resolver
        Assert.AreSame(context, options.TypeInfoResolver);
    }

    [TestMethod]
    public void CreateOptions_WorksWithOptionsBasedContext()
    {
        var baseOptions = new YamlSerializerOptions
        {
            SourceName = "original.yaml",
            WriteIndented = false,
        };
        var context = new TestYamlSerializerContext(baseOptions);

        // Override just SourceName, keep other base settings
        var options = context.CreateOptions(o => o with { SourceName = "override.yaml" });

        Assert.AreEqual("override.yaml", options.SourceName);
        Assert.IsFalse(options.WriteIndented); // Preserved from base
        Assert.AreSame(context, options.TypeInfoResolver);
    }

    [TestMethod]
    public void CreateOptions_ResultCanBeUsedForDeserialization()
    {
        var context = new TestYamlSerializerContext();
        var options = context.CreateOptions(o => o with { SourceName = "test-input.yaml" });

        // Verify the options work for actual deserialization
        var yaml = "first_name: hello\nAge: 42\n";
        var result = YamlSerializer.Deserialize<GeneratedPerson>(yaml, options);

        Assert.IsNotNull(result);
        Assert.AreEqual("hello", result.FirstName);
    }

    [TestMethod]
    public void CreateOptions_SourceNameAppearsInErrorMessages()
    {
        var context = new TestYamlSerializerContext();
        var options = context.CreateOptions(o => o with { SourceName = "myfile.yaml" });

        var yaml = "first_name: hello\nAge: not-a-number\n";
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize<GeneratedPerson>(yaml, options));

        StringAssert.Contains(ex.Message, "myfile.yaml");
    }

    private sealed class DummyConverter : YamlConverter<int>
    {
        public override int Read(YamlReader reader)
        {
            reader.Skip();
            return 0;
        }

        public override void Write(YamlWriter writer, int value)
            => writer.WriteScalar("0");
    }
}
