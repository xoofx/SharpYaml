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
