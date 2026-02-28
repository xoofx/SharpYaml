using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharpYaml.SourceGeneration;

[Generator]
public sealed class YamlSerializerContextGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor ContextMustBePartial = new(
        id: "SHARPYAML001",
        title: "Yaml serializer context must be partial",
        messageFormat: "Type '{0}' derives from SharpYaml.Serialization.YamlSerializerContext and must be declared partial to support source generation",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private sealed class SourceGenerationOptionsModel
    {
        public bool? WriteIndented { get; set; }

        public int? IndentSize { get; set; }

        public bool? PropertyNameCaseInsensitive { get; set; }

        public string? DefaultIgnoreCondition { get; set; }

        public string? PropertyNamingPolicy { get; set; }

        public string? DictionaryKeyPolicy { get; set; }
    }

    private sealed class ContextModel
    {
        public ContextModel(
            INamedTypeSymbol contextSymbol,
            string namespaceName,
            string typeName,
            ImmutableArray<string> serializableTypes,
            SourceGenerationOptionsModel sourceGenerationOptions,
            bool isValid)
        {
            ContextSymbol = contextSymbol;
            NamespaceName = namespaceName;
            TypeName = typeName;
            SerializableTypes = serializableTypes;
            SourceGenerationOptions = sourceGenerationOptions;
            IsValid = isValid;
        }

        public INamedTypeSymbol ContextSymbol { get; }

        public string NamespaceName { get; }

        public string TypeName { get; }

        public ImmutableArray<string> SerializableTypes { get; }

        public SourceGenerationOptionsModel SourceGenerationOptions { get; }

        public bool IsValid { get; }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateContexts = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
            static (syntaxContext, _) => TryCreateContextModel(syntaxContext))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        context.RegisterSourceOutput(candidateContexts.Collect(), static (sourceProductionContext, models) =>
        {
            var byMetadataName = new Dictionary<string, ContextModel>(StringComparer.Ordinal);
            foreach (var model in models)
            {
                byMetadataName[model.ContextSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)] = model;
            }

            foreach (var model in byMetadataName.Values)
            {
                EmitContext(sourceProductionContext, model);
            }
        });
    }

    private static ContextModel? TryCreateContextModel(GeneratorSyntaxContext syntaxContext)
    {
        if (syntaxContext.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        if (syntaxContext.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        if (!DerivesFromYamlSerializerContext(classSymbol))
        {
            return null;
        }

        var serializableTypes = ImmutableArray.CreateBuilder<string>();
        var sourceGenerationOptions = new SourceGenerationOptionsModel();
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (IsJsonSerializableAttribute(attribute))
            {
                if (attribute.ConstructorArguments.Length != 1)
                {
                    continue;
                }

                var argument = attribute.ConstructorArguments[0];
                if (argument.Kind != TypedConstantKind.Type || argument.Value is not ITypeSymbol typeSymbol)
                {
                    continue;
                }

                serializableTypes.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                continue;
            }

            if (IsJsonSourceGenerationOptionsAttribute(attribute))
            {
                ApplySourceGenerationOptionsAttribute(attribute, sourceGenerationOptions);
            }
        }

        if (serializableTypes.Count == 0)
        {
            return null;
        }

        var isPartial = classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));
        var containingNamespace = classSymbol.ContainingNamespace;
        var namespaceName = containingNamespace.IsGlobalNamespace ? string.Empty : containingNamespace.ToDisplayString();
        return new ContextModel(
            classSymbol,
            namespaceName,
            classSymbol.Name,
            serializableTypes.Distinct(StringComparer.Ordinal).ToImmutableArray(),
            sourceGenerationOptions,
            isPartial);
    }

    private static bool IsJsonSerializableAttribute(AttributeData attribute)
    {
        return string.Equals(
            attribute.AttributeClass?.ToDisplayString(),
            "System.Text.Json.Serialization.JsonSerializableAttribute",
            StringComparison.Ordinal);
    }

    private static bool IsJsonSourceGenerationOptionsAttribute(AttributeData attribute)
    {
        return string.Equals(
            attribute.AttributeClass?.ToDisplayString(),
            "System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute",
            StringComparison.Ordinal);
    }

    private static void ApplySourceGenerationOptionsAttribute(AttributeData attribute, SourceGenerationOptionsModel options)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            switch (namedArgument.Key)
            {
                case "WriteIndented":
                    options.WriteIndented = GetBooleanValue(namedArgument.Value);
                    break;
                case "IndentSize":
                    options.IndentSize = GetIntValue(namedArgument.Value);
                    break;
                case "PropertyNameCaseInsensitive":
                    options.PropertyNameCaseInsensitive = GetBooleanValue(namedArgument.Value);
                    break;
                case "DefaultIgnoreCondition":
                    options.DefaultIgnoreCondition = MapJsonIgnoreCondition(namedArgument.Value);
                    break;
                case "PropertyNamingPolicy":
                    options.PropertyNamingPolicy = MapJsonKnownNamingPolicy(namedArgument.Value);
                    break;
                case "DictionaryKeyPolicy":
                    options.DictionaryKeyPolicy = MapJsonKnownNamingPolicy(namedArgument.Value);
                    break;
            }
        }
    }

    private static bool? GetBooleanValue(TypedConstant constant)
    {
        return constant.Value is bool value ? value : null;
    }

    private static int? GetIntValue(TypedConstant constant)
    {
        return constant.Value is int value ? value : null;
    }

    private static string? MapJsonIgnoreCondition(TypedConstant constant)
    {
        if (constant.Value is not int value)
        {
            return null;
        }

        return value switch
        {
            0 => "Never",
            2 => "WhenWritingDefault",
            3 => "WhenWritingNull",
            _ => null,
        };
    }

    private static string? MapJsonKnownNamingPolicy(TypedConstant constant)
    {
        if (constant.Value is not int value)
        {
            return null;
        }

        return value switch
        {
            1 => "CamelCase",
            _ => null,
        };
    }

    private static bool DerivesFromYamlSerializerContext(INamedTypeSymbol symbol)
    {
        var current = symbol;
        while (current is not null)
        {
            if (string.Equals(current.ToDisplayString(), "SharpYaml.Serialization.YamlSerializerContext", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void EmitContext(SourceProductionContext context, ContextModel model)
    {
        if (!model.IsValid)
        {
            var location = model.ContextSymbol.Locations.Length > 0 ? model.ContextSymbol.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(ContextMustBePartial, location, model.ContextSymbol.Name));
            return;
        }

        var source = GenerateContextSource(model);
        var hintName = $"{model.TypeName}.g.cs";
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateContextSource(ContextModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using global::System;");
        builder.AppendLine();

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            builder.Append("namespace ").Append(model.NamespaceName).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("partial class ").Append(model.TypeName).AppendLine();
        builder.AppendLine("{");

        builder.Append("    public static ").Append(model.TypeName).AppendLine(" Default { get; } = CreateDefaultContext();");
        builder.AppendLine();
        builder.Append("    private static ").Append(model.TypeName).AppendLine(" CreateDefaultContext()");
        builder.AppendLine("    {");
        builder.Append("        var context = new ").Append(model.TypeName).AppendLine("();");
        builder.AppendLine("        ApplySourceGenerationOptions(context.Options);");
        builder.AppendLine("        return context;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static void ApplySourceGenerationOptions(global::SharpYaml.YamlSerializerOptions options)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(options);");
        AppendOptionAssignments(builder, model.SourceGenerationOptions);
        builder.AppendLine("    }");
        builder.AppendLine();

        for (var index = 0; index < model.SerializableTypes.Length; index++)
        {
            var serializableType = model.SerializableTypes[index];
            builder.Append("    private global::SharpYaml.YamlTypeInfo<").Append(serializableType).Append(">? _typeInfo").Append(index).AppendLine(";");
        }

        builder.AppendLine();
        builder.AppendLine("    public override global::SharpYaml.YamlTypeInfo? GetTypeInfo(global::System.Type type, global::SharpYaml.YamlSerializerOptions options)");
        builder.AppendLine("    {");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(type);");
        builder.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(options);");
        builder.AppendLine();

        for (var index = 0; index < model.SerializableTypes.Length; index++)
        {
            var serializableType = model.SerializableTypes[index];
            builder.Append("        if (type == typeof(").Append(serializableType).AppendLine("))");
            builder.AppendLine("        {");
            builder.Append("            return GetTypeInfo").Append(index).AppendLine("(options);");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        builder.AppendLine("        return null;");
        builder.AppendLine("    }");
        builder.AppendLine();

        for (var index = 0; index < model.SerializableTypes.Length; index++)
        {
            var serializableType = model.SerializableTypes[index];
            builder.Append("    public global::SharpYaml.YamlTypeInfo<").Append(serializableType).Append("> TypeInfo").Append(index).AppendLine();
            builder.AppendLine("    {");
            builder.Append("        get => _typeInfo").Append(index).Append(" ??= new GeneratedTypeInfo").Append(index).AppendLine("(Options);");
            builder.AppendLine("    }");
            builder.AppendLine();

            builder.Append("    private global::SharpYaml.YamlTypeInfo<").Append(serializableType).Append("> GetTypeInfo").Append(index).AppendLine("(global::SharpYaml.YamlSerializerOptions options)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (global::System.Object.ReferenceEquals(options, Options))");
            builder.AppendLine("        {");
            builder.Append("            return TypeInfo").Append(index).AppendLine(";");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.Append("        return new GeneratedTypeInfo").Append(index).AppendLine("(options);");
            builder.AppendLine("    }");
            builder.AppendLine();

            builder.Append("    private sealed class GeneratedTypeInfo").Append(index).Append(" : global::SharpYaml.YamlTypeInfo<").Append(serializableType).AppendLine(">");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly global::SharpYaml.YamlTypeInfo _inner;");
            builder.AppendLine();
            builder.Append("        public GeneratedTypeInfo").Append(index).AppendLine("(global::SharpYaml.YamlSerializerOptions options) : base(options)");
            builder.AppendLine("        {");
            builder.Append("            _inner = global::SharpYaml.ReflectionYamlTypeInfoResolver.Default.GetTypeInfo(typeof(").Append(serializableType).AppendLine("), options)");
            builder.AppendLine("                ?? throw new global::System.InvalidOperationException(\"Generated metadata could not be created for the requested type.\");");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.Append("        public override string Serialize(").Append(serializableType).AppendLine(" value)");
            builder.AppendLine("        {");
            builder.AppendLine("            return _inner.SerializeAsString(value);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.Append("        public override ").Append(serializableType).AppendLine("? Deserialize(string yaml)");
            builder.AppendLine("        {");
            builder.Append("            return (").Append(serializableType).AppendLine("?)_inner.DeserializeFromString(yaml);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendOptionAssignments(StringBuilder builder, SourceGenerationOptionsModel options)
    {
        if (options.WriteIndented.HasValue)
        {
            builder.Append("        options.WriteIndented = ")
                .Append(options.WriteIndented.Value ? "true" : "false")
                .AppendLine(";");
        }

        if (options.IndentSize.HasValue)
        {
            builder.Append("        options.IndentSize = ")
                .Append(options.IndentSize.Value)
                .AppendLine(";");
        }

        if (options.PropertyNameCaseInsensitive.HasValue)
        {
            builder.Append("        options.PropertyNameCaseInsensitive = ")
                .Append(options.PropertyNameCaseInsensitive.Value ? "true" : "false")
                .AppendLine(";");
        }

        if (!string.IsNullOrEmpty(options.DefaultIgnoreCondition))
        {
            builder.Append("        options.DefaultIgnoreCondition = global::SharpYaml.YamlIgnoreCondition.")
                .Append(options.DefaultIgnoreCondition)
                .AppendLine(";");
        }

        if (!string.IsNullOrEmpty(options.PropertyNamingPolicy))
        {
            builder.Append("        options.PropertyNamingPolicy = global::SharpYaml.YamlNamingPolicy.")
                .Append(options.PropertyNamingPolicy)
                .AppendLine(";");
        }

        if (!string.IsNullOrEmpty(options.DictionaryKeyPolicy))
        {
            builder.Append("        options.DictionaryKeyPolicy = global::SharpYaml.YamlNamingPolicy.")
                .Append(options.DictionaryKeyPolicy)
                .AppendLine(";");
        }
    }
}
