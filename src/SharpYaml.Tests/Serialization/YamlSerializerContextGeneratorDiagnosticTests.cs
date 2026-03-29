#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;
using SharpYaml.SourceGeneration;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlSerializerContextGeneratorDiagnosticTests
{
    private static readonly string[] NullableWarningIds = ["CS8600", "CS8601", "CS8602", "CS8603", "CS8604", "CS8618"];

    private static readonly ImmutableDictionary<string, ReportDiagnostic> NullableWarningsAsErrors =
        NullableWarningIds.ToImmutableDictionary(static id => id, static _ => ReportDiagnostic.Error);

    [TestMethod]
    public void GeneratorWarnsWhenJsonSerializableIsUsedOnYamlSerializerContext()
    {
        const string source = """
            using System.Text.Json.Serialization;
            using SharpYaml.Serialization;

            [JsonSerializable(typeof(int))]
            internal partial class LegacyYamlContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SHARPYAML007")
            .ToArray();

        Assert.AreEqual(1, diagnostics.Length);
        Assert.AreEqual(DiagnosticSeverity.Warning, diagnostics[0].Severity);
        StringAssert.Contains(diagnostics[0].GetMessage(), "[JsonSerializable]");
        StringAssert.Contains(diagnostics[0].GetMessage(), "[YamlSerializable]");
    }

    [TestMethod]
    public void GeneratorDoesNotReportNullableWarningsForMissingNullableInitOnlyProperties()
    {
        const string source = """
            #nullable enable

            using SharpYaml.Serialization;

            public sealed class AppOptions
            {
                public string? NullableMock { get; init; }

                public string NonNullableMock { get; init; } = string.Empty;
            }

            [YamlSerializable(typeof(AppOptions))]
            internal partial class AppOptionsYamlContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var nullableDiagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Where(diagnostic => NullableWarningIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(0, nullableDiagnostics.Length, string.Join(Environment.NewLine, nullableDiagnostics.Select(static diagnostic => diagnostic.ToString())));
        StringAssert.Contains(result.GeneratedSource, "NullableMock");
        StringAssert.Contains(result.GeneratedSource, "NonNullableMock");
    }

    [TestMethod]
    public void GeneratorReportsErrorForNonAssignableDerivedTypeMapping()
    {
        const string source = """
            using SharpYaml.Serialization;

            public abstract class Animal
            {
            }

            public sealed class Rock
            {
            }

            [YamlSerializable(typeof(Animal))]
            [YamlDerivedTypeMapping(typeof(Animal), typeof(Rock), "rock")]
            internal partial class InvalidMappingContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SHARPYAML020")
            .ToArray();

        Assert.AreEqual(1, diagnostics.Length);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
        StringAssert.Contains(diagnostics[0].GetMessage(), "Rock");
        StringAssert.Contains(diagnostics[0].GetMessage(), "Animal");
    }

    [TestMethod]
    public void GeneratorWarnsWhenDerivedTypeMappingBaseUsesSerializerDefaults()
    {
        const string source = """
            using SharpYaml.Serialization;

            public abstract class Animal
            {
                public string Name { get; set; } = string.Empty;
            }

            public sealed class Dog : Animal
            {
                public int BarkVolume { get; set; }
            }

            [YamlSerializable(typeof(Animal))]
            [YamlDerivedTypeMapping(typeof(Animal), typeof(Dog), "dog")]
            internal partial class MappingContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "SHARPYAML021")
            .ToArray();

        Assert.AreEqual(1, diagnostics.Length);
        Assert.AreEqual(DiagnosticSeverity.Warning, diagnostics[0].Severity);
        StringAssert.Contains(diagnostics[0].GetMessage(), "Animal");
        StringAssert.Contains(result.GeneratedSource, "value is global::Dog");
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenContextLevelConverterHandlesMemberType()
    {
        const string source = """
            using SharpYaml.Serialization;

            public sealed class CustomType
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class CustomTypeConverter : YamlConverter<CustomType>
            {
                public override CustomType Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new CustomType { Value = value };
                }

                public override void Write(YamlWriter writer, CustomType value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            public sealed class ModelWithCustomType
            {
                public CustomType? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithCustomType))]
            [YamlSourceGenerationOptions(Converters = [typeof(CustomTypeConverter)])]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenMemberHasYamlConverterAttribute()
    {
        const string source = """
            using SharpYaml.Serialization;

            public sealed class CustomType
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class CustomTypeConverter : YamlConverter<CustomType>
            {
                public override CustomType Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new CustomType { Value = value };
                }

                public override void Write(YamlWriter writer, CustomType value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            public sealed class ModelWithConverterOnMember
            {
                [YamlConverter(typeof(CustomTypeConverter))]
                public CustomType? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithConverterOnMember))]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenTypeHasYamlConverterAttribute()
    {
        const string source = """
            using SharpYaml.Serialization;

            public sealed class TypeLevelConverter : YamlConverter<ConverterDecoratedType>
            {
                public override ConverterDecoratedType Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new ConverterDecoratedType { Value = value };
                }

                public override void Write(YamlWriter writer, ConverterDecoratedType value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            [YamlConverter(typeof(TypeLevelConverter))]
            public sealed class ConverterDecoratedType
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class ModelWithTypeLevelConverter
            {
                public ConverterDecoratedType? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithTypeLevelConverter))]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenContextConverterHandlesArrayElementType()
    {
        const string source = """
            using System.Collections.Generic;
            using SharpYaml.Serialization;

            public sealed class CustomType
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class CustomTypeConverter : YamlConverter<CustomType>
            {
                public override CustomType Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new CustomType { Value = value };
                }

                public override void Write(YamlWriter writer, CustomType value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            public sealed class ModelWithList
            {
                public List<CustomType>? Items { get; set; }
            }

            [YamlSerializable(typeof(ModelWithList))]
            [YamlSourceGenerationOptions(Converters = [typeof(CustomTypeConverter)])]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenContextConverterHandlesDictionaryValueType()
    {
        const string source = """
            using System.Collections.Generic;
            using SharpYaml.Serialization;

            public sealed class CustomType
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class CustomTypeConverter : YamlConverter<CustomType>
            {
                public override CustomType Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new CustomType { Value = value };
                }

                public override void Write(YamlWriter writer, CustomType value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            public sealed class ModelWithDictionary
            {
                public Dictionary<string, CustomType>? Items { get; set; }
            }

            [YamlSerializable(typeof(ModelWithDictionary))]
            [YamlSourceGenerationOptions(Converters = [typeof(CustomTypeConverter)])]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    [TestMethod]
    public void SHARPYAML002_StillFires_WhenNoConverterHandlesType()
    {
        const string source = """
            using SharpYaml.Serialization;

            public sealed class UnhandledType
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class ModelWithUnhandledType
            {
                public UnhandledType? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithUnhandledType))]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(1, diagnostics.Length);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostics[0].Severity);
        StringAssert.Contains(diagnostics[0].GetMessage(), "UnhandledType");
    }

    [TestMethod]
    public void SHARPYAML002_StillFires_WhenConverterHandlesDifferentType()
    {
        const string source = """
            using SharpYaml.Serialization;

            public sealed class TypeA
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class TypeB
            {
                public string Value { get; set; } = string.Empty;
            }

            public sealed class TypeAConverter : YamlConverter<TypeA>
            {
                public override TypeA Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new TypeA { Value = value };
                }

                public override void Write(YamlWriter writer, TypeA value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            public sealed class ModelWithTypeB
            {
                public TypeB? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithTypeB))]
            [YamlSourceGenerationOptions(Converters = [typeof(TypeAConverter)])]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(1, diagnostics.Length);
        StringAssert.Contains(diagnostics[0].GetMessage(), "TypeB");
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenConverterInheritsFromAnotherConverter()
    {
        const string source = """
            using SharpYaml.Serialization;

            public sealed class CustomType
            {
                public string Value { get; set; } = string.Empty;
            }

            public class BaseCustomTypeConverter : YamlConverter<CustomType>
            {
                public override CustomType Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new CustomType { Value = value };
                }

                public override void Write(YamlWriter writer, CustomType value)
                {
                    writer.WriteScalar(value.Value);
                }
            }

            public sealed class DerivedCustomTypeConverter : BaseCustomTypeConverter
            {
            }

            public sealed class ModelWithCustomType
            {
                public CustomType? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithCustomType))]
            [YamlSourceGenerationOptions(Converters = [typeof(DerivedCustomTypeConverter)])]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    [TestMethod]
    public void SHARPYAML002_IsSuppressed_WhenContextConverterHandlesNullableValueType()
    {
        const string source = """
            using SharpYaml.Serialization;

            public struct CustomStruct
            {
                public int Value { get; set; }
            }

            public sealed class CustomStructConverter : YamlConverter<CustomStruct>
            {
                public override CustomStruct Read(YamlReader reader)
                {
                    var value = reader.GetScalarValue();
                    reader.Read();
                    return new CustomStruct { Value = int.Parse(value) };
                }

                public override void Write(YamlWriter writer, CustomStruct value)
                {
                    writer.WriteScalar(value.Value.ToString());
                }
            }

            public sealed class ModelWithNullableStruct
            {
                public CustomStruct? Item { get; set; }
            }

            [YamlSerializable(typeof(ModelWithNullableStruct))]
            [YamlSourceGenerationOptions(Converters = [typeof(CustomStructConverter)])]
            internal partial class TestContext : YamlSerializerContext
            {
            }
            """;

        var result = RunGenerator(source);

        var diagnostics = result.Diagnostics
            .Where(static d => d.Id == "SHARPYAML002")
            .ToArray();

        Assert.AreEqual(0, diagnostics.Length, string.Join(Environment.NewLine, diagnostics.Select(static d => d.GetMessage())));
    }

    private static (Compilation OutputCompilation, Diagnostic[] Diagnostics, string GeneratedSource) RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorWarningTests",
            syntaxTrees: new[] { syntaxTree },
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable)
                .WithSpecificDiagnosticOptions(NullableWarningsAsErrors));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new YamlSerializerContextGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var generatedSource = string.Join(
            Environment.NewLine,
            driver.GetRunResult().Results
                .SelectMany(static result => result.GeneratedSources)
                .Select(static generatedSourceResult => generatedSourceResult.SourceText.ToString()));

        return (outputCompilation, generatorDiagnostics.Concat(outputCompilation.GetDiagnostics()).ToArray(), generatedSource);
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var platformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return platformAssemblies
            .Concat(
            [
                typeof(object).Assembly.Location,
                typeof(JsonSerializableAttribute).Assembly.Location,
                typeof(YamlSerializerContext).Assembly.Location,
                typeof(YamlSerializerContextGenerator).Assembly.Location,
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
