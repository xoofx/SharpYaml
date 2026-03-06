#nullable enable

using System;
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

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorWarningTests",
            syntaxTrees: new[] { syntaxTree },
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new YamlSerializerContextGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var diagnostics = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Id == "SHARPYAML007")
            .ToArray();

        Assert.AreEqual(1, diagnostics.Length);
        Assert.AreEqual(DiagnosticSeverity.Warning, diagnostics[0].Severity);
        StringAssert.Contains(diagnostics[0].GetMessage(), "[JsonSerializable]");
        StringAssert.Contains(diagnostics[0].GetMessage(), "[YamlSerializable]");
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
