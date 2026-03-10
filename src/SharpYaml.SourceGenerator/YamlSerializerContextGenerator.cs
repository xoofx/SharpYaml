using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    private static readonly DiagnosticDescriptor UnsupportedMemberType = new(
        id: "SHARPYAML002",
        title: "Unsupported member type",
        messageFormat: "Type '{0}' contains member '{1}' of unsupported type '{2}'. Add [YamlSerializable(typeof({2}))] to the context or change the member type.",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedExtensionDataMember = new(
        id: "SHARPYAML003",
        title: "Unsupported extension data member",
        messageFormat: "Type '{0}' contains extension data member '{1}' of unsupported type '{2}'. Extension data members must be 'IDictionary<string, object>', 'IDictionary<string, SharpYaml.Model.YamlNode>', or 'SharpYaml.Model.YamlMapping'.",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleExtensionDataMembers = new(
        id: "SHARPYAML004",
        title: "Multiple extension data members",
        messageFormat: "Type '{0}' contains multiple extension data members. Only one member can be annotated with [YamlExtensionData] or [JsonExtensionData].",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSourceGenerationOption = new(
        id: "SHARPYAML005",
        title: "Invalid source generation option",
        messageFormat: "Invalid source generation option on context '{0}': {1}",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidConverterType = new(
        id: "SHARPYAML006",
        title: "Invalid converter type",
        messageFormat: "Converter type '{0}' is invalid: {1}",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor JsonSerializableOnYamlSerializerContext = new(
        id: "SHARPYAML007",
        title: "JsonSerializable is not valid on YamlSerializerContext",
        messageFormat: "Context '{0}' uses [JsonSerializable] for '{1}'. Replace it with [YamlSerializable].",
        category: "SharpYaml.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private sealed class SourceGenerationOptionsModel
    {
        public bool? WriteIndented { get; set; }
        public int? IndentSize { get; set; }
        public bool? PropertyNameCaseInsensitive { get; set; }
        public string? DefaultIgnoreCondition { get; set; }
        public string? PropertyNamingPolicy { get; set; }
        public string? DictionaryKeyPolicy { get; set; }
        public string? MappingOrder { get; set; }
        public string? Schema { get; set; }
        public bool? UseSchema { get; set; }
        public string? DuplicateKeyHandling { get; set; }
        public bool? UnsafeAllowDeserializeFromTagTypeName { get; set; }
        public string? ReferenceHandling { get; set; }
        public string? SourceName { get; set; }
        public bool? PreferPlainStyle { get; set; }
        public bool? PreferQuotedForAmbiguousScalars { get; set; }
        public string? DiscriminatorStyle { get; set; }
        public string? TypeDiscriminatorPropertyName { get; set; }
        public string? UnknownDerivedTypeHandling { get; set; }
        public ImmutableArray<ITypeSymbol> ConverterTypes { get; set; } = ImmutableArray<ITypeSymbol>.Empty;

        public void ApplyFrom(SourceGenerationOptionsModel other)
        {
            if (other.WriteIndented.HasValue) WriteIndented = other.WriteIndented;
            if (other.IndentSize.HasValue) IndentSize = other.IndentSize;
            if (other.PropertyNameCaseInsensitive.HasValue) PropertyNameCaseInsensitive = other.PropertyNameCaseInsensitive;
            if (!string.IsNullOrEmpty(other.DefaultIgnoreCondition)) DefaultIgnoreCondition = other.DefaultIgnoreCondition;
            if (!string.IsNullOrEmpty(other.PropertyNamingPolicy)) PropertyNamingPolicy = other.PropertyNamingPolicy;
            if (!string.IsNullOrEmpty(other.DictionaryKeyPolicy)) DictionaryKeyPolicy = other.DictionaryKeyPolicy;
            if (!string.IsNullOrEmpty(other.MappingOrder)) MappingOrder = other.MappingOrder;
            if (!string.IsNullOrEmpty(other.Schema)) Schema = other.Schema;
            if (other.UseSchema.HasValue) UseSchema = other.UseSchema;
            if (!string.IsNullOrEmpty(other.DuplicateKeyHandling)) DuplicateKeyHandling = other.DuplicateKeyHandling;
            if (other.UnsafeAllowDeserializeFromTagTypeName.HasValue) UnsafeAllowDeserializeFromTagTypeName = other.UnsafeAllowDeserializeFromTagTypeName;
            if (!string.IsNullOrEmpty(other.ReferenceHandling)) ReferenceHandling = other.ReferenceHandling;
            if (other.SourceName is not null) SourceName = other.SourceName;
            if (other.PreferPlainStyle.HasValue) PreferPlainStyle = other.PreferPlainStyle;
            if (other.PreferQuotedForAmbiguousScalars.HasValue) PreferQuotedForAmbiguousScalars = other.PreferQuotedForAmbiguousScalars;
            if (!string.IsNullOrEmpty(other.DiscriminatorStyle)) DiscriminatorStyle = other.DiscriminatorStyle;
            if (other.TypeDiscriminatorPropertyName is not null) TypeDiscriminatorPropertyName = other.TypeDiscriminatorPropertyName;
            if (!string.IsNullOrEmpty(other.UnknownDerivedTypeHandling)) UnknownDerivedTypeHandling = other.UnknownDerivedTypeHandling;
            if (!other.ConverterTypes.IsDefaultOrEmpty) ConverterTypes = other.ConverterTypes;
        }
    }

    private sealed class ContextModel
    {
        public ContextModel(
            INamedTypeSymbol contextSymbol,
            string namespaceName,
            string typeName,
            ImmutableArray<SerializableTypeModel> serializableTypes,
            ImmutableArray<LegacyJsonSerializableUsageModel> legacyJsonSerializableUsages,
            SourceGenerationOptionsModel sourceGenerationOptions,
            bool isValid)
        {
            ContextSymbol = contextSymbol;
            NamespaceName = namespaceName;
            TypeName = typeName;
            SerializableTypes = serializableTypes;
            LegacyJsonSerializableUsages = legacyJsonSerializableUsages;
            SourceGenerationOptions = sourceGenerationOptions;
            IsValid = isValid;
        }

        public INamedTypeSymbol ContextSymbol { get; }
        public string NamespaceName { get; }
        public string TypeName { get; }
        public ImmutableArray<SerializableTypeModel> SerializableTypes { get; }
        public ImmutableArray<LegacyJsonSerializableUsageModel> LegacyJsonSerializableUsages { get; }
        public SourceGenerationOptionsModel SourceGenerationOptions { get; }
        public bool IsValid { get; }
    }

    private sealed class SerializableTypeModel
    {
        public SerializableTypeModel(ITypeSymbol typeSymbol, string? typeInfoPropertyName)
        {
            TypeSymbol = typeSymbol;
            TypeInfoPropertyName = typeInfoPropertyName;
        }

        public ITypeSymbol TypeSymbol { get; }
        public string? TypeInfoPropertyName { get; }
    }

    private sealed class LegacyJsonSerializableUsageModel
    {
        public LegacyJsonSerializableUsageModel(Location? location, ITypeSymbol typeSymbol)
        {
            Location = location;
            TypeSymbol = typeSymbol;
        }

        public Location? Location { get; }
        public ITypeSymbol TypeSymbol { get; }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateContexts = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
                static (syntaxContext, _) => TryCreateContextModel(syntaxContext))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        var compilationAndModels = context.CompilationProvider.Combine(candidateContexts.Collect());
        context.RegisterSourceOutput(compilationAndModels, static (spc, input) =>
        {
            var compilation = input.Left;
            var models = input.Right;

            var byMetadataName = new Dictionary<string, ContextModel>(models.Length, StringComparer.Ordinal);
            foreach (var model in models)
            {
                byMetadataName[model.ContextSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)] = model;
            }

            foreach (var model in byMetadataName.Values)
            {
                EmitContext(spc, compilation, model);
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

        var serializableTypes = ImmutableArray.CreateBuilder<SerializableTypeModel>();
        var legacyJsonSerializableUsages = ImmutableArray.CreateBuilder<LegacyJsonSerializableUsageModel>();
        var jsonSourceGenerationOptions = new SourceGenerationOptionsModel();
        var yamlSourceGenerationOptions = new SourceGenerationOptionsModel();
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (TryCreateSerializableTypeModel(attribute, out var serializableType))
            {
                serializableTypes.Add(serializableType);
                if (IsJsonSerializableAttribute(attribute))
                {
                    legacyJsonSerializableUsages.Add(new LegacyJsonSerializableUsageModel(
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        serializableType.TypeSymbol));
                }
                continue;
            }

            if (IsJsonSourceGenerationOptionsAttribute(attribute))
            {
                ApplyJsonSourceGenerationOptionsAttribute(attribute, jsonSourceGenerationOptions);
                continue;
            }

            if (IsYamlSourceGenerationOptionsAttribute(attribute))
            {
                ApplyYamlSourceGenerationOptionsAttribute(attribute, yamlSourceGenerationOptions);
            }
        }

        if (serializableTypes.Count == 0)
        {
            return null;
        }

        var sourceGenerationOptions = new SourceGenerationOptionsModel();
        sourceGenerationOptions.ApplyFrom(jsonSourceGenerationOptions);
        sourceGenerationOptions.ApplyFrom(yamlSourceGenerationOptions);

        var isPartial = classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
        var containingNamespace = classSymbol.ContainingNamespace;
        var namespaceName = containingNamespace is { IsGlobalNamespace: false } ? containingNamespace.ToDisplayString() : string.Empty;
        var typeName = classSymbol.Name;

        return new ContextModel(
            classSymbol,
            namespaceName,
            typeName,
            serializableTypes.ToImmutable(),
            legacyJsonSerializableUsages.ToImmutable(),
            sourceGenerationOptions,
            isValid: isPartial);
    }

    private static void EmitContext(SourceProductionContext context, Compilation compilation, ContextModel model)
    {
        if (!model.IsValid)
        {
            context.ReportDiagnostic(Diagnostic.Create(ContextMustBePartial, model.ContextSymbol.Locations.FirstOrDefault(), model.ContextSymbol.ToDisplayString()));
            return;
        }

        for (var i = 0; i < model.LegacyJsonSerializableUsages.Length; i++)
        {
            var usage = model.LegacyJsonSerializableUsages[i];
            context.ReportDiagnostic(Diagnostic.Create(
                JsonSerializableOnYamlSerializerContext,
                usage.Location ?? model.ContextSymbol.Locations.FirstOrDefault(),
                model.ContextSymbol.ToDisplayString(),
                usage.TypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        var resolvedTypes = ExpandSerializableTypes(model.SerializableTypes.Select(static item => item.TypeSymbol).ToImmutableArray());

        var indexByType = new Dictionary<ITypeSymbol, int>(resolvedTypes.Length, SymbolEqualityComparer.Default);
        for (var i = 0; i < resolvedTypes.Length; i++)
        {
            indexByType[resolvedTypes[i]] = i;
        }

        ValidateSourceGenerationOptions(context, compilation, model);

        // Validate that member types are generated as well (or are known scalars).
        for (var i = 0; i < resolvedTypes.Length; i++)
        {
            if (resolvedTypes[i] is not INamedTypeSymbol named || (named.TypeKind != TypeKind.Class && named.TypeKind != TypeKind.Struct))
            {
                continue;
            }

            var extensionDataMembers = GetExtensionDataMembers(named);
            if (extensionDataMembers.Length > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MultipleExtensionDataMembers,
                    named.Locations.FirstOrDefault(),
                    named.ToDisplayString()));
            }

            ISymbol? extensionDataMember = extensionDataMembers.Length == 1 ? extensionDataMembers[0] : null;
            if (extensionDataMember is not null)
            {
                var extensionType = GetMemberType(extensionDataMember);
                if (extensionType is null || !IsSupportedExtensionDataMemberType(extensionType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedExtensionDataMember,
                        extensionDataMember.Locations.FirstOrDefault(),
                        named.ToDisplayString(),
                        extensionDataMember.Name,
                        extensionType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "<unknown>"));
                }
            }

            foreach (var member in GetSerializableMembers(named))
            {
                if (extensionDataMember is not null && SymbolEqualityComparer.Default.Equals(member, extensionDataMember))
                {
                    continue;
                }

                var memberType = GetMemberType(member);
                if (memberType is null)
                {
                    continue;
                }

                if (IsKnownScalar(memberType))
                {
                    continue;
                }

                if (TryGetArrayElementType(memberType, out var arrayElementType) ||
                    TryGetSequenceElementType(memberType, out arrayElementType, out _))
                {
                    if (IsKnownScalar(arrayElementType) || indexByType.ContainsKey(arrayElementType))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMemberType,
                        member.Locations.FirstOrDefault(),
                        named.ToDisplayString(),
                        member.Name,
                        arrayElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    continue;
                }

                if (TryGetDictionaryTypes(memberType, out var dictionaryKeyType, out var dictionaryValueType, out _))
                {
                    if (!IsSupportedDictionaryKeyType(dictionaryKeyType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            UnsupportedMemberType,
                            member.Locations.FirstOrDefault(),
                            named.ToDisplayString(),
                            member.Name,
                            memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                        continue;
                    }

                    if (IsKnownScalar(dictionaryValueType) || indexByType.ContainsKey(dictionaryValueType))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMemberType,
                        member.Locations.FirstOrDefault(),
                        named.ToDisplayString(),
                        member.Name,
                        dictionaryValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    continue;
                }

                if (!indexByType.ContainsKey(memberType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedMemberType,
                        member.Locations.FirstOrDefault(),
                        named.ToDisplayString(),
                        member.Name,
                        memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
            }
        }

        var source = GenerateContextSource(model, resolvedTypes, indexByType);
        context.AddSource($"{model.TypeName}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static void ValidateSourceGenerationOptions(SourceProductionContext context, Compilation compilation, ContextModel model)
    {
        var location = model.ContextSymbol.Locations.FirstOrDefault();
        var options = model.SourceGenerationOptions;

        if (options.IndentSize is < 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSourceGenerationOption,
                location,
                model.ContextSymbol.ToDisplayString(),
                $"{nameof(options.IndentSize)} must be at least 1."));
        }

        if (string.Equals(options.DiscriminatorStyle, "Unspecified", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidSourceGenerationOption,
                location,
                model.ContextSymbol.ToDisplayString(),
                $"{nameof(options.DiscriminatorStyle)} cannot be Unspecified."));
        }

        if (options.ConverterTypes.IsDefaultOrEmpty)
        {
            return;
        }

        var yamlConverterSymbol = compilation.GetTypeByMetadataName("SharpYaml.Serialization.YamlConverter");
        if (yamlConverterSymbol is null)
        {
            return;
        }

        for (var i = 0; i < options.ConverterTypes.Length; i++)
        {
            var converterType = options.ConverterTypes[i];
            if (converterType is not INamedTypeSymbol named)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    converterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "Converter types must be named types."));
                continue;
            }

            if (named.TypeKind != TypeKind.Class && named.TypeKind != TypeKind.Struct)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "Converter types must be classes or structs."));
                continue;
            }

            if (named.IsAbstract)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "Converter types cannot be abstract."));
                continue;
            }

            if (named.IsUnboundGenericType || named.TypeArguments.Any(static arg => arg.TypeKind == TypeKind.TypeParameter))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "Converter types cannot be open generic types."));
                continue;
            }

            if (named.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "Converter types must be accessible to the generated context (public or internal)."));
                continue;
            }

            if (!DerivesFrom(named, yamlConverterSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    $"Converter types must derive from '{yamlConverterSymbol.ToDisplayString()}'."));
                continue;
            }

            if (!named.InstanceConstructors.Any(static ctor => ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidConverterType,
                    location,
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    "Converter types must provide a public or internal parameterless constructor."));
            }
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<ITypeSymbol> ExpandSerializableTypes(ImmutableArray<ITypeSymbol> roots)
    {
        // Always include explicitly declared root types. Additionally include polymorphic derived types
        // so generated polymorphism dispatch can call into their serializers without requiring explicit roots.
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<ITypeSymbol>();

        for (var i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (seen.Add(root))
            {
                builder.Add(root);
                queue.Enqueue(root);
            }
        }

        while (queue.Count != 0)
        {
            var type = queue.Dequeue();
            if (type is not INamedTypeSymbol named)
            {
                continue;
            }

            foreach (var derived in GetPolymorphicDerivedTypes(named))
            {
                if (seen.Add(derived))
                {
                    builder.Add(derived);
                    queue.Enqueue(derived);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static string GenerateContextSource(ContextModel model, ImmutableArray<ITypeSymbol> types, Dictionary<ITypeSymbol, int> indexByType)
    {
        var builder = new StringBuilder();
        var propertyNamingPolicy = ResolveJsonNamingPolicy(model.SourceGenerationOptions.PropertyNamingPolicy);
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable annotations");
        builder.AppendLine();
        builder.AppendLine("using System.Collections.Frozen;");
        builder.AppendLine("using System;");
        builder.AppendLine();

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            builder.Append("namespace ").Append(model.NamespaceName).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("partial class ").Append(model.TypeName).AppendLine();
        builder.AppendLine("{");
        builder.Append("    public static ").Append(model.TypeName).AppendLine(" Default { get; } = new(CreateDefaultOptions(), isDefault: true);");
        builder.AppendLine();
        if (!model.ContextSymbol.InstanceConstructors.Any(static ctor => ctor.Parameters.Length == 0 && !ctor.IsImplicitlyDeclared))
        {
            builder.Append("    public ").Append(model.TypeName).AppendLine("() : base() { }");
            builder.AppendLine();
        }

        builder.Append("    private ").Append(model.TypeName).AppendLine("(global::SharpYaml.YamlSerializerOptions options, bool isDefault) : base(options) { }");
        builder.AppendLine();
        builder.AppendLine("    private static global::SharpYaml.YamlSerializerOptions CreateDefaultOptions()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new global::SharpYaml.YamlSerializerOptions");
        builder.AppendLine("        {");
        AppendOptionAssignments(builder, model.SourceGenerationOptions);
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();

        var typeInfoPropertyNames = CreateTypeInfoPropertyNames(model, types);

        for (var index = 0; index < types.Length; index++)
        {
            var serializableType = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("    private global::SharpYaml.YamlTypeInfo<").Append(serializableType).Append(">? _typeInfo").Append(index).AppendLine(";");
        }

        builder.AppendLine();
        builder.AppendLine("    private static readonly global::System.Collections.Frozen.FrozenDictionary<global::System.Type, int> s_typeIndexByType =");
        builder.Append("        new global::System.Collections.Generic.Dictionary<global::System.Type, int>(").Append(types.Length).AppendLine(")");
        builder.AppendLine("        {");
        for (var index = 0; index < types.Length; index++)
        {
            var serializableType = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("            [typeof(").Append(serializableType).Append(")] = ").Append(index).AppendLine(",");
        }
        builder.AppendLine("        }.ToFrozenDictionary();");
        builder.AppendLine();
        builder.AppendLine("    public override global::SharpYaml.YamlTypeInfo? GetTypeInfo(global::System.Type type, global::SharpYaml.YamlSerializerOptions options)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (!global::System.Object.ReferenceEquals(options, Options))");
        builder.AppendLine("        {");
        builder.AppendLine("            throw new global::System.InvalidOperationException(");
        builder.AppendLine("                $\"The provided {nameof(global::SharpYaml.YamlSerializerOptions)} instance does not match the options associated with the context '{GetType()}'. \" +");
        builder.AppendLine("                $\"Use the overloads that accept a {nameof(global::SharpYaml.Serialization.YamlSerializerContext)} or a {nameof(global::SharpYaml.YamlTypeInfo)} directly.\");");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (!s_typeIndexByType.TryGetValue(type, out var index))");
        builder.AppendLine("        {");
        builder.AppendLine("            return null;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return index switch");
        builder.AppendLine("        {");
        for (var index = 0; index < types.Length; index++)
        {
            builder.Append("            ").Append(index).Append(" => ").Append(typeInfoPropertyNames[index]).AppendLine(",");
        }
        builder.AppendLine("            _ => null,");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();

        for (var index = 0; index < types.Length; index++)
        {
            var serializableType = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = typeInfoPropertyNames[index];
            builder.Append("    public global::SharpYaml.YamlTypeInfo<").Append(serializableType).Append("> ").Append(propertyName).AppendLine();
            builder.AppendLine("    {");
            builder.Append("        get => _typeInfo").Append(index).Append(" ??= new GeneratedTypeInfo").Append(index).AppendLine("(Options);");
            builder.AppendLine("    }");
            builder.AppendLine();

            builder.Append("    private sealed class GeneratedTypeInfo").Append(index).Append(" : global::SharpYaml.YamlTypeInfo<").Append(serializableType).AppendLine(">");
            builder.AppendLine("    {");
            builder.AppendLine("        public GeneratedTypeInfo" + index + "(global::SharpYaml.YamlSerializerOptions options) : base(options) { }");
            builder.AppendLine();
            builder.Append("        public override void Write(global::SharpYaml.Serialization.YamlWriter writer, ").Append(serializableType).AppendLine(" value)");
            builder.AppendLine("        {");
            builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(writer);");
            builder.Append("            WriteValue").Append(index).AppendLine("(writer, value);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.Append("        public override ").Append(serializableType).AppendLine(" Read(global::SharpYaml.Serialization.YamlReader reader)");
            builder.AppendLine("        {");
            builder.Append("            return ReadValue").Append(index).AppendLine("(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        for (var index = 0; index < types.Length; index++)
        {
            var typeName = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            EmitWriteValue(builder, index, types[index], typeName, indexByType, propertyNamingPolicy);
            builder.AppendLine();
            EmitReadValue(builder, index, types[index], typeName, indexByType, propertyNamingPolicy);
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void EmitWriteValue(StringBuilder builder, int index, ITypeSymbol typeSymbol, string typeName, Dictionary<ITypeSymbol, int> indexByType, JsonNamingPolicy? propertyNamingPolicy)
    {
        var attributeConverterTypeName = GetYamlConverterAttributeTypeName((ISymbol)typeSymbol);

        builder.Append("    private static void WriteValue").Append(index)
            .Append("(global::SharpYaml.Serialization.YamlWriter writer, ").Append(typeName).AppendLine(" value)");
        builder.AppendLine("    {");
        builder.AppendLine("        var options = writer.Options;");
        builder.AppendLine("        var hasCustomConverters = options.Converters.Count != 0;");
        builder.AppendLine();

        if (attributeConverterTypeName is not null)
        {
            if (typeSymbol.IsReferenceType)
            {
                builder.AppendLine("        if (value is null)");
                builder.AppendLine("        {");
                builder.AppendLine("            writer.WriteNullValue();");
                builder.AppendLine("            return;");
                builder.AppendLine("        }");
            }

            builder.Append("        global::SharpYaml.Serialization.YamlConverter attributeConverter = new ").Append(attributeConverterTypeName).AppendLine("();");
            builder.AppendLine("        if (attributeConverter is global::SharpYaml.Serialization.YamlConverterFactory factory)");
            builder.AppendLine("        {");
            builder.Append("            var created = factory.CreateConverter(typeof(").Append(typeName).AppendLine("), options);");
            builder.AppendLine("            if (created is null || !created.CanConvert(typeof(" + typeName + ")))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new global::System.InvalidOperationException(\"Converter factory returned an invalid converter.\");");
            builder.AppendLine("            }");
            builder.AppendLine("            created.Write(writer, value);");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine("        attributeConverter.Write(writer, value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        builder.Append("        if (hasCustomConverters && writer.TryGetCustomConverter(typeof(").Append(typeName).AppendLine("), out var rootCustomConverter) && rootCustomConverter is not null)");
        builder.AppendLine("        {");
        builder.AppendLine("            rootCustomConverter.Write(writer, value);");
        builder.AppendLine("            return;");
        builder.AppendLine("        }");
        builder.AppendLine();

        if (typeSymbol is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.AppendLine("        if (!value.HasValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            writer.WriteNullValue();");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        var underlying = value.Value;");

            if (!TryEmitWriteScalar(builder, underlyingType, "underlying", indent: "        "))
            {
                builder.AppendLine("        throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this nullable scalar type.\");");
            }

            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.AppendLine("        writer.WriteString(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            builder.AppendLine("        writer.WriteScalar(value.ToString());");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetArrayElementType(typeSymbol, out var arrayElementType))
        {
            builder.AppendLine("        if (value is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            writer.WriteNullValue();");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (writer.TryWriteReference(value)) { return; }");
            builder.AppendLine();
            builder.AppendLine("        writer.WriteStartSequence();");
            builder.AppendLine("        for (var i = 0; i < value.Length; i++)");
            builder.AppendLine("        {");
            builder.AppendLine("            var element = value[i];");
            EmitWriteKnownType(builder, arrayElementType, indexByType, "element", indent: "            ");
            builder.AppendLine("        }");
            builder.AppendLine("        writer.WriteEndSequence();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetSequenceElementType(typeSymbol, out var sequenceElementType, out var sequenceKind))
        {
            if (sequenceKind == SequenceKind.ImmutableArray)
            {
                builder.AppendLine("        if (value.IsDefault)");
                builder.AppendLine("        {");
                builder.AppendLine("            writer.WriteNullValue();");
                builder.AppendLine("            return;");
                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine("        writer.WriteStartSequence();");
                builder.AppendLine("        for (var i = 0; i < value.Length; i++)");
                builder.AppendLine("        {");
                builder.AppendLine("            var element = value[i];");
                EmitWriteKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                builder.AppendLine("        }");
                builder.AppendLine("        writer.WriteEndSequence();");
                builder.AppendLine("        return;");
                builder.AppendLine("    }");
                return;
            }

            builder.AppendLine("        if (value is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            writer.WriteNullValue();");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (writer.TryWriteReference(value)) { return; }");
            builder.AppendLine();
            builder.AppendLine("        writer.WriteStartSequence();");
            if (sequenceKind == SequenceKind.List)
            {
                builder.AppendLine("        for (var i = 0; i < value.Count; i++)");
                builder.AppendLine("        {");
                builder.AppendLine("            var element = value[i];");
                EmitWriteKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                builder.AppendLine("        }");
            }
            else
            {
                builder.AppendLine("        foreach (var element in value)");
                builder.AppendLine("        {");
                EmitWriteKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                builder.AppendLine("        }");
            }
            builder.AppendLine("        writer.WriteEndSequence();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetDictionaryTypes(typeSymbol, out var dictionaryKeyType, out var dictionaryValueType, out _))
        {
            builder.AppendLine("        if (value is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            writer.WriteNullValue();");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (writer.TryWriteReference(value)) { return; }");
            builder.AppendLine();
            builder.AppendLine("        writer.WriteStartMapping();");
            builder.AppendLine("        foreach (var pair in value)");
            builder.AppendLine("        {");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("            var key = writer.ConvertDictionaryKey(pair.Key);");
                builder.AppendLine("            writer.WritePropertyName(key);");
            }
            else if (dictionaryKeyType is INamedTypeSymbol enumKeyType && enumKeyType.TypeKind == TypeKind.Enum)
            {
                builder.AppendLine("            writer.WritePropertyName(pair.Key.ToString());");
            }
            else if (dictionaryKeyType.SpecialType == SpecialType.System_Boolean)
            {
                builder.AppendLine("            writer.WritePropertyName(pair.Key ? \"true\" : \"false\");");
            }
            else if (dictionaryKeyType.SpecialType == SpecialType.System_Double)
            {
                builder.AppendLine("            var keyText = double.IsPositiveInfinity(pair.Key) ? \".inf\" : double.IsNegativeInfinity(pair.Key) ? \"-.inf\" : double.IsNaN(pair.Key) ? \".nan\" : pair.Key.ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture);");
                builder.AppendLine("            writer.WritePropertyName(keyText);");
            }
            else if (dictionaryKeyType.SpecialType == SpecialType.System_Single)
            {
                builder.AppendLine("            var keyText = float.IsPositiveInfinity(pair.Key) ? \".inf\" : float.IsNegativeInfinity(pair.Key) ? \"-.inf\" : float.IsNaN(pair.Key) ? \".nan\" : pair.Key.ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture);");
                builder.AppendLine("            writer.WritePropertyName(keyText);");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.DateTime")
            {
                builder.AppendLine("            writer.WritePropertyName(pair.Key.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.DateTimeOffset")
            {
                builder.AppendLine("            writer.WritePropertyName(pair.Key.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Guid")
            {
                builder.AppendLine("            writer.WritePropertyName(pair.Key.ToString(\"D\"));");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.TimeSpan")
            {
                builder.AppendLine("            writer.WritePropertyName(pair.Key.ToString(\"c\", global::System.Globalization.CultureInfo.InvariantCulture));");
            }
            else
            {
                builder.AppendLine("            writer.WritePropertyName(((global::System.IFormattable)pair.Key).ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));");
            }

            EmitWriteKnownType(builder, dictionaryValueType, indexByType, "pair.Value", indent: "            ");
            builder.AppendLine("        }");
            builder.AppendLine("        writer.WriteEndMapping();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol is INamedTypeSymbol named && (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct))
        {
            var emitLifecycleCallbacks =
                (named.TypeKind == TypeKind.Class && (!named.IsSealed || ImplementsAnyYamlLifecycleCallback(named))) ||
                (named.TypeKind == TypeKind.Struct && ImplementsAnyYamlLifecycleCallback(named));

            if (named.TypeKind == TypeKind.Class)
            {
                builder.AppendLine("        if (value is null)");
                builder.AppendLine("        {");
                builder.AppendLine("            writer.WriteNullValue();");
                builder.AppendLine("            return;");
                builder.AppendLine("        }");
            }

            if (named.TypeKind == TypeKind.Class)
            {
                builder.AppendLine();
                builder.AppendLine("        if (writer.TryWriteReference(value)) { return; }");
            }

            if (emitLifecycleCallbacks)
            {
                builder.AppendLine();
                builder.AppendLine("        void InvokeOnSerializing()");
                builder.AppendLine("        {");
                builder.AppendLine("            if (value is global::SharpYaml.Serialization.IYamlOnSerializing onSerializing)");
                builder.AppendLine("            {");
                builder.AppendLine("                try");
                builder.AppendLine("                {");
                builder.AppendLine("                    onSerializing.OnSerializing();");
                builder.AppendLine("                }");
                builder.AppendLine("                catch (global::System.Exception exception)");
                builder.AppendLine("                {");
                builder.Append("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowCallbackInvocationFailed(typeof(").Append(typeName).AppendLine("), \"IYamlOnSerializing.OnSerializing\", exception);");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine("        void InvokeOnSerialized()");
                builder.AppendLine("        {");
                builder.AppendLine("            if (value is global::SharpYaml.Serialization.IYamlOnSerialized onSerialized)");
                builder.AppendLine("            {");
                builder.AppendLine("                try");
                builder.AppendLine("                {");
                builder.AppendLine("                    onSerialized.OnSerialized();");
                builder.AppendLine("                }");
                builder.AppendLine("                catch (global::System.Exception exception)");
                builder.AppendLine("                {");
                builder.Append("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowCallbackInvocationFailed(typeof(").Append(typeName).AppendLine("), \"IYamlOnSerialized.OnSerialized\", exception);");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine("        InvokeOnSerializing();");
            }

            if (named.TypeKind == TypeKind.Class && TryGetPolymorphismInfo(named, out var polymorphism) && polymorphism.DerivedTypes.Length != 0)
            {
                builder.AppendLine();

                var discriminatorPropertyNameExpression = polymorphism.DiscriminatorPropertyNameOverride is null
                    ? "options.PolymorphismOptions.TypeDiscriminatorPropertyName"
                    : ToLiteral(polymorphism.DiscriminatorPropertyNameOverride);
                var discriminatorStyleExpression = polymorphism.DiscriminatorStyleOverrideValue is null
                    ? "options.PolymorphismOptions.DiscriminatorStyle"
                    : $"(global::SharpYaml.YamlTypeDiscriminatorStyle){polymorphism.DiscriminatorStyleOverrideValue.Value}";

                builder.Append("        var discriminatorPropertyName = ").Append(discriminatorPropertyNameExpression).AppendLine(";");
                builder.Append("        var discriminatorStyle = ").Append(discriminatorStyleExpression).AppendLine(";");

                for (var i = 0; i < polymorphism.DerivedTypes.Length; i++)
                {
                    var derived = polymorphism.DerivedTypes[i];
                    var derivedTypeName = derived.DerivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!indexByType.TryGetValue(derived.DerivedType, out var derivedIndex))
                    {
                        continue;
                    }

                    var derivedLocal = $"derived{derivedIndex}";
                    builder.Append("        if (value is ").Append(derivedTypeName).Append(' ').Append(derivedLocal).AppendLine(")");
                    builder.AppendLine("        {");

                    if (derived.Tag is not null)
                    {
                        builder.Append("            if (discriminatorStyle is global::SharpYaml.YamlTypeDiscriminatorStyle.Tag or global::SharpYaml.YamlTypeDiscriminatorStyle.Both) { writer.WriteTag(")
                            .Append(ToLiteral(derived.Tag)).AppendLine("); }");
                    }

                    builder.AppendLine("            writer.WriteStartMapping();");
                    if (derived.Discriminator is not null)
                    {
                        builder.AppendLine("            if (discriminatorStyle is global::SharpYaml.YamlTypeDiscriminatorStyle.Property or global::SharpYaml.YamlTypeDiscriminatorStyle.Both)");
                        builder.AppendLine("            {");
                        builder.AppendLine("                writer.WritePropertyName(discriminatorPropertyName);");
                        builder.Append("                writer.WriteScalar(").Append(ToLiteral(derived.Discriminator)).AppendLine(");");
                        builder.AppendLine("            }");
                    }
                    builder.Append("            WriteMembers").Append(derivedIndex).Append("(writer, ").Append(derivedLocal).AppendLine(", discriminatorPropertyName);");
                    builder.AppendLine("            writer.WriteEndMapping();");
                    if (emitLifecycleCallbacks)
                    {
                        builder.AppendLine("            InvokeOnSerialized();");
                    }
                    builder.AppendLine("            return;");
                    builder.AppendLine("        }");
                }

                builder.Append("        if (value.GetType() != typeof(").Append(typeName).AppendLine("))");
                builder.AppendLine("        {");
                builder.Append("            throw new global::System.NotSupportedException($\"Type '{value.GetType()}' is not a registered derived type of '{typeof(")
                    .Append(typeName).AppendLine(")}'.\");");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            builder.AppendLine("        writer.WriteStartMapping();");

            var extensionData = TryCreateExtensionDataMemberModel(named);
            var members = GetSerializableMembers(named)
                .Where(m => extensionData is null || !SymbolEqualityComparer.Default.Equals(m, extensionData.Symbol))
                .Select(m => CreateMemberModel(m, propertyNamingPolicy))
                .ToImmutableArray();
            builder.Append("        WriteMembers").Append(index).AppendLine("(writer, value, discriminatorPropertyName: null);");
            builder.AppendLine("        writer.WriteEndMapping();");
            if (emitLifecycleCallbacks)
            {
                builder.AppendLine("        InvokeOnSerialized();");
            }
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            EmitWriteMembersMethod(builder, index, typeName, members, extensionData, indexByType);
            return;
        }

        builder.AppendLine("        throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this type.\");");
        builder.AppendLine("    }");
    }

    private static void EmitWriteMembersMethod(StringBuilder builder, int index, string typeName, ImmutableArray<MemberModel> members, ExtensionDataMemberModel? extensionData, Dictionary<ITypeSymbol, int> indexByType)
    {
        builder.Append("    private static void WriteMembers").Append(index)
            .Append("(global::SharpYaml.Serialization.YamlWriter writer, ").Append(typeName)
            .AppendLine(" value, string? discriminatorPropertyName)");
        builder.AppendLine("    {");
        builder.AppendLine("        var options = writer.Options;");
        builder.AppendLine("        var hasCustomConverters = options.Converters.Count != 0;");
        builder.AppendLine();

        foreach (var member in members)
        {
            var memberValueVar = "__value" + index + "_" + member.Symbol.Name;
            var ignoreVar = "__ignore" + index + "_" + member.Symbol.Name;
            var nameVar = "__name" + index + "_" + member.Symbol.Name;

            builder.Append("        var ").Append(memberValueVar).Append(" = ").Append(member.AccessExpression).AppendLine(";");
            builder.Append("        var ").Append(ignoreVar).Append(" = ").Append(member.IgnoreConditionExpression).AppendLine(";");

            builder.Append("        if (").Append(ignoreVar).AppendLine(" == global::SharpYaml.YamlIgnoreCondition.WhenWritingNull)");
            builder.AppendLine("        {");
            if (member.Type.IsReferenceType)
            {
                builder.Append("            if (").Append(memberValueVar).AppendLine(" is null)");
                builder.AppendLine("            {");
                builder.AppendLine("                // Skip null value.");
                builder.AppendLine("            }");
                builder.AppendLine("            else");
                builder.AppendLine("            {");
                builder.Append("                var ").Append(nameVar).Append(" = ").Append(member.SerializedNameExpressionForWrite).AppendLine(";");
                builder.Append("                if (discriminatorPropertyName is null || !global::System.String.Equals(").Append(nameVar).AppendLine(", discriminatorPropertyName, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                {");
                builder.Append("                    writer.WritePropertyName(").Append(nameVar).AppendLine(");");
                EmitWriteMemberValueWithCustomConverter(builder, member, indexByType, valueExpression: memberValueVar, indent: "                    ");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
            }
            else
            {
                builder.AppendLine("            // Value types are never null.");
                builder.Append("            var ").Append(nameVar).Append(" = ").Append(member.SerializedNameExpressionForWrite).AppendLine(";");
                builder.Append("            if (discriminatorPropertyName is null || !global::System.String.Equals(").Append(nameVar).AppendLine(", discriminatorPropertyName, global::System.StringComparison.Ordinal))");
                builder.AppendLine("            {");
                builder.Append("                writer.WritePropertyName(").Append(nameVar).AppendLine(");");
                EmitWriteMemberValueWithCustomConverter(builder, member, indexByType, valueExpression: memberValueVar, indent: "                ");
                builder.AppendLine("            }");
            }
            builder.AppendLine("        }");
            builder.Append("        else if (").Append(ignoreVar).AppendLine(" == global::SharpYaml.YamlIgnoreCondition.WhenWritingDefault)");
            builder.AppendLine("        {");
            builder.Append("            if (global::System.Collections.Generic.EqualityComparer<").Append(member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">.Default.Equals(")
                .Append(memberValueVar).AppendLine(", default))");
            builder.AppendLine("            {");
            builder.AppendLine("                // Skip default value.");
            builder.AppendLine("            }");
            builder.AppendLine("            else");
            builder.AppendLine("            {");
            builder.Append("                var ").Append(nameVar).Append(" = ").Append(member.SerializedNameExpressionForWrite).AppendLine(";");
            builder.Append("                if (discriminatorPropertyName is null || !global::System.String.Equals(").Append(nameVar).AppendLine(", discriminatorPropertyName, global::System.StringComparison.Ordinal))");
            builder.AppendLine("                {");
            builder.Append("                    writer.WritePropertyName(").Append(nameVar).AppendLine(");");
            EmitWriteMemberValueWithCustomConverter(builder, member, indexByType, valueExpression: memberValueVar, indent: "                    ");
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("        else");
            builder.AppendLine("        {");
            builder.Append("            var ").Append(nameVar).Append(" = ").Append(member.SerializedNameExpressionForWrite).AppendLine(";");
            builder.Append("            if (discriminatorPropertyName is null || !global::System.String.Equals(").Append(nameVar).AppendLine(", discriminatorPropertyName, global::System.StringComparison.Ordinal))");
            builder.AppendLine("            {");
            builder.Append("                writer.WritePropertyName(").Append(nameVar).AppendLine(");");
            EmitWriteMemberValueWithCustomConverter(builder, member, indexByType, valueExpression: memberValueVar, indent: "                ");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
        }

        if (extensionData is not null)
        {
            builder.AppendLine();
            builder.Append("        var extensionData = value.").Append(extensionData.Symbol.Name).AppendLine(";");
            builder.AppendLine("        if (extensionData is not null)");
            builder.AppendLine("        {");

            if (extensionData.Kind == ExtensionDataKind.Dictionary)
            {
                var valueTypeName = (extensionData.DictionaryValueType ?? throw new InvalidOperationException("Extension data dictionary value type is missing."))
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                builder.AppendLine("            if (options.MappingOrder == global::SharpYaml.YamlMappingOrderPolicy.Sorted)");
                builder.AppendLine("            {");
                builder.Append("                var items = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, ").Append(valueTypeName).AppendLine(">>(extensionData.Count);");
                builder.AppendLine("                foreach (var pair in extensionData)");
                builder.AppendLine("                {");
                builder.AppendLine("                    items.Add(pair);");
                builder.AppendLine("                }");
                builder.AppendLine("                items.Sort(static (x, y) => global::System.String.CompareOrdinal(x.Key, y.Key));");
                builder.AppendLine("                for (var i = 0; i < items.Count; i++)");
                builder.AppendLine("                {");
                builder.AppendLine("                    var key = items[i].Key;");
                builder.AppendLine("                    if (discriminatorPropertyName is not null && global::System.String.Equals(key, discriminatorPropertyName, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                    {");
                builder.AppendLine("                        continue;");
                builder.AppendLine("                    }");
                builder.AppendLine("                    writer.WritePropertyName(key);");
                builder.AppendLine("                    var extensionValue = items[i].Value;");
                builder.AppendLine("                    if (extensionValue is null)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        writer.WriteNullValue();");
                builder.AppendLine("                    }");
                builder.AppendLine("                    else");
                builder.AppendLine("                    {");
                builder.Append("                        var converter = writer.GetConverter(typeof(").Append(valueTypeName).AppendLine("));");
                builder.AppendLine("                        converter.Write(writer, extensionValue);");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("            else");
                builder.AppendLine("            {");
                builder.AppendLine("                foreach (var pair in extensionData)");
                builder.AppendLine("                {");
                builder.AppendLine("                    var key = pair.Key;");
                builder.AppendLine("                    if (discriminatorPropertyName is not null && global::System.String.Equals(key, discriminatorPropertyName, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                    {");
                builder.AppendLine("                        continue;");
                builder.AppendLine("                    }");
                builder.AppendLine("                    writer.WritePropertyName(key);");
                builder.AppendLine("                    var extensionValue = pair.Value;");
                builder.AppendLine("                    if (extensionValue is null)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        writer.WriteNullValue();");
                builder.AppendLine("                    }");
                builder.AppendLine("                    else");
                builder.AppendLine("                    {");
                builder.Append("                        var converter = writer.GetConverter(typeof(").Append(valueTypeName).AppendLine("));");
                builder.AppendLine("                        converter.Write(writer, extensionValue);");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
            }
            else
            {
                builder.AppendLine("            if (extensionData is not global::SharpYaml.Model.YamlMapping mapping)");
                builder.AppendLine("            {");
                builder.AppendLine("                throw new global::SharpYaml.YamlException(global::SharpYaml.Mark.Empty, global::SharpYaml.Mark.Empty, \"Extension data must be a SharpYaml.Model.YamlMapping.\");");
                builder.AppendLine("            }");
                builder.AppendLine("            var list = (global::System.Collections.Generic.IList<global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>>)mapping;");
                builder.AppendLine("            if (options.MappingOrder == global::SharpYaml.YamlMappingOrderPolicy.Sorted)");
                builder.AppendLine("            {");
                builder.AppendLine("                var items = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, global::SharpYaml.Model.YamlElement?>>(list.Count);");
                builder.AppendLine("                for (var i = 0; i < list.Count; i++)");
                builder.AppendLine("                {");
                builder.AppendLine("                    var pair = list[i];");
                builder.AppendLine("                    if (pair.Key is not global::SharpYaml.Model.YamlValue keyValue)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        throw new global::SharpYaml.YamlException(global::SharpYaml.Mark.Empty, global::SharpYaml.Mark.Empty, \"Only scalar mapping keys are supported for extension data.\");");
                builder.AppendLine("                    }");
                builder.AppendLine("                    items.Add(new global::System.Collections.Generic.KeyValuePair<string, global::SharpYaml.Model.YamlElement?>(keyValue.Value, pair.Value));");
                builder.AppendLine("                }");
                builder.AppendLine("                items.Sort(static (x, y) => global::System.String.CompareOrdinal(x.Key, y.Key));");
                builder.AppendLine("                for (var i = 0; i < items.Count; i++)");
                builder.AppendLine("                {");
                builder.AppendLine("                    var key = items[i].Key;");
                builder.AppendLine("                    if (discriminatorPropertyName is not null && global::System.String.Equals(key, discriminatorPropertyName, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                    {");
                builder.AppendLine("                        continue;");
                builder.AppendLine("                    }");
                builder.AppendLine("                    writer.WritePropertyName(key);");
                builder.AppendLine("                    var extensionValue = items[i].Value;");
                builder.AppendLine("                    if (extensionValue is null)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        writer.WriteNullValue();");
                builder.AppendLine("                    }");
                builder.AppendLine("                    else");
                builder.AppendLine("                    {");
                builder.AppendLine("                        var converter = writer.GetConverter(typeof(global::SharpYaml.Model.YamlNode));");
                builder.AppendLine("                        converter.Write(writer, extensionValue);");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("            else");
                builder.AppendLine("            {");
                builder.AppendLine("                for (var i = 0; i < list.Count; i++)");
                builder.AppendLine("                {");
                builder.AppendLine("                    var pair = list[i];");
                builder.AppendLine("                    if (pair.Key is not global::SharpYaml.Model.YamlValue keyValue)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        throw new global::SharpYaml.YamlException(global::SharpYaml.Mark.Empty, global::SharpYaml.Mark.Empty, \"Only scalar mapping keys are supported for extension data.\");");
                builder.AppendLine("                    }");
                builder.AppendLine("                    var key = keyValue.Value;");
                builder.AppendLine("                    if (discriminatorPropertyName is not null && global::System.String.Equals(key, discriminatorPropertyName, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                    {");
                builder.AppendLine("                        continue;");
                builder.AppendLine("                    }");
                builder.AppendLine("                    writer.WritePropertyName(key);");
                builder.AppendLine("                    var extensionValue = pair.Value;");
                builder.AppendLine("                    if (extensionValue is null)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        writer.WriteNullValue();");
                builder.AppendLine("                    }");
                builder.AppendLine("                    else");
                builder.AppendLine("                    {");
                builder.AppendLine("                        var converter = writer.GetConverter(typeof(global::SharpYaml.Model.YamlNode));");
                builder.AppendLine("                        converter.Write(writer, extensionValue);");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
            }

            builder.AppendLine("        }");
        }

        builder.AppendLine("    }");
    }

    private static void EmitReadObjectCoreMethod(StringBuilder builder, int index, ITypeSymbol typeSymbol, string typeName, ImmutableArray<MemberModel> members, ExtensionDataMemberModel? extensionData, Dictionary<ITypeSymbol, int> indexByType, JsonNamingPolicy? propertyNamingPolicy)
    {
        var emitLifecycleCallbacks =
            typeSymbol is INamedTypeSymbol lifecycleType &&
            ((lifecycleType.TypeKind == TypeKind.Class && (!lifecycleType.IsSealed || ImplementsAnyYamlLifecycleCallback(lifecycleType))) ||
             (lifecycleType.TypeKind == TypeKind.Struct && ImplementsAnyYamlLifecycleCallback(lifecycleType)));

        builder.Append("    private static ").Append(typeName).Append(typeSymbol.IsReferenceType ? "?" : string.Empty).Append(" ReadObjectCore").Append(index)
            .AppendLine("(global::SharpYaml.Serialization.YamlReader reader)");
        builder.AppendLine("    {");
        builder.AppendLine("        var options = reader.Options;");
        builder.AppendLine("        var hasCustomConverters = options.Converters.Count != 0;");
        builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedMapping(reader);");
        builder.AppendLine("        }");
        builder.AppendLine("        var mappingStart = reader.Start;");

        if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Class && namedType.IsAbstract)
        {
            builder.Append("        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowAbstractTypeWithoutDiscriminator(reader, typeof(").Append(typeName).AppendLine("));");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.IsReferenceType)
        {
            builder.AppendLine("        var instanceAnchor = reader.Anchor;");
        }

        if (typeSymbol is INamedTypeSymbol ctorType && ctorType.TypeKind == TypeKind.Class && !ctorType.IsAbstract)
        {
            if (!TrySelectDeserializationConstructor(ctorType, out var selectedConstructor, out var constructorError))
            {
                builder.Append("        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, ").Append(ToLiteral(constructorError ?? "The generated YAML serializer does not support this type.")).AppendLine(");");
                builder.AppendLine("    }");
                return;
            }

            if (selectedConstructor is not null && !IsConstructorAccessibleFromGeneratedContext(selectedConstructor))
            {
                builder.Append("        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, ").Append(ToLiteral("The generated YAML serializer only supports constructors that are accessible from generated code (public, internal, or protected internal).")).AppendLine(");");
                builder.AppendLine("    }");
                return;
            }

            if (selectedConstructor is not null &&
                (selectedConstructor.Parameters.Length != 0 ||
                 members.Any(static member => member.IsInitOnly) ||
                 extensionData is { IsInitOnly: true }))
            {
                EmitReadObjectCoreWithConstructor(builder, index, ctorType, typeName, selectedConstructor, members, extensionData, indexByType, emitLifecycleCallbacks, propertyNamingPolicy);
                builder.AppendLine("    }");
                return;
            }
        }

        builder.Append("        var instance = new ").Append(typeName).AppendLine("();");
        if (typeSymbol.IsReferenceType)
        {
            builder.AppendLine("        if (instanceAnchor is not null) { reader.RegisterAnchor(instanceAnchor, instance); }");
        }

        if (emitLifecycleCallbacks)
        {
            builder.AppendLine("        if (instance is global::SharpYaml.Serialization.IYamlOnDeserializing onDeserializing)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                onDeserializing.OnDeserializing();");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (global::System.Exception exception)");
            builder.AppendLine("            {");
            builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowCallbackInvocationFailed(reader, typeof(").Append(typeName).AppendLine("), \"IYamlOnDeserializing.OnDeserializing\", exception);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        if (extensionData is not null)
        {
            if (extensionData.Kind == ExtensionDataKind.Dictionary)
            {
                var valueTypeName = (extensionData.DictionaryValueType ?? throw new InvalidOperationException("Extension data dictionary value type is missing."))
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.AppendLine("        void ReadAndStoreExtensionData(string extensionKey)");
                builder.AppendLine("        {");
                builder.Append("            var container = instance.").Append(extensionData.Symbol.Name).AppendLine(";");
                builder.AppendLine("            if (container is null)");
                builder.AppendLine("            {");
                builder.Append("                container = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                    .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
                if (extensionData.CanAssign)
                {
                    builder.Append("                instance.").Append(extensionData.Symbol.Name).AppendLine(" = container;");
                }
                else
                {
                    builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"Extension data member '")
                        .Append(extensionData.Symbol.Name).Append("' could not be assigned.\");").AppendLine();
                }
                builder.AppendLine("            }");
                builder.Append("            var converter = reader.GetConverter(typeof(").Append(valueTypeName).AppendLine("));");
                builder.Append("            var extensionValue = (").Append(valueTypeName).Append(")converter.Read(reader, typeof(").Append(valueTypeName).AppendLine("));");
                builder.AppendLine("            container[extensionKey] = extensionValue;");
                builder.AppendLine("        }");
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine("        void ReadAndStoreExtensionData(string extensionKey)");
                builder.AppendLine("        {");
                builder.Append("            var mapping = instance.").Append(extensionData.Symbol.Name).AppendLine(";");
                builder.AppendLine("            if (mapping is null)");
                builder.AppendLine("            {");
                builder.AppendLine("                mapping = new global::SharpYaml.Model.YamlMapping();");
                if (extensionData.CanAssign)
                {
                    builder.Append("                instance.").Append(extensionData.Symbol.Name).AppendLine(" = mapping;");
                }
                else
                {
                    builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"Extension data member '")
                        .Append(extensionData.Symbol.Name).Append("' could not be assigned.\");").AppendLine();
                }
                builder.AppendLine("            }");
                builder.AppendLine("            var converter = reader.GetConverter(typeof(global::SharpYaml.Model.YamlElement));");
                builder.AppendLine("            var extensionValue = (global::SharpYaml.Model.YamlElement?)converter.Read(reader, typeof(global::SharpYaml.Model.YamlElement));");
                builder.AppendLine("            var list = (global::System.Collections.Generic.IList<global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>>)mapping;");
                builder.AppendLine("            for (var i = 0; i < list.Count; i++)");
                builder.AppendLine("            {");
                builder.AppendLine("                var pair = list[i];");
                builder.AppendLine("                if (pair.Key is global::SharpYaml.Model.YamlValue keyValue && global::System.String.Equals(keyValue.Value, extensionKey, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                {");
                builder.AppendLine("                    list[i] = new global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>(pair.Key, extensionValue);");
                builder.AppendLine("                    return;");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("            mapping.Add(new global::SharpYaml.Model.YamlValue(extensionKey), extensionValue);");
                builder.AppendLine("        }");
                builder.AppendLine();
            }
        }

        var requiredMembers = members.Where(static m => m.IsRequired).ToArray();
        var readCandidates = members.Where(static m => IsWritableMember(m.Symbol) || m.IsRequired).ToImmutableArray();
        for (var i = 0; i < requiredMembers.Length; i++)
        {
            builder.Append("        var __required").Append(index).Append("_").Append(requiredMembers[i].Symbol.Name).AppendLine(" = false;");
        }
        if (requiredMembers.Length != 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine("        var mergeEnabled = options.Schema is global::SharpYaml.YamlSchemaKind.Core or global::SharpYaml.YamlSchemaKind.Extended;");
        builder.AppendLine("        global::System.Collections.Generic.HashSet<string>? explicitKeys = mergeEnabled");
        builder.AppendLine("            ? new global::System.Collections.Generic.HashSet<string>(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal)");
        builder.AppendLine("            : null;");
        builder.AppendLine();
        builder.AppendLine("        void ReadAndApplyMerge()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (!mergeEnabled)");
        builder.AppendLine("            {");
        builder.AppendLine("                reader.Skip();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
        builder.AppendLine("            {");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("            {");
        builder.AppendLine("                ApplyMergeMapping();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
        builder.AppendLine("            {");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
        builder.AppendLine("                {");
        builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("                    {");
        builder.AppendLine("                        throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge sequence entries must be mappings.\");");
        builder.AppendLine("                    }");
        builder.AppendLine("                    ApplyMergeMapping();");
        builder.AppendLine("                }");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Alias)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge alias values are not supported in source-generated deserialization.\");");
        builder.AppendLine("            }");
        builder.AppendLine("            throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge key value must be a mapping or a sequence of mappings.\");");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        void ApplyMergeMapping()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedMapping(reader);");
        builder.AppendLine("            }");
        builder.AppendLine("            reader.Read();");
        builder.AppendLine("            while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
        builder.AppendLine("            {");
        builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
        builder.AppendLine("                {");
        builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalarKey(reader);");
        builder.AppendLine("                }");
        builder.AppendLine("                var mergeKey = reader.ScalarValue ?? string.Empty;");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                if (mergeEnabled && global::System.String.Equals(mergeKey, \"<<\", global::System.StringComparison.Ordinal))");
        builder.AppendLine("                {");
        builder.AppendLine("                    ReadAndApplyMerge();");
        builder.AppendLine("                    continue;");
        builder.AppendLine("                }");
        builder.AppendLine("                if (explicitKeys is not null && explicitKeys.Contains(mergeKey))");
        builder.AppendLine("                {");
        builder.AppendLine("                    reader.Skip();");
        builder.AppendLine("                    continue;");
        builder.AppendLine("                }");
        builder.AppendLine("                var matched = false;");
        foreach (var member in readCandidates)
        {
            builder.Append("                if (!matched && global::System.String.Equals(mergeKey, ").Append(member.SerializedNameExpressionForRead)
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("                {");
            builder.AppendLine("                    matched = true;");
            if (member.IsRequired)
            {
                builder.Append("                    __required").Append(index).Append("_").Append(member.Symbol.Name).AppendLine(" = true;");
            }

            if (IsWritableMember(member.Symbol))
            {
                EmitReadMemberValueWithCustomConverter(builder, member, indexByType);
            }
            else
            {
                if (extensionData is not null)
                {
                    builder.AppendLine("                    ReadAndStoreExtensionData(mergeKey);");
                }
                else
                {
                    builder.AppendLine("                    reader.Skip();");
                }
            }

            builder.AppendLine("                }");
        }

        builder.AppendLine("                if (!matched)");
        builder.AppendLine("                {");
        if (extensionData is not null)
        {
            builder.AppendLine("                    ReadAndStoreExtensionData(mergeKey);");
        }
        else
        {
            builder.AppendLine("                    reader.Skip();");
        }
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("            reader.Read();");
        builder.AppendLine("        }");
        builder.AppendLine();

        builder.AppendLine("        reader.Read();");
        builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalarKey(reader);");
        builder.AppendLine("            }");
        builder.AppendLine("            var key = reader.ScalarValue ?? string.Empty;");
        builder.AppendLine("            reader.Read();");
        builder.AppendLine();
        builder.AppendLine("            if (mergeEnabled && global::System.String.Equals(key, \"<<\", global::System.StringComparison.Ordinal))");
        builder.AppendLine("            {");
        builder.AppendLine("                ReadAndApplyMerge();");
        builder.AppendLine("                continue;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            explicitKeys?.Add(key);");

        builder.AppendLine("            var matched = false;");
        foreach (var member in readCandidates)
        {
            builder.Append("            if (!matched && global::System.String.Equals(key, ").Append(member.SerializedNameExpressionForRead)
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("            {");
            builder.AppendLine("                matched = true;");
            if (member.IsRequired)
            {
                builder.Append("                __required").Append(index).Append("_").Append(member.Symbol.Name).AppendLine(" = true;");
            }

            if (IsWritableMember(member.Symbol))
            {
                EmitReadMemberValueWithCustomConverter(builder, member, indexByType);
            }
            else
            {
                if (extensionData is not null)
                {
                    builder.AppendLine("                ReadAndStoreExtensionData(key);");
                }
                else
                {
                    builder.AppendLine("                reader.Skip();");
                }
            }
            builder.AppendLine("            }");
        }

        builder.AppendLine("            if (!matched)");
        builder.AppendLine("            {");
        if (extensionData is not null)
        {
            builder.AppendLine("                ReadAndStoreExtensionData(key);");
        }
        else
        {
            builder.AppendLine("                reader.Skip();");
        }
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        if (requiredMembers.Length != 0)
        {
            builder.AppendLine();
            builder.Append("        if (");
            for (var i = 0; i < requiredMembers.Length; i++)
            {
                if (i != 0)
                {
                    builder.Append(" || ");
                }
                builder.Append("!__required").Append(index).Append("_").Append(requiredMembers[i].Symbol.Name);
            }
            builder.AppendLine(")");
            builder.AppendLine("        {");
            builder.AppendLine("            var missing = new global::System.Collections.Generic.List<string>();");
            for (var i = 0; i < requiredMembers.Length; i++)
            {
                builder.Append("            if (!__required").Append(index).Append("_").Append(requiredMembers[i].Symbol.Name).AppendLine(")");
                builder.AppendLine("            {");
                builder.Append("                missing.Add(").Append(requiredMembers[i].SerializedNameExpressionForRead).AppendLine(");");
                builder.AppendLine("            }");
            }
            builder.Append("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowMissingRequiredMembers(reader, mappingStart, typeof(").Append(typeName).AppendLine("), missing);");
            builder.AppendLine("        }");
        }

        if (emitLifecycleCallbacks)
        {
            builder.AppendLine();
            builder.AppendLine("        if (instance is global::SharpYaml.Serialization.IYamlOnDeserialized onDeserialized)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                onDeserialized.OnDeserialized();");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (global::System.Exception exception)");
            builder.AppendLine("            {");
            builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowCallbackInvocationFailed(reader, typeof(").Append(typeName).AppendLine("), \"IYamlOnDeserialized.OnDeserialized\", exception);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
        }
        builder.AppendLine("        reader.Read();");
        builder.AppendLine("        return instance;");
        builder.AppendLine("    }");
    }

    private static void EmitReadObjectCoreWithConstructor(
        StringBuilder builder,
        int index,
        INamedTypeSymbol typeSymbol,
        string typeName,
        IMethodSymbol constructor,
        ImmutableArray<MemberModel> members,
        ExtensionDataMemberModel? extensionData,
        Dictionary<ITypeSymbol, int> indexByType,
        bool emitLifecycleCallbacks,
        JsonNamingPolicy? propertyNamingPolicy)
    {
        // Map constructor parameters to member YAML names when possible (STJ-like binding rules).
        var parameters = constructor.Parameters;
        var ctorBoundMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        var parameterYamlNameExpressions = new string[parameters.Length];
        var parameterValueVarNames = new string[parameters.Length];
        var parameterSeenVarNames = new string[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterName = parameter.Name ?? throw new InvalidOperationException("Constructor parameter name was not available.");

            MemberModel? matchedMember = null;
            for (var m = 0; m < members.Length; m++)
            {
                if (string.Equals(members[m].Symbol.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedMember = members[m];
                    break;
                }
            }

            if (matchedMember is not null)
            {
                ctorBoundMembers.Add(matchedMember.Symbol);
                parameterYamlNameExpressions[i] = matchedMember.SerializedNameExpressionForRead;
            }
            else
            {
                parameterYamlNameExpressions[i] = ToLiteral(ApplyNamingPolicy(parameterName, propertyNamingPolicy));
            }

            parameterValueVarNames[i] = $"__ctor{index}_{parameterName}";
            parameterSeenVarNames[i] = $"__ctor{index}_{parameterName}_seen";
        }

        var requiredMembers = members.Where(static m => m.IsRequired).ToArray();
        var requiredVarBySymbol = new Dictionary<ISymbol, string>(requiredMembers.Length, SymbolEqualityComparer.Default);
        for (var i = 0; i < requiredMembers.Length; i++)
        {
            requiredVarBySymbol[requiredMembers[i].Symbol] = $"__required{index}_{requiredMembers[i].Symbol.Name}";
        }

        // Buffer writable members that are not constructor-bound. Init-only members are applied in the object initializer
        // when the instance is created, while mutable members are assigned afterwards.
        var bufferedMembers = members
            .Where(m => IsWritableMember(m.Symbol) && !ctorBoundMembers.Contains(m.Symbol))
            .ToArray();
        var initOnlyMembers = bufferedMembers.Where(static m => m.IsInitOnly).ToArray();
        var postCreateBufferedMembers = bufferedMembers.Where(static m => !m.IsInitOnly).ToArray();
        var useObjectInitializer = initOnlyMembers.Length != 0 || extensionData is { IsInitOnly: true };
        var initOnlyExtensionDataValueVarName = extensionData is { IsInitOnly: true } ? $"__extensionData{index}" : null;

        var bufferedMemberValueVarNames = new Dictionary<ISymbol, string>(bufferedMembers.Length, SymbolEqualityComparer.Default);
        var bufferedMemberSeenVarNames = new Dictionary<ISymbol, string>(bufferedMembers.Length, SymbolEqualityComparer.Default);

        for (var i = 0; i < bufferedMembers.Length; i++)
        {
            var member = bufferedMembers[i];
            bufferedMemberValueVarNames[member.Symbol] = $"__member{index}_{member.Symbol.Name}";
            bufferedMemberSeenVarNames[member.Symbol] = $"__member{index}_{member.Symbol.Name}_seen";
        }

        // Parameters
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterTypeName = parameters[i].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("        var ").Append(parameterValueVarNames[i]).Append(" = default(").Append(parameterTypeName).AppendLine(");");
            builder.Append("        var ").Append(parameterSeenVarNames[i]).AppendLine(" = false;");
        }

        // Buffered members
        for (var i = 0; i < bufferedMembers.Length; i++)
        {
            var member = bufferedMembers[i];
            var memberTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("        var ").Append(bufferedMemberValueVarNames[member.Symbol]).Append(" = default(").Append(memberTypeName).AppendLine(");");
            builder.Append("        var ").Append(bufferedMemberSeenVarNames[member.Symbol]).AppendLine(" = false;");
        }

        // Required members
        for (var i = 0; i < requiredMembers.Length; i++)
        {
            builder.Append("        var ").Append(requiredVarBySymbol[requiredMembers[i].Symbol]).AppendLine(" = false;");
        }

        if (extensionData is not null)
        {
            builder.AppendLine();

            if (extensionData.Kind == ExtensionDataKind.Dictionary)
            {
                var valueTypeName = (extensionData.DictionaryValueType ?? throw new InvalidOperationException("Extension data dictionary value type is missing."))
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                builder.Append("        global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, ").Append(valueTypeName).AppendLine(">>? extensionEntries = null;");
                builder.AppendLine("        void BufferExtensionData(string extensionKey)");
                builder.AppendLine("        {");
                builder.AppendLine("            extensionEntries ??= new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, " + valueTypeName + ">>();");
                builder.Append("            var converter = reader.GetConverter(typeof(").Append(valueTypeName).AppendLine("));");
                builder.Append("            var extensionValue = (").Append(valueTypeName).Append(")converter.Read(reader, typeof(").Append(valueTypeName).AppendLine("));");
                builder.AppendLine("            extensionEntries.Add(new global::System.Collections.Generic.KeyValuePair<string, " + valueTypeName + ">(extensionKey, extensionValue));");
                builder.AppendLine("        }");
            }
            else
            {
                builder.AppendLine("        global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, global::SharpYaml.Model.YamlElement?>>? extensionEntries = null;");
                builder.AppendLine("        void BufferExtensionData(string extensionKey)");
                builder.AppendLine("        {");
                builder.AppendLine("            extensionEntries ??= new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, global::SharpYaml.Model.YamlElement?>>();");
                builder.AppendLine("            var converter = reader.GetConverter(typeof(global::SharpYaml.Model.YamlElement));");
                builder.AppendLine("            var extensionValue = (global::SharpYaml.Model.YamlElement?)converter.Read(reader, typeof(global::SharpYaml.Model.YamlElement));");
                builder.AppendLine("            extensionEntries.Add(new global::System.Collections.Generic.KeyValuePair<string, global::SharpYaml.Model.YamlElement?>(extensionKey, extensionValue));");
                builder.AppendLine("        }");
            }
        }

        builder.AppendLine();
        builder.AppendLine("        var mergeEnabled = options.Schema is global::SharpYaml.YamlSchemaKind.Core or global::SharpYaml.YamlSchemaKind.Extended;");
        builder.AppendLine("        global::System.Collections.Generic.HashSet<string>? explicitKeys = mergeEnabled");
        builder.AppendLine("            ? new global::System.Collections.Generic.HashSet<string>(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal)");
        builder.AppendLine("            : null;");
        builder.AppendLine();
        builder.AppendLine("        void ReadAndApplyMerge()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (!mergeEnabled)");
        builder.AppendLine("            {");
        builder.AppendLine("                reader.Skip();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
        builder.AppendLine("            {");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("            {");
        builder.AppendLine("                ApplyMergeMapping();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
        builder.AppendLine("            {");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
        builder.AppendLine("                {");
        builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("                    {");
        builder.AppendLine("                        throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge sequence entries must be mappings.\");");
        builder.AppendLine("                    }");
        builder.AppendLine("                    ApplyMergeMapping();");
        builder.AppendLine("                }");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Alias)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge alias values are not supported in source-generated deserialization.\");");
        builder.AppendLine("            }");
        builder.AppendLine("            throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge key value must be a mapping or a sequence of mappings.\");");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        void ApplyMergeMapping()");
        builder.AppendLine("        {");
        builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedMapping(reader);");
        builder.AppendLine("            }");
        builder.AppendLine("            reader.Read();");
        builder.AppendLine("            while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
        builder.AppendLine("            {");
        builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
        builder.AppendLine("                {");
        builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalarKey(reader);");
        builder.AppendLine("                }");
        builder.AppendLine("                var mergeKey = reader.ScalarValue ?? string.Empty;");
        builder.AppendLine("                reader.Read();");
        builder.AppendLine("                if (mergeEnabled && global::System.String.Equals(mergeKey, \"<<\", global::System.StringComparison.Ordinal))");
        builder.AppendLine("                {");
        builder.AppendLine("                    ReadAndApplyMerge();");
        builder.AppendLine("                    continue;");
        builder.AppendLine("                }");
        builder.AppendLine("                if (explicitKeys is not null && explicitKeys.Contains(mergeKey))");
        builder.AppendLine("                {");
        builder.AppendLine("                    reader.Skip();");
        builder.AppendLine("                    continue;");
        builder.AppendLine("                }");
        builder.AppendLine();
        builder.AppendLine("                var matched = false;");

        // Constructor parameters first.
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterName = parameter.Name ?? string.Empty;

            builder.Append("                if (!matched && global::System.String.Equals(mergeKey, ").Append(parameterYamlNameExpressions[i])
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("                {");
            builder.AppendLine("                    matched = true;");
            if (requiredMembers.Length != 0)
            {
                for (var m = 0; m < members.Length; m++)
                {
                    var member = members[m];
                    if (!member.IsRequired)
                    {
                        continue;
                    }

                    if (string.Equals(member.Symbol.Name, parameterName, StringComparison.OrdinalIgnoreCase) &&
                        requiredVarBySymbol.TryGetValue(member.Symbol, out var requiredVar))
                    {
                        builder.Append("                    ").Append(requiredVar).AppendLine(" = true;");
                    }
                }
            }

            var tmpVar = $"__merge_ctor{index}_tmp{i}";
            EmitReadKnownType(builder, parameter.Type, indexByType, tmpVar, indent: "                    ");
            builder.Append("                    ").Append(parameterValueVarNames[i]).Append(" = ").Append(tmpVar).AppendLine(";");
            builder.Append("                    ").Append(parameterSeenVarNames[i]).AppendLine(" = true;");
            builder.AppendLine("                }");
        }

        // Buffered writable members next.
        for (var i = 0; i < bufferedMembers.Length; i++)
        {
            var member = bufferedMembers[i];
            var localValueVar = bufferedMemberValueVarNames[member.Symbol];
            var localSeenVar = bufferedMemberSeenVarNames[member.Symbol];

            var localModel = new MemberModel(
                member.Symbol,
                member.Type,
                member.SerializedNameExpressionForRead,
                member.SerializedNameExpressionForWrite,
                member.AccessExpression,
                rhs => localValueVar + " = " + rhs,
                member.IgnoreConditionExpression,
                member.AttributeConverterTypeName,
                member.IsRequired,
                member.IsInitOnly);

            builder.Append("                if (!matched && global::System.String.Equals(mergeKey, ").Append(member.SerializedNameExpressionForRead)
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("                {");
            builder.AppendLine("                    matched = true;");
            if (member.IsRequired && requiredVarBySymbol.TryGetValue(member.Symbol, out var requiredVar))
            {
                builder.Append("                    ").Append(requiredVar).AppendLine(" = true;");
            }
            EmitReadMemberValueWithCustomConverter(builder, localModel, indexByType);
            builder.Append("                    ").Append(localSeenVar).AppendLine(" = true;");
            builder.AppendLine("                }");
        }

        // Required read-only members (non-writable and not constructor-bound).
        for (var i = 0; i < requiredMembers.Length; i++)
        {
            var member = requiredMembers[i];
            if (IsWritableMember(member.Symbol) || ctorBoundMembers.Contains(member.Symbol))
            {
                continue;
            }

            builder.Append("                if (!matched && global::System.String.Equals(mergeKey, ").Append(member.SerializedNameExpressionForRead)
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("                {");
            builder.AppendLine("                    matched = true;");
            builder.Append("                    ").Append(requiredVarBySymbol[member.Symbol]).AppendLine(" = true;");
            if (extensionData is not null)
            {
                builder.AppendLine("                    BufferExtensionData(mergeKey);");
            }
            else
            {
                builder.AppendLine("                    reader.Skip();");
            }
            builder.AppendLine("                }");
        }

        builder.AppendLine();
        builder.AppendLine("                if (!matched)");
        builder.AppendLine("                {");
        if (extensionData is not null)
        {
            builder.AppendLine("                    BufferExtensionData(mergeKey);");
        }
        else
        {
            builder.AppendLine("                    reader.Skip();");
        }
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("            reader.Read();");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        reader.Read();");
        builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
        builder.AppendLine("            {");
        builder.AppendLine("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalarKey(reader);");
        builder.AppendLine("            }");
        builder.AppendLine("            var key = reader.ScalarValue ?? string.Empty;");
        builder.AppendLine("            reader.Read();");
        builder.AppendLine();
        builder.AppendLine("            if (mergeEnabled && global::System.String.Equals(key, \"<<\", global::System.StringComparison.Ordinal))");
        builder.AppendLine("            {");
        builder.AppendLine("                ReadAndApplyMerge();");
        builder.AppendLine("                continue;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            explicitKeys?.Add(key);");
        builder.AppendLine();
        builder.AppendLine("            var matched = false;");

        // Constructor parameters first.
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterName = parameter.Name ?? string.Empty;

            builder.Append("            if (!matched && global::System.String.Equals(key, ").Append(parameterYamlNameExpressions[i])
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("            {");
            builder.AppendLine("                matched = true;");
            if (requiredMembers.Length != 0)
            {
                // If the parameter binds to a required member, mark it as seen.
                for (var m = 0; m < members.Length; m++)
                {
                    var member = members[m];
                    if (!member.IsRequired)
                    {
                        continue;
                    }

                    if (string.Equals(member.Symbol.Name, parameterName, StringComparison.OrdinalIgnoreCase) &&
                        requiredVarBySymbol.TryGetValue(member.Symbol, out var requiredVar))
                    {
                        builder.Append("                ").Append(requiredVar).AppendLine(" = true;");
                    }
                }
            }

            var tmpVar = $"__ctor{index}_tmp{i}";
            EmitReadKnownType(builder, parameter.Type, indexByType, tmpVar, indent: "                ");
            builder.Append("                ").Append(parameterValueVarNames[i]).Append(" = ").Append(tmpVar).AppendLine(";");
            builder.Append("                ").Append(parameterSeenVarNames[i]).AppendLine(" = true;");
            builder.AppendLine("            }");
        }

        // Buffered writable members next.
        for (var i = 0; i < bufferedMembers.Length; i++)
        {
            var member = bufferedMembers[i];
            var localValueVar = bufferedMemberValueVarNames[member.Symbol];
            var localSeenVar = bufferedMemberSeenVarNames[member.Symbol];

            var localModel = new MemberModel(
                member.Symbol,
                member.Type,
                member.SerializedNameExpressionForRead,
                member.SerializedNameExpressionForWrite,
                member.AccessExpression,
                rhs => localValueVar + " = " + rhs,
                member.IgnoreConditionExpression,
                member.AttributeConverterTypeName,
                member.IsRequired,
                member.IsInitOnly);

            builder.Append("            if (!matched && global::System.String.Equals(key, ").Append(member.SerializedNameExpressionForRead)
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("            {");
            builder.AppendLine("                matched = true;");
            if (member.IsRequired && requiredVarBySymbol.TryGetValue(member.Symbol, out var requiredVar))
            {
                builder.Append("                ").Append(requiredVar).AppendLine(" = true;");
            }
            EmitReadMemberValueWithCustomConverter(builder, localModel, indexByType);
            builder.Append("                ").Append(localSeenVar).AppendLine(" = true;");
            builder.AppendLine("            }");
        }

        // Required read-only members (non-writable and not constructor-bound).
        for (var i = 0; i < requiredMembers.Length; i++)
        {
            var member = requiredMembers[i];
            if (IsWritableMember(member.Symbol) || ctorBoundMembers.Contains(member.Symbol))
            {
                continue;
            }

            builder.Append("            if (!matched && global::System.String.Equals(key, ").Append(member.SerializedNameExpressionForRead)
                .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
            builder.AppendLine();
            builder.AppendLine("            {");
            builder.AppendLine("                matched = true;");
            builder.Append("                ").Append(requiredVarBySymbol[member.Symbol]).AppendLine(" = true;");
            if (extensionData is not null)
            {
                builder.AppendLine("                BufferExtensionData(key);");
            }
            else
            {
                builder.AppendLine("                reader.Skip();");
            }
            builder.AppendLine("            }");
        }

        builder.AppendLine();
        builder.AppendLine("            if (!matched)");
        builder.AppendLine("            {");
        if (extensionData is not null)
        {
            builder.AppendLine("                BufferExtensionData(key);");
        }
        else
        {
            builder.AppendLine("                reader.Skip();");
        }
        builder.AppendLine("            }");
        builder.AppendLine("        }");

        builder.AppendLine();

        // Ensure all constructor parameters are satisfied.
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.HasExplicitDefaultValue)
            {
                var defaultExpr = GetOptionalParameterDefaultValueExpression(parameter);
                builder.Append("        if (!").Append(parameterSeenVarNames[i]).AppendLine(")");
                builder.AppendLine("        {");
                builder.Append("            ").Append(parameterValueVarNames[i]).Append(" = ").Append(defaultExpr).AppendLine(";");
                builder.AppendLine("        }");
            }
            else
            {
                builder.Append("        if (!").Append(parameterSeenVarNames[i]).AppendLine(")");
                builder.AppendLine("        {");
                builder.Append("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowMissingRequiredConstructorParameter(reader, mappingStart, typeof(").Append(typeName).Append("), ").Append(ToLiteral(parameter.Name ?? string.Empty)).AppendLine(");");
                builder.AppendLine("        }");
            }
        }

        builder.AppendLine();
        if (useObjectInitializer)
        {
            builder.Append("        var __defaults").Append(index).Append(" = default(").Append(typeName);
            if (typeSymbol.IsReferenceType)
            {
                builder.Append('?');
            }

            builder.AppendLine(");");
            if (extensionData is { IsInitOnly: true })
            {
                builder.Append("        __defaults").Append(index).Append(" = new ").Append(typeName).Append('(');
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i != 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(parameterValueVarNames[i]);
                }

                builder.AppendLine(");");
            }
            else
            {
                builder.Append("        if (");
                for (var i = 0; i < initOnlyMembers.Length; i++)
                {
                    if (i != 0)
                    {
                        builder.Append(" || ");
                    }

                    builder.Append('!').Append(bufferedMemberSeenVarNames[initOnlyMembers[i].Symbol]);
                }

                builder.AppendLine(")");
                builder.AppendLine("        {");
                builder.Append("            __defaults").Append(index).Append(" = new ").Append(typeName).Append('(');
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i != 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(parameterValueVarNames[i]);
                }

                builder.AppendLine(");");
                builder.AppendLine("        }");
            }

            if (extensionData is { IsInitOnly: true } initOnlyExtensionData)
            {
                builder.Append("        var ").Append(initOnlyExtensionDataValueVarName).Append(" = __defaults").Append(index);
                if (typeSymbol.IsReferenceType)
                {
                    builder.Append('!');
                }

                builder.Append('.').Append(initOnlyExtensionData.Symbol.Name).AppendLine(";");
                builder.AppendLine("        if (extensionEntries is not null && extensionEntries.Count != 0)");
                builder.AppendLine("        {");
                if (initOnlyExtensionData.Kind == ExtensionDataKind.Dictionary)
                {
                    var valueTypeName = (initOnlyExtensionData.DictionaryValueType ?? throw new InvalidOperationException("Extension data dictionary value type is missing."))
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    builder.Append("            var container = ").Append(initOnlyExtensionDataValueVarName).AppendLine(";");
                    builder.AppendLine("            if (container is null)");
                    builder.AppendLine("            {");
                    builder.Append("                container = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                        .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
                    builder.Append("                ").Append(initOnlyExtensionDataValueVarName).AppendLine(" = container;");
                    builder.AppendLine("            }");
                    builder.AppendLine("            for (var i = 0; i < extensionEntries.Count; i++)");
                    builder.AppendLine("            {");
                    builder.AppendLine("                var entry = extensionEntries[i];");
                    builder.AppendLine("                container[entry.Key] = entry.Value;");
                    builder.AppendLine("            }");
                }
                else
                {
                    builder.Append("            var mapping = ").Append(initOnlyExtensionDataValueVarName).AppendLine(";");
                    builder.AppendLine("            if (mapping is null)");
                    builder.AppendLine("            {");
                    builder.AppendLine("                mapping = new global::SharpYaml.Model.YamlMapping();");
                    builder.Append("                ").Append(initOnlyExtensionDataValueVarName).AppendLine(" = mapping;");
                    builder.AppendLine("            }");
                    builder.AppendLine("            var list = (global::System.Collections.Generic.IList<global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>>)mapping;");
                    builder.AppendLine("            for (var entryIndex = 0; entryIndex < extensionEntries.Count; entryIndex++)");
                    builder.AppendLine("            {");
                    builder.AppendLine("                var entry = extensionEntries[entryIndex];");
                    builder.AppendLine("                var extensionKey = entry.Key;");
                    builder.AppendLine("                var extensionValue = entry.Value;");
                    builder.AppendLine("                var replaced = false;");
                    builder.AppendLine("                for (var i = 0; i < list.Count; i++)");
                    builder.AppendLine("                {");
                    builder.AppendLine("                    var pair = list[i];");
                    builder.AppendLine("                    if (pair.Key is global::SharpYaml.Model.YamlValue keyValue && global::System.String.Equals(keyValue.Value, extensionKey, global::System.StringComparison.Ordinal))");
                    builder.AppendLine("                    {");
                    builder.AppendLine("                        list[i] = new global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>(pair.Key, extensionValue);");
                    builder.AppendLine("                        replaced = true;");
                    builder.AppendLine("                        break;");
                    builder.AppendLine("                    }");
                    builder.AppendLine("                }");
                    builder.AppendLine("                if (!replaced)");
                    builder.AppendLine("                {");
                    builder.AppendLine("                    mapping.Add(new global::SharpYaml.Model.YamlValue(extensionKey), extensionValue);");
                    builder.AppendLine("                }");
                    builder.AppendLine("            }");
                }

                builder.AppendLine("        }");
            }

            builder.AppendLine();
        }

        builder.Append("        var instance = new ").Append(typeName).Append('(');
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i != 0)
            {
                builder.Append(", ");
            }
            builder.Append(parameterValueVarNames[i]);
        }

        if (!useObjectInitializer)
        {
            builder.AppendLine(");");
        }
        else
        {
            builder.AppendLine(")");
            builder.AppendLine("        {");
            for (var i = 0; i < initOnlyMembers.Length; i++)
            {
                var member = initOnlyMembers[i];
                var seenVar = bufferedMemberSeenVarNames[member.Symbol];
                var valueVar = bufferedMemberValueVarNames[member.Symbol];

                builder.Append("            ").Append(member.Symbol.Name).Append(" = ").Append(seenVar).Append(" ? ").Append(valueVar).Append(" : __defaults").Append(index);
                if (typeSymbol.IsReferenceType)
                {
                    builder.Append('!');
                }

                builder.Append('.').Append(member.Symbol.Name).AppendLine(",");
            }

            if (extensionData is { IsInitOnly: true } initOnlyExtensionData)
            {
                builder.Append("            ").Append(initOnlyExtensionData.Symbol.Name).Append(" = ").Append(initOnlyExtensionDataValueVarName).AppendLine(",");
            }

            builder.AppendLine("        };");
        }

        builder.AppendLine("        if (instanceAnchor is not null) { reader.RegisterAnchor(instanceAnchor, instance); }");

        if (emitLifecycleCallbacks)
        {
            builder.AppendLine();
            builder.AppendLine("        if (instance is global::SharpYaml.Serialization.IYamlOnDeserializing onDeserializing)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                onDeserializing.OnDeserializing();");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (global::System.Exception exception)");
            builder.AppendLine("            {");
            builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowCallbackInvocationFailed(reader, typeof(").Append(typeName).AppendLine("), \"IYamlOnDeserializing.OnDeserializing\", exception);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
        }

        // Apply buffered writable members.
        for (var i = 0; i < postCreateBufferedMembers.Length; i++)
        {
            var member = postCreateBufferedMembers[i];
            var localValueVar = bufferedMemberValueVarNames[member.Symbol];
            var localSeenVar = bufferedMemberSeenVarNames[member.Symbol];
            builder.AppendLine();
            builder.Append("        if (").Append(localSeenVar).AppendLine(")");
            builder.AppendLine("        {");
            builder.Append("            ").Append(member.AssignExpression(localValueVar)).AppendLine(";");
            builder.AppendLine("        }");
        }

        // Apply buffered extension entries.
        if (extensionData is not null && !extensionData.IsInitOnly)
        {
            builder.AppendLine();
            builder.AppendLine("        if (extensionEntries is not null)");
            builder.AppendLine("        {");
            if (extensionData.Kind == ExtensionDataKind.Dictionary)
            {
                var valueTypeName = (extensionData.DictionaryValueType ?? throw new InvalidOperationException("Extension data dictionary value type is missing."))
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                builder.Append("            var container = instance.").Append(extensionData.Symbol.Name).AppendLine(";");
                builder.AppendLine("            if (container is null)");
                builder.AppendLine("            {");
                builder.Append("                container = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                    .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
                if (extensionData.CanAssign)
                {
                    builder.Append("                instance.").Append(extensionData.Symbol.Name).AppendLine(" = container;");
                }
                else
                {
                    builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"Extension data member '")
                        .Append(extensionData.Symbol.Name).Append("' could not be assigned.\");").AppendLine();
                }
                builder.AppendLine("            }");
                builder.AppendLine("            for (var i = 0; i < extensionEntries.Count; i++)");
                builder.AppendLine("            {");
                builder.AppendLine("                var entry = extensionEntries[i];");
                builder.AppendLine("                container[entry.Key] = entry.Value;");
                builder.AppendLine("            }");
            }
            else
            {
                builder.Append("            var mapping = instance.").Append(extensionData.Symbol.Name).AppendLine(";");
                builder.AppendLine("            if (mapping is null)");
                builder.AppendLine("            {");
                builder.AppendLine("                mapping = new global::SharpYaml.Model.YamlMapping();");
                if (extensionData.CanAssign)
                {
                    builder.Append("                instance.").Append(extensionData.Symbol.Name).AppendLine(" = mapping;");
                }
                else
                {
                    builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"Extension data member '")
                        .Append(extensionData.Symbol.Name).Append("' could not be assigned.\");").AppendLine();
                }
                builder.AppendLine("            }");
                builder.AppendLine("            var list = (global::System.Collections.Generic.IList<global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>>)mapping;");
                builder.AppendLine("            for (var entryIndex = 0; entryIndex < extensionEntries.Count; entryIndex++)");
                builder.AppendLine("            {");
                builder.AppendLine("                var entry = extensionEntries[entryIndex];");
                builder.AppendLine("                var extensionKey = entry.Key;");
                builder.AppendLine("                var extensionValue = entry.Value;");
                builder.AppendLine("                var replaced = false;");
                builder.AppendLine("                for (var i = 0; i < list.Count; i++)");
                builder.AppendLine("                {");
                builder.AppendLine("                    var pair = list[i];");
                builder.AppendLine("                    if (pair.Key is global::SharpYaml.Model.YamlValue keyValue && global::System.String.Equals(keyValue.Value, extensionKey, global::System.StringComparison.Ordinal))");
                builder.AppendLine("                    {");
                builder.AppendLine("                        list[i] = new global::System.Collections.Generic.KeyValuePair<global::SharpYaml.Model.YamlElement, global::SharpYaml.Model.YamlElement?>(pair.Key, extensionValue);");
                builder.AppendLine("                        replaced = true;");
                builder.AppendLine("                        break;");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!replaced)");
                builder.AppendLine("                {");
                builder.AppendLine("                    mapping.Add(new global::SharpYaml.Model.YamlValue(extensionKey), extensionValue);");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
            }
            builder.AppendLine("        }");
        }

        // Required member checks.
        if (requiredMembers.Length != 0)
        {
            builder.AppendLine();
            builder.Append("        if (");
            for (var i = 0; i < requiredMembers.Length; i++)
            {
                if (i != 0)
                {
                    builder.Append(" || ");
                }
                builder.Append("!").Append(requiredVarBySymbol[requiredMembers[i].Symbol]);
            }
            builder.AppendLine(")");
            builder.AppendLine("        {");
            builder.AppendLine("            var missing = new global::System.Collections.Generic.List<string>();");
            for (var i = 0; i < requiredMembers.Length; i++)
            {
                var requiredVar = requiredVarBySymbol[requiredMembers[i].Symbol];
                builder.Append("            if (!").Append(requiredVar).AppendLine(")");
                builder.AppendLine("            {");
                builder.Append("                missing.Add(").Append(requiredMembers[i].SerializedNameExpressionForRead).AppendLine(");");
                builder.AppendLine("            }");
            }
            builder.Append("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowMissingRequiredMembers(reader, mappingStart, typeof(").Append(typeName).AppendLine("), missing);");
            builder.AppendLine("        }");
        }

        if (emitLifecycleCallbacks)
        {
            builder.AppendLine();
            builder.AppendLine("        if (instance is global::SharpYaml.Serialization.IYamlOnDeserialized onDeserialized)");
            builder.AppendLine("        {");
            builder.AppendLine("            try");
            builder.AppendLine("            {");
            builder.AppendLine("                onDeserialized.OnDeserialized();");
            builder.AppendLine("            }");
            builder.AppendLine("            catch (global::System.Exception exception)");
            builder.AppendLine("            {");
            builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowCallbackInvocationFailed(reader, typeof(").Append(typeName).AppendLine("), \"IYamlOnDeserialized.OnDeserialized\", exception);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
        }

        builder.AppendLine();
        builder.AppendLine("        reader.Read();");
        builder.AppendLine("        return instance;");
    }

    private static void EmitReadValue(StringBuilder builder, int index, ITypeSymbol typeSymbol, string typeName, Dictionary<ITypeSymbol, int> indexByType, JsonNamingPolicy? propertyNamingPolicy)
    {
        var attributeConverterTypeName = GetYamlConverterAttributeTypeName((ISymbol)typeSymbol);

        builder.Append("    private static ").Append(typeName).Append(typeSymbol.IsReferenceType ? "?" : string.Empty).Append(" ReadValue").Append(index)
            .AppendLine("(global::SharpYaml.Serialization.YamlReader reader)");
        builder.AppendLine("    {");
        builder.AppendLine("        var options = reader.Options;");
        builder.AppendLine("        var hasCustomConverters = options.Converters.Count != 0;");
        builder.AppendLine();

        if (attributeConverterTypeName is not null)
        {
            builder.Append("        global::SharpYaml.Serialization.YamlConverter attributeConverter = new ").Append(attributeConverterTypeName).AppendLine("();");
            builder.AppendLine("        if (attributeConverter is global::SharpYaml.Serialization.YamlConverterFactory factory)");
            builder.AppendLine("        {");
            builder.Append("            var created = factory.CreateConverter(typeof(").Append(typeName).AppendLine("), options);");
            builder.AppendLine("            if (created is null || !created.CanConvert(typeof(" + typeName + ")))");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new global::System.InvalidOperationException(\"Converter factory returned an invalid converter.\");");
            builder.AppendLine("            }");
            builder.Append("            return (").Append(typeName).AppendLine(")created.Read(reader, typeof(" + typeName + "))!;");
            builder.AppendLine("        }");
            builder.Append("        return (").Append(typeName).AppendLine(")attributeConverter.Read(reader, typeof(" + typeName + "))!;");
            builder.AppendLine("    }");
            return;
        }

        builder.Append("        if (hasCustomConverters && reader.TryGetCustomConverter(typeof(").Append(typeName).AppendLine("), out var rootCustomConverter) && rootCustomConverter is not null)");
        builder.AppendLine("        {");
        builder.Append("            var untyped = rootCustomConverter.Read(reader, typeof(").Append(typeName).AppendLine("));");
        builder.AppendLine("            if (untyped is null)");
        builder.AppendLine("            {");
        builder.AppendLine("                return default;");
        builder.AppendLine("            }");
        builder.Append("            return (").Append(typeName).AppendLine(")untyped;");
        builder.AppendLine("        }");
        builder.AppendLine();

        if (typeSymbol is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");

            EmitReadScalar(builder, underlyingType, "value");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return value;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        var text = reader.ScalarValue ?? string.Empty;");
            builder.Append("        if (global::System.Enum.TryParse<").Append(typeName).AppendLine(">(text, ignoreCase: true, out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return parsed;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var numeric))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.Append("            return (").Append(typeName).AppendLine(")global::System.Enum.ToObject(typeof(" + typeName + "), numeric);");
            builder.AppendLine("        }");
            builder.AppendLine("        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidEnumScalar(reader, text);");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetArrayElementType(typeSymbol, out var arrayElementType))
        {
            var elementTypeName = arrayElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("        if (reader.TryReadAlias(out var rootAliasValue))");
            builder.AppendLine("        {");
            builder.Append("            return (").Append(typeName).AppendLine(")rootAliasValue!;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedSequence(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        var rootAnchor = reader.Anchor;");
            builder.AppendLine("        reader.Read();");
            builder.Append("        var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
            builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
            builder.AppendLine("        {");
            EmitReadKnownType(builder, arrayElementType, indexByType, "element", indent: "            ");
            builder.AppendLine("            list.Add(element);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        var array = list.ToArray();");
            builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, array); }");
            builder.AppendLine("        return array;");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetSequenceElementType(typeSymbol, out var sequenceElementType, out var sequenceKind))
        {
            var elementTypeName = sequenceElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("        if (reader.TryReadAlias(out var rootAliasValue))");
            builder.AppendLine("        {");
            builder.Append("            return (").Append(typeName).AppendLine(")rootAliasValue!;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedSequence(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        var rootAnchor = reader.Anchor;");
            builder.AppendLine("        reader.Read();");

            switch (sequenceKind)
            {
                case SequenceKind.Set:
                    builder.Append("        var set = new global::System.Collections.Generic.HashSet<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, set); }");
                    builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("        {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                    builder.AppendLine("            set.Add(element);");
                    builder.AppendLine("        }");
                    builder.AppendLine("        reader.Read();");
                    builder.AppendLine("        return set;");
                    builder.AppendLine("    }");
                    return;

                case SequenceKind.ImmutableArray:
                    builder.Append("        var builderArray = global::System.Collections.Immutable.ImmutableArray.CreateBuilder<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("        {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                    builder.AppendLine("            builderArray.Add(element);");
                    builder.AppendLine("        }");
                    builder.AppendLine("        reader.Read();");
                    builder.AppendLine("        var immutableArray = builderArray.ToImmutable();");
                    builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, immutableArray); }");
                    builder.AppendLine("        return immutableArray;");
                    builder.AppendLine("    }");
                    return;

                case SequenceKind.ImmutableList:
                    builder.Append("        var builderList = global::System.Collections.Immutable.ImmutableList.CreateBuilder<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("        {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                    builder.AppendLine("            builderList.Add(element);");
                    builder.AppendLine("        }");
                    builder.AppendLine("        reader.Read();");
                    builder.AppendLine("        var immutableList = builderList.ToImmutable();");
                    builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, immutableList); }");
                    builder.AppendLine("        return immutableList;");
                    builder.AppendLine("    }");
                    return;

                case SequenceKind.ImmutableHashSet:
                    builder.Append("        var builderSet = global::System.Collections.Immutable.ImmutableHashSet.CreateBuilder<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("        {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                    builder.AppendLine("            builderSet.Add(element);");
                    builder.AppendLine("        }");
                    builder.AppendLine("        reader.Read();");
                    builder.AppendLine("        var immutableSet = builderSet.ToImmutable();");
                    builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, immutableSet); }");
                    builder.AppendLine("        return immutableSet;");
                    builder.AppendLine("    }");
                    return;

                default:
                    builder.Append("        var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, list); }");
                    builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("        {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "            ");
                    builder.AppendLine("            list.Add(element);");
                    builder.AppendLine("        }");
                    builder.AppendLine("        reader.Read();");
                    builder.AppendLine("        return list;");
                    builder.AppendLine("    }");
                    return;
            }
        }

        if (TryGetDictionaryTypes(typeSymbol, out var dictionaryKeyType, out var dictionaryValueType, out _))
        {
            var keyTypeName = dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var valueTypeName = dictionaryValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("        if (reader.TryReadAlias(out var rootAliasValue))");
            builder.AppendLine("        {");
            builder.Append("            return (").Append(typeName).AppendLine(")rootAliasValue!;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedMapping(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        var rootAnchor = reader.Anchor;");
            builder.AppendLine("        reader.Read();");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.Append("        var dictionary = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                    .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
                builder.AppendLine("        var mergeEnabled = options.Schema is global::SharpYaml.YamlSchemaKind.Core or global::SharpYaml.YamlSchemaKind.Extended;");
                builder.AppendLine("        global::System.Collections.Generic.HashSet<string>? explicitKeys = mergeEnabled");
                builder.AppendLine("            ? new global::System.Collections.Generic.HashSet<string>(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal)");
                builder.AppendLine("            : null;");
                builder.AppendLine("        global::System.Collections.Generic.HashSet<string>? seenKeys = mergeEnabled");
                builder.AppendLine("            ? new global::System.Collections.Generic.HashSet<string>(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal)");
                builder.AppendLine("            : null;");
            }
            else
            {
                builder.Append("        var dictionary = new global::System.Collections.Generic.Dictionary<").Append(keyTypeName).Append(", ").Append(valueTypeName).AppendLine(">();");
            }

            builder.AppendLine("        if (rootAnchor is not null) { reader.RegisterAnchor(rootAnchor, dictionary); }");
            builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
            builder.AppendLine("        {");
                builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("            {");
                builder.AppendLine("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalarKey(reader);");
                builder.AppendLine("            }");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("            var key = reader.ScalarValue ?? string.Empty;");
            }
            else
            {
                builder.Append("            var key = default(").Append(keyTypeName).AppendLine(");");
                builder.AppendLine("            {");
                EmitReadScalarAssignment(builder, dictionaryKeyType, "key", indent: "                ");
                builder.AppendLine("            }");
            }
            builder.AppendLine("            reader.Read();");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("            if (mergeEnabled && global::System.String.Equals(key, \"<<\", global::System.StringComparison.Ordinal))");
                builder.AppendLine("            {");
                builder.AppendLine("                if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
                builder.AppendLine("                {");
                builder.AppendLine("                    reader.Read();");
                builder.AppendLine("                }");
                builder.AppendLine("                else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartMapping || reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Alias)");
                builder.AppendLine("                {");
                builder.Append("                    var merged = ReadValue").Append(index).AppendLine("(reader);");
                builder.AppendLine("                    if (merged is not null)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        foreach (var pair in merged)");
                builder.AppendLine("                        {");
                builder.AppendLine("                            if (explicitKeys is not null && explicitKeys.Contains(pair.Key))");
                builder.AppendLine("                            {");
                builder.AppendLine("                                continue;");
                builder.AppendLine("                            }");
                builder.AppendLine("                            dictionary[pair.Key] = pair.Value;");
                builder.AppendLine("                        }");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("                else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
                builder.AppendLine("                {");
                builder.AppendLine("                    reader.Read();");
                builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                builder.AppendLine("                    {");
                builder.AppendLine("                        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping && reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Alias)");
                builder.AppendLine("                        {");
                builder.AppendLine("                            throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge sequence entries must be mappings.\");");
                builder.AppendLine("                        }");
                builder.Append("                        var merged = ReadValue").Append(index).AppendLine("(reader);");
                builder.AppendLine("                        if (merged is not null)");
                builder.AppendLine("                        {");
                builder.AppendLine("                            foreach (var pair in merged)");
                builder.AppendLine("                            {");
                builder.AppendLine("                                if (explicitKeys is not null && explicitKeys.Contains(pair.Key))");
                builder.AppendLine("                                {");
                builder.AppendLine("                                    continue;");
                builder.AppendLine("                                }");
                builder.AppendLine("                                dictionary[pair.Key] = pair.Value;");
                builder.AppendLine("                            }");
                builder.AppendLine("                        }");
                builder.AppendLine("                    }");
                builder.AppendLine("                    reader.Read();");
                builder.AppendLine("                }");
                builder.AppendLine("                else");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge key value must be a mapping or a sequence of mappings.\");");
                builder.AppendLine("                }");
                builder.AppendLine("                continue;");
                builder.AppendLine("            }");
                builder.AppendLine("            explicitKeys?.Add(key);");
            }

            EmitReadKnownType(builder, dictionaryValueType, indexByType, "value", indent: "            ");
            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("            if (seenKeys is not null && !seenKeys.Add(key))");
                builder.AppendLine("            {");
                builder.AppendLine("                switch (options.DuplicateKeyHandling)");
                builder.AppendLine("                {");
                builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.Error:");
                builder.AppendLine("                        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowDuplicateMappingKey(reader, key);");
                builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.FirstWins:");
                builder.AppendLine("                        break;");
                builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.LastWins:");
                builder.AppendLine("                        dictionary[key] = value;");
                builder.AppendLine("                        break;");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("            else");
                builder.AppendLine("            {");
                builder.AppendLine("                dictionary[key] = value;");
                builder.AppendLine("            }");
            }
            else
            {
                builder.AppendLine("            if (dictionary.ContainsKey(key))");
                builder.AppendLine("            {");
                builder.AppendLine("                switch (options.DuplicateKeyHandling)");
                builder.AppendLine("                {");
                builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.Error:");
                builder.AppendLine("                        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowDuplicateMappingKey(reader, key.ToString() ?? string.Empty);");
                builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.FirstWins:");
                builder.AppendLine("                        break;");
                builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.LastWins:");
                builder.AppendLine("                        dictionary[key] = value;");
                builder.AppendLine("                        break;");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("            else");
                builder.AppendLine("            {");
                builder.AppendLine("                dictionary[key] = value;");
                builder.AppendLine("            }");
            }
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return dictionary;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        var value = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("        if (global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return null;");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return value;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidBooleanScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int32)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int64)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt32)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt32Scalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt64)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt64Scalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Byte)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsed) || parsed > byte.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidByteScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (byte)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_SByte)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsed) || parsed is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidSByteScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (sbyte)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int16)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsed) || parsed is < short.MinValue or > short.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt16Scalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (short)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt16)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsed) || parsed > ushort.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt16Scalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (ushort)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNIntScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (nint)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNUIntScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (nuint)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader, out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return (float)parsed;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Decimal)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(reader, out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDecimalScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return result;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("        }");
            builder.AppendLine("        var text = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("        if (text.Length != 1) { throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidCharScalar(reader, text); }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return text[0];");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol is INamedTypeSymbol named && (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct))
        {
            if (named.TypeKind == TypeKind.Class)
            {
                builder.AppendLine("        if (reader.TryReadAlias(out var rootAliasValue))");
                builder.AppendLine("        {");
                builder.Append("            return (").Append(typeName).AppendLine(")rootAliasValue!;");
                builder.AppendLine("        }");
                builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
                builder.AppendLine("        {");
                builder.AppendLine("            reader.Read();");
                builder.AppendLine("            return default;");
                builder.AppendLine("        }");
            }

            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedMapping(reader);");
            builder.AppendLine("        }");

            var extensionData = TryCreateExtensionDataMemberModel(named);
            var members = GetSerializableMembers(named)
                .Where(m => extensionData is null || !SymbolEqualityComparer.Default.Equals(m, extensionData.Symbol))
                .Select(m => CreateMemberModel(m, propertyNamingPolicy))
                .ToImmutableArray();

            if (named.TypeKind == TypeKind.Class && TryGetPolymorphismInfo(named, out var polymorphism) && polymorphism.DerivedTypes.Length != 0)
            {
                builder.AppendLine("        var rootTag = reader.Tag;");

                var discriminatorPropertyNameExpression = polymorphism.DiscriminatorPropertyNameOverride is null
                    ? "options.PolymorphismOptions.TypeDiscriminatorPropertyName"
                    : ToLiteral(polymorphism.DiscriminatorPropertyNameOverride);
                var discriminatorStyleExpression = polymorphism.DiscriminatorStyleOverrideValue is null
                    ? "options.PolymorphismOptions.DiscriminatorStyle"
                    : $"(global::SharpYaml.YamlTypeDiscriminatorStyle){polymorphism.DiscriminatorStyleOverrideValue.Value}";
                var unknownHandlingExpression = polymorphism.UnknownDerivedTypeHandlingOverrideValue is null
                    ? "options.PolymorphismOptions.UnknownDerivedTypeHandling"
                    : $"(global::SharpYaml.YamlUnknownDerivedTypeHandling){polymorphism.UnknownDerivedTypeHandlingOverrideValue.Value}";

                builder.Append("        var discriminatorPropertyName = ").Append(discriminatorPropertyNameExpression).AppendLine(";");
                builder.Append("        var discriminatorStyle = ").Append(discriminatorStyleExpression).AppendLine(";");
                builder.Append("        var unknownDerivedTypeHandling = ").Append(unknownHandlingExpression).AppendLine(";");
                builder.AppendLine("        var buffered = global::SharpYaml.Serialization.YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, discriminatorPropertyName, out var discriminatorValue);");
                builder.AppendLine("        var bufferedReader = reader.CreateReader(buffered);");
                builder.AppendLine("        if (!bufferedReader.Read()) { return default; }");

                builder.AppendLine("        if (discriminatorStyle is not global::SharpYaml.YamlTypeDiscriminatorStyle.Tag && discriminatorValue is not null)");
                builder.AppendLine("        {");
                for (var i = 0; i < polymorphism.DerivedTypes.Length; i++)
                {
                    var derived = polymorphism.DerivedTypes[i];
                    if (derived.Discriminator is null)
                    {
                        continue;
                    }

                    if (!indexByType.TryGetValue(derived.DerivedType, out var derivedIndex))
                    {
                        continue;
                    }

                    builder.Append("            if (global::System.String.Equals(discriminatorValue, ").Append(ToLiteral(derived.Discriminator))
                        .AppendLine(", global::System.StringComparison.Ordinal))");
                    builder.AppendLine("            {");
                    builder.Append("                return (").Append(typeName).Append(")ReadValue").Append(derivedIndex).AppendLine("(bufferedReader)!;");
                    builder.AppendLine("            }");
                }

                // When discriminator value is present but unrecognized, try default type before failing
                if (polymorphism.DefaultDerivedType is not null && indexByType.TryGetValue(polymorphism.DefaultDerivedType, out var defaultIndexForUnknown))
                {
                    builder.Append("            return (").Append(typeName).Append(")ReadValue").Append(defaultIndexForUnknown).AppendLine("(bufferedReader)!;");
                }
                else
                {
                    builder.AppendLine("            if (unknownDerivedTypeHandling == global::SharpYaml.YamlUnknownDerivedTypeHandling.Fail)");
                    builder.AppendLine("            {");
                    builder.Append("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowUnknownTypeDiscriminator(bufferedReader, discriminatorValue, typeof(").Append(typeName).AppendLine("));");
                    builder.AppendLine("            }");
                }
                builder.AppendLine("        }");

                builder.AppendLine("        if (discriminatorStyle is not global::SharpYaml.YamlTypeDiscriminatorStyle.Property && rootTag is not null)");
                builder.AppendLine("        {");
                for (var i = 0; i < polymorphism.DerivedTypes.Length; i++)
                {
                    var derived = polymorphism.DerivedTypes[i];
                    if (derived.Tag is null)
                    {
                        continue;
                    }

                    if (!indexByType.TryGetValue(derived.DerivedType, out var derivedIndex))
                    {
                        continue;
                    }

                    builder.Append("            if (global::System.String.Equals(rootTag, ").Append(ToLiteral(derived.Tag))
                        .AppendLine(", global::System.StringComparison.Ordinal))");
                    builder.AppendLine("            {");
                    builder.Append("                return (").Append(typeName).Append(")ReadValue").Append(derivedIndex).AppendLine("(bufferedReader)!;");
                    builder.AppendLine("            }");
                }
                builder.AppendLine("        }");

                // Fallback: use default derived type if available
                if (polymorphism.DefaultDerivedType is not null && indexByType.TryGetValue(polymorphism.DefaultDerivedType, out var defaultIndex))
                {
                    builder.Append("        return (").Append(typeName).Append(")ReadValue").Append(defaultIndex).AppendLine("(bufferedReader)!;");
                }
                else if (named.IsAbstract)
                {
                    builder.Append("        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowAbstractTypeWithoutDiscriminator(bufferedReader, typeof(").Append(typeName).AppendLine("));");
                }
                else
                {
                    builder.Append("        return ReadObjectCore").Append(index).AppendLine("(bufferedReader);");
                }
                builder.AppendLine("    }");
                builder.AppendLine();
                EmitReadObjectCoreMethod(builder, index, typeSymbol, typeName, members, extensionData, indexByType, propertyNamingPolicy);
                return;
            }

            builder.Append("        return ReadObjectCore").Append(index).AppendLine("(reader);");
            builder.AppendLine("    }");
            builder.AppendLine();
            EmitReadObjectCoreMethod(builder, index, typeSymbol, typeName, members, extensionData, indexByType, propertyNamingPolicy);
            return;
        }

        builder.AppendLine("        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"The generated YAML serializer does not support this type.\");");
        builder.AppendLine("    }");
    }

    private static void EmitWriteMemberValue(StringBuilder builder, MemberModel member, Dictionary<ITypeSymbol, int> indexByType, string valueExpression, string indent)
    {
        var innerIndent = indent + "    ";

        if (member.Type is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.Append(indent).Append("if (!").Append(valueExpression).AppendLine(".HasValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("writer.WriteNullValue();");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).Append("var underlying = ").Append(valueExpression).AppendLine(".Value;");

            if (!TryEmitWriteScalar(builder, underlyingType, "underlying", indent: innerIndent))
            {
                builder.Append(innerIndent).AppendLine("throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this nullable member type.\");");
            }

            builder.Append(indent).AppendLine("}");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_String)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Double)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Single)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Char)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString());");
            return;
        }

        if (TryEmitWriteScalar(builder, member.Type, valueExpression, indent))
        {
            return;
        }

        if (TryGetArrayElementType(member.Type, out var arrayElementType))
        {
            builder.Append(indent).Append("if (").Append(valueExpression).AppendLine(" is null)");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("writer.WriteNullValue();");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).Append("if (writer.TryWriteReference(").Append(valueExpression).AppendLine("))");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("}");
            builder.Append(innerIndent).AppendLine("else");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent + "    ").AppendLine("writer.WriteStartSequence();");
            builder.Append(innerIndent + "    ").Append("for (var i = 0; i < ").Append(valueExpression).AppendLine(".Length; i++)");
            builder.Append(innerIndent + "    ").AppendLine("{");
            builder.Append(innerIndent + "        ").Append("var element = ").Append(valueExpression).AppendLine("[i];");
            EmitWriteKnownType(builder, arrayElementType, indexByType, "element", indent: innerIndent + "        ");
            builder.Append(innerIndent + "    ").AppendLine("}");
            builder.Append(innerIndent + "    ").AppendLine("writer.WriteEndSequence();");
            builder.Append(innerIndent).AppendLine("}");
            builder.Append(indent).AppendLine("}");
            return;
        }

        if (TryGetSequenceElementType(member.Type, out var sequenceElementType, out var sequenceKind))
        {
            if (sequenceKind == SequenceKind.ImmutableArray)
            {
                builder.Append(indent).Append("if (").Append(valueExpression).AppendLine(".IsDefault)");
                builder.Append(indent).AppendLine("{");
                builder.Append(innerIndent).AppendLine("writer.WriteNullValue();");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).AppendLine("else");
                builder.Append(indent).AppendLine("{");
                builder.Append(innerIndent).AppendLine("writer.WriteStartSequence();");
                builder.Append(innerIndent).Append("for (var i = 0; i < ").Append(valueExpression).AppendLine(".Length; i++)");
                builder.Append(innerIndent).AppendLine("{");
                builder.Append(innerIndent + "    ").Append("var element = ").Append(valueExpression).AppendLine("[i];");
                EmitWriteKnownType(builder, sequenceElementType, indexByType, "element", indent: innerIndent + "    ");
                builder.Append(innerIndent).AppendLine("}");
                builder.Append(innerIndent).AppendLine("writer.WriteEndSequence();");
                builder.Append(indent).AppendLine("}");
                return;
            }

            builder.Append(indent).Append("if (").Append(valueExpression).AppendLine(" is null)");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("writer.WriteNullValue();");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).Append("if (writer.TryWriteReference(").Append(valueExpression).AppendLine("))");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("}");
            builder.Append(innerIndent).AppendLine("else");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent + "    ").AppendLine("writer.WriteStartSequence();");
            if (sequenceKind == SequenceKind.List)
            {
                builder.Append(innerIndent + "    ").Append("for (var i = 0; i < ").Append(valueExpression).AppendLine(".Count; i++)");
                builder.Append(innerIndent + "    ").AppendLine("{");
                builder.Append(innerIndent + "        ").Append("var element = ").Append(valueExpression).AppendLine("[i];");
                EmitWriteKnownType(builder, sequenceElementType, indexByType, "element", indent: innerIndent + "        ");
                builder.Append(innerIndent + "    ").AppendLine("}");
            }
            else
            {
                builder.Append(innerIndent + "    ").Append("foreach (var element in ").Append(valueExpression).AppendLine(")");
                builder.Append(innerIndent + "    ").AppendLine("{");
                EmitWriteKnownType(builder, sequenceElementType, indexByType, "element", indent: innerIndent + "        ");
                builder.Append(innerIndent + "    ").AppendLine("}");
            }
            builder.Append(innerIndent + "    ").AppendLine("writer.WriteEndSequence();");
            builder.Append(innerIndent).AppendLine("}");
            builder.Append(indent).AppendLine("}");
            return;
        }

        if (TryGetDictionaryTypes(member.Type, out var dictionaryKeyType, out var dictionaryValueType, out _))
        {
            builder.Append(indent).Append("if (").Append(valueExpression).AppendLine(" is null)");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("writer.WriteNullValue();");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(innerIndent).Append("if (writer.TryWriteReference(").Append(valueExpression).AppendLine("))");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("}");
            builder.Append(innerIndent).AppendLine("else");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent + "    ").AppendLine("writer.WriteStartMapping();");
            builder.Append(innerIndent + "    ").Append("foreach (var pair in ").Append(valueExpression).AppendLine(")");
            builder.Append(innerIndent + "    ").AppendLine("{");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.Append(innerIndent + "        ").AppendLine("var key = writer.ConvertDictionaryKey(pair.Key);");
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(key);");
            }
            else if (dictionaryKeyType is INamedTypeSymbol enumType2 && enumType2.TypeKind == TypeKind.Enum)
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(pair.Key.ToString());");
            }
            else if (dictionaryKeyType.SpecialType == SpecialType.System_Boolean)
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(pair.Key ? \"true\" : \"false\");");
            }
            else if (dictionaryKeyType.SpecialType == SpecialType.System_Double)
            {
                builder.Append(innerIndent + "        ").AppendLine("var keyText = double.IsPositiveInfinity(pair.Key) ? \".inf\" : double.IsNegativeInfinity(pair.Key) ? \"-.inf\" : double.IsNaN(pair.Key) ? \".nan\" : pair.Key.ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture);");
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(keyText);");
            }
            else if (dictionaryKeyType.SpecialType == SpecialType.System_Single)
            {
                builder.Append(innerIndent + "        ").AppendLine("var keyText = float.IsPositiveInfinity(pair.Key) ? \".inf\" : float.IsNegativeInfinity(pair.Key) ? \"-.inf\" : float.IsNaN(pair.Key) ? \".nan\" : pair.Key.ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture);");
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(keyText);");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.DateTime")
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(pair.Key.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.DateTimeOffset")
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(pair.Key.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Guid")
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(pair.Key.ToString(\"D\"));");
            }
            else if (dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.TimeSpan")
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(pair.Key.ToString(\"c\", global::System.Globalization.CultureInfo.InvariantCulture));");
            }
            else
            {
                builder.Append(innerIndent + "        ").AppendLine("writer.WritePropertyName(((global::System.IFormattable)pair.Key).ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));");
            }

            EmitWriteKnownType(builder, dictionaryValueType, indexByType, "pair.Value", indent: innerIndent + "        ");
            builder.Append(innerIndent + "    ").AppendLine("}");
            builder.Append(innerIndent + "    ").AppendLine("writer.WriteEndMapping();");
            builder.Append(innerIndent).AppendLine("}");
            builder.Append(indent).AppendLine("}");
            return;
        }

        if (indexByType.TryGetValue(member.Type, out var typeIndex))
        {
            builder.Append(indent).Append("WriteValue").Append(typeIndex).Append("(writer, ").Append(valueExpression).AppendLine(");");
            return;
        }

        builder.Append(indent).AppendLine("throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this member type.\");");
    }

    private static void EmitWriteMemberValueWithCustomConverter(StringBuilder builder, MemberModel member, Dictionary<ITypeSymbol, int> indexByType, string valueExpression, string indent)
    {
        var memberTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (member.AttributeConverterTypeName is not null)
        {
            builder.Append(indent).Append("{").AppendLine();
            builder.Append(indent).Append("    var attributeConverter = (global::SharpYaml.Serialization.YamlConverter)new ")
                .Append(member.AttributeConverterTypeName).AppendLine("();");
            builder.Append(indent).AppendLine("    if (attributeConverter is global::SharpYaml.Serialization.YamlConverterFactory factory)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).Append("        attributeConverter = factory.CreateConverter(typeof(").Append(memberTypeName).AppendLine("), options);");
            builder.Append(indent).AppendLine("        if (attributeConverter is null || !attributeConverter.CanConvert(typeof(" + memberTypeName + ")))");
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).AppendLine("            throw new global::System.InvalidOperationException(\"Attribute converter factory returned an invalid converter.\");");
            builder.Append(indent).AppendLine("        }");
            builder.Append(indent).AppendLine("    }");
            builder.Append(indent).Append("    attributeConverter.Write(writer, ").Append(valueExpression).AppendLine(");");
            builder.Append(indent).AppendLine("}");
            return;
        }

        builder.Append(indent).Append("if (hasCustomConverters && writer.TryGetCustomConverter(typeof(").Append(memberTypeName).AppendLine("), out var memberCustomConverter) && memberCustomConverter is not null)");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent + "    ").Append("memberCustomConverter.Write(writer, ").Append(valueExpression).AppendLine(");");
        builder.Append(indent).AppendLine("}");
        builder.Append(indent).AppendLine("else");
        builder.Append(indent).AppendLine("{");
        EmitWriteMemberValue(builder, member, indexByType, valueExpression, indent + "    ");
        builder.Append(indent).AppendLine("}");
    }

    private static void EmitReadMemberValue(StringBuilder builder, MemberModel member, Dictionary<ITypeSymbol, int> indexByType)
    {
        if (member.Type is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.AppendLine("                if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                    }");
            builder.AppendLine("                    var text = reader.ScalarValue ?? string.Empty;");
            EmitReadScalar(builder, underlyingType, "value", textExpression: "text", indent: "                    ");
            builder.Append("                    ").Append(member.AssignExpression("value")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_String)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("reader.ScalarValue ?? string.Empty")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Boolean)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(reader, out var parsedBool))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidBooleanScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedBool")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Byte)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsedByte) || parsedByte > byte.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidByteScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(byte)parsedByte")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_SByte)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsedSByte) || parsedSByte is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidSByteScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(sbyte)parsedSByte")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Int16)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsedInt16) || parsedInt16 is < short.MinValue or > short.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt16Scalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(short)parsedInt16")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UInt16)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsedUInt16) || parsedUInt16 > ushort.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt16Scalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(ushort)parsedUInt16")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Int32)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(reader, out var parsed))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsed")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Int64)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsedInt64))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedInt64")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UInt32)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(reader, out var parsedUInt32))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt32Scalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedUInt32")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UInt64)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsedUInt64))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt64Scalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedUInt64")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_IntPtr)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var parsedIntPtr))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNIntScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(nint)parsedIntPtr")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader, out var parsedUIntPtr))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNUIntScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(nuint)parsedUIntPtr")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Double)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader, out var parsedDouble))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedDouble")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Single)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader, out var parsedSingle))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(float)parsedSingle")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Decimal)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(reader, out var parsedDecimal))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDecimalScalar(reader);");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedDecimal")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Char)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                var text = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("                if (text.Length != 1) { throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidCharScalar(reader, text); }");
            builder.Append("                ").Append(member.AssignExpression("text[0]")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type is INamedTypeSymbol systemType &&
            string.Equals(systemType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            if (string.Equals(systemType.Name, "DateTime", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.DateTime.TryParse(reader.ScalarValue, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDateTime))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateTimeScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedDateTime")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "DateTimeOffset", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.DateTimeOffset.TryParse(reader.ScalarValue, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDateTimeOffset))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateTimeOffsetScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedDateTimeOffset")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "Guid", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.Guid.TryParse(reader.ScalarValue, out var parsedGuid))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidGuidScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedGuid")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "TimeSpan", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.TimeSpan.TryParse(reader.ScalarValue, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedTimeSpan))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidTimeSpanScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedTimeSpan")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "DateOnly", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.DateOnly.TryParse(reader.ScalarValue, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedDateOnly))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateOnlyScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedDateOnly")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "TimeOnly", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.TimeOnly.TryParse(reader.ScalarValue, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedTimeOnly))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidTimeOnlyScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedTimeOnly")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "Half", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.Half.TryParse(reader.ScalarValue, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedHalf))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidHalfScalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedHalf")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "Int128", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.Int128.TryParse(reader.ScalarValue, global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedInt128))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt128Scalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedInt128")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }

            if (string.Equals(systemType.Name, "UInt128", StringComparison.Ordinal))
            {
                builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
                builder.AppendLine("                }");
                builder.AppendLine("                if (!global::System.UInt128.TryParse(reader.ScalarValue, global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedUInt128))");
                builder.AppendLine("                {");
                builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt128Scalar(reader);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(member.AssignExpression("parsedUInt128")).AppendLine(";");
                builder.AppendLine("                reader.Read();");
                return;
            }
        }

        if (member.Type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.AppendLine("                }");
            builder.AppendLine("                var text = reader.ScalarValue ?? string.Empty;");
            builder.Append("                if (global::System.Enum.TryParse<").Append(enumTypeName).AppendLine(">(text, ignoreCase: true, out var parsedEnum))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("parsedEnum")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var numeric))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression($"({enumTypeName})global::System.Enum.ToObject(typeof({enumTypeName}), numeric)")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidEnumScalar(reader, text);");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetArrayElementType(member.Type, out var arrayElementType))
        {
            var elementTypeName = arrayElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var memberTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TryReadAlias(out var memberAliasValue))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression($"({memberTypeName})memberAliasValue!")).AppendLine(";");
            builder.AppendLine("                }");
            builder.AppendLine("                else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
                builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
                builder.AppendLine("                    {");
                    builder.AppendLine("                        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedSequence(reader);");
                builder.AppendLine("                    }");
            builder.AppendLine("                    var memberAnchor = reader.Anchor;");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
            builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
            builder.AppendLine("                    {");
            EmitReadKnownType(builder, arrayElementType, indexByType, "element", indent: "                        ");
            builder.AppendLine("                        list.Add(element);");
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                    var array = list.ToArray();");
            builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, array); }");
            builder.Append("                    ").Append(member.AssignExpression("array")).AppendLine(";");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetSequenceElementType(member.Type, out var sequenceElementType, out var sequenceKind))
        {
            var elementTypeName = sequenceElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var memberTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TryReadAlias(out var memberAliasValue))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression($"({memberTypeName})memberAliasValue!")).AppendLine(";");
            builder.AppendLine("                }");
            builder.AppendLine("                else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedSequence(reader);");
            builder.AppendLine("                    }");
            builder.AppendLine("                    var memberAnchor = reader.Anchor;");
            builder.AppendLine("                    reader.Read();");

            switch (sequenceKind)
            {
                case SequenceKind.Set:
                    builder.Append("                    var set = new global::System.Collections.Generic.HashSet<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, set); }");
                    builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("                    {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "                        ");
                    builder.AppendLine("                        set.Add(element);");
                    builder.AppendLine("                    }");
                    builder.AppendLine("                    reader.Read();");
                    builder.Append("                    ").Append(member.AssignExpression("set")).AppendLine(";");
                    builder.AppendLine("                }");
                    return;

                case SequenceKind.ImmutableArray:
                    builder.Append("                    var builderArray = global::System.Collections.Immutable.ImmutableArray.CreateBuilder<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("                    {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "                        ");
                    builder.AppendLine("                        builderArray.Add(element);");
                    builder.AppendLine("                    }");
                    builder.AppendLine("                    reader.Read();");
                    builder.AppendLine("                    var immutableArray = builderArray.ToImmutable();");
                    builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, immutableArray); }");
                    builder.Append("                    ").Append(member.AssignExpression("immutableArray")).AppendLine(";");
                    builder.AppendLine("                }");
                    return;

                case SequenceKind.ImmutableList:
                    builder.Append("                    var builderList = global::System.Collections.Immutable.ImmutableList.CreateBuilder<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("                    {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "                        ");
                    builder.AppendLine("                        builderList.Add(element);");
                    builder.AppendLine("                    }");
                    builder.AppendLine("                    reader.Read();");
                    builder.AppendLine("                    var immutableList = builderList.ToImmutable();");
                    builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, immutableList); }");
                    builder.Append("                    ").Append(member.AssignExpression("immutableList")).AppendLine(";");
                    builder.AppendLine("                }");
                    return;

                case SequenceKind.ImmutableHashSet:
                    builder.Append("                    var builderSet = global::System.Collections.Immutable.ImmutableHashSet.CreateBuilder<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("                    {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "                        ");
                    builder.AppendLine("                        builderSet.Add(element);");
                    builder.AppendLine("                    }");
                    builder.AppendLine("                    reader.Read();");
                    builder.AppendLine("                    var immutableSet = builderSet.ToImmutable();");
                    builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, immutableSet); }");
                    builder.Append("                    ").Append(member.AssignExpression("immutableSet")).AppendLine(";");
                    builder.AppendLine("                }");
                    return;

                default:
                    builder.Append("                    var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
                    builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, list); }");
                    builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                    builder.AppendLine("                    {");
                    EmitReadKnownType(builder, sequenceElementType, indexByType, "element", indent: "                        ");
                    builder.AppendLine("                        list.Add(element);");
                    builder.AppendLine("                    }");
                    builder.AppendLine("                    reader.Read();");
                    builder.Append("                    ").Append(member.AssignExpression("list")).AppendLine(";");
                    builder.AppendLine("                }");
                    return;
            }
        }

        if (TryGetDictionaryTypes(member.Type, out var dictionaryKeyType, out var dictionaryValueType, out _))
        {
            var keyTypeName = dictionaryKeyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var valueTypeName = dictionaryValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var memberTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TryReadAlias(out var memberAliasValue))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression($"({memberTypeName})memberAliasValue!")).AppendLine(";");
            builder.AppendLine("                }");
            builder.AppendLine("                else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedMapping(reader);");
            builder.AppendLine("                    }");
            builder.AppendLine("                    var memberAnchor = reader.Anchor;");
            builder.AppendLine("                    reader.Read();");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.Append("                    var dictionary = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                    .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
                builder.AppendLine("                    var dictionaryMergeEnabled = options.Schema is global::SharpYaml.YamlSchemaKind.Core or global::SharpYaml.YamlSchemaKind.Extended;");
                builder.AppendLine("                    global::System.Collections.Generic.HashSet<string>? dictionaryExplicitKeys = dictionaryMergeEnabled");
                builder.AppendLine("                        ? new global::System.Collections.Generic.HashSet<string>(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal)");
                builder.AppendLine("                        : null;");
                builder.AppendLine("                    global::System.Collections.Generic.HashSet<string>? dictionarySeenKeys = dictionaryMergeEnabled");
                builder.AppendLine("                        ? new global::System.Collections.Generic.HashSet<string>(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal)");
                builder.AppendLine("                        : null;");
                builder.AppendLine("                    global::SharpYaml.Serialization.YamlConverter? dictionaryMergeConverter = null;");
            }
            else
            {
                builder.Append("                    var dictionary = new global::System.Collections.Generic.Dictionary<").Append(keyTypeName).Append(", ").Append(valueTypeName).AppendLine(">();");
            }

            builder.AppendLine("                    if (memberAnchor is not null) { reader.RegisterAnchor(memberAnchor, dictionary); }");
            builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                        {");
            builder.AppendLine("                            throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalarKey(reader);");
            builder.AppendLine("                        }");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("                        var entryKey = reader.ScalarValue ?? string.Empty;");
            }
            else
            {
                builder.Append("                        var entryKey = default(").Append(keyTypeName).AppendLine(");");
                builder.AppendLine("                        {");
                EmitReadScalarAssignment(builder, dictionaryKeyType, "entryKey", indent: "                            ");
                builder.AppendLine("                        }");
            }
            builder.AppendLine("                        reader.Read();");

            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("                        if (dictionaryMergeEnabled && global::System.String.Equals(entryKey, \"<<\", global::System.StringComparison.Ordinal))");
                builder.AppendLine("                        {");
                builder.AppendLine("                            if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader))");
                builder.AppendLine("                            {");
                builder.AppendLine("                                reader.Read();");
                builder.AppendLine("                            }");
                builder.AppendLine("                            else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartMapping || reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Alias)");
                builder.AppendLine("                            {");
                builder.AppendLine("                                dictionaryMergeConverter ??= reader.GetConverter(typeof(global::System.Collections.Generic.Dictionary<string, " + valueTypeName + ">));");
                builder.AppendLine("                                var merged = (global::System.Collections.Generic.Dictionary<string, " + valueTypeName + ">?)dictionaryMergeConverter.Read(reader, typeof(global::System.Collections.Generic.Dictionary<string, " + valueTypeName + ">));");
                builder.AppendLine("                                if (merged is not null)");
                builder.AppendLine("                                {");
                builder.AppendLine("                                    foreach (var pair in merged)");
                builder.AppendLine("                                    {");
                builder.AppendLine("                                        if (dictionaryExplicitKeys is not null && dictionaryExplicitKeys.Contains(pair.Key))");
                builder.AppendLine("                                        {");
                builder.AppendLine("                                            continue;");
                builder.AppendLine("                                        }");
                builder.AppendLine("                                        dictionary[pair.Key] = pair.Value;");
                builder.AppendLine("                                    }");
                builder.AppendLine("                                }");
                builder.AppendLine("                            }");
                builder.AppendLine("                            else if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
                builder.AppendLine("                            {");
                builder.AppendLine("                                reader.Read();");
                builder.AppendLine("                                while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
                builder.AppendLine("                                {");
                builder.AppendLine("                                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping && reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Alias)");
                builder.AppendLine("                                    {");
                builder.AppendLine("                                        throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge sequence entries must be mappings.\");");
                builder.AppendLine("                                    }");
                builder.AppendLine("                                    dictionaryMergeConverter ??= reader.GetConverter(typeof(global::System.Collections.Generic.Dictionary<string, " + valueTypeName + ">));");
                builder.AppendLine("                                    var merged = (global::System.Collections.Generic.Dictionary<string, " + valueTypeName + ">?)dictionaryMergeConverter.Read(reader, typeof(global::System.Collections.Generic.Dictionary<string, " + valueTypeName + ">));");
                builder.AppendLine("                                    if (merged is not null)");
                builder.AppendLine("                                    {");
                builder.AppendLine("                                        foreach (var pair in merged)");
                builder.AppendLine("                                        {");
                builder.AppendLine("                                            if (dictionaryExplicitKeys is not null && dictionaryExplicitKeys.Contains(pair.Key))");
                builder.AppendLine("                                            {");
                builder.AppendLine("                                                continue;");
                builder.AppendLine("                                            }");
                builder.AppendLine("                                            dictionary[pair.Key] = pair.Value;");
                builder.AppendLine("                                        }");
                builder.AppendLine("                                    }");
                builder.AppendLine("                                }");
                builder.AppendLine("                                reader.Read();");
                builder.AppendLine("                            }");
                builder.AppendLine("                            else");
                builder.AppendLine("                            {");
                builder.AppendLine("                                throw new global::SharpYaml.YamlException(reader.SourceName, reader.Start, reader.End, \"Merge key value must be a mapping or a sequence of mappings.\");");
                builder.AppendLine("                            }");
                builder.AppendLine("                            continue;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        dictionaryExplicitKeys?.Add(entryKey);");
            }

            EmitReadKnownType(builder, dictionaryValueType, indexByType, "value", indent: "                        ");
            if (dictionaryKeyType.SpecialType == SpecialType.System_String)
            {
                builder.AppendLine("                        if (dictionarySeenKeys is not null && !dictionarySeenKeys.Add(entryKey))");
                builder.AppendLine("                        {");
                builder.AppendLine("                            switch (options.DuplicateKeyHandling)");
                builder.AppendLine("                            {");
                builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.Error:");
                builder.AppendLine("                                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowDuplicateMappingKey(reader, entryKey);");
                builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.FirstWins:");
                builder.AppendLine("                                    break;");
                builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.LastWins:");
                builder.AppendLine("                                    dictionary[entryKey] = value;");
                builder.AppendLine("                                    break;");
                builder.AppendLine("                            }");
                builder.AppendLine("                        }");
                builder.AppendLine("                        else");
                builder.AppendLine("                        {");
                builder.AppendLine("                            dictionary[entryKey] = value;");
                builder.AppendLine("                        }");
            }
            else
            {
                builder.AppendLine("                        if (dictionary.ContainsKey(entryKey))");
                builder.AppendLine("                        {");
                builder.AppendLine("                            switch (options.DuplicateKeyHandling)");
                builder.AppendLine("                            {");
                builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.Error:");
                builder.AppendLine("                                    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowDuplicateMappingKey(reader, entryKey.ToString() ?? string.Empty);");
                builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.FirstWins:");
                builder.AppendLine("                                    break;");
                builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.LastWins:");
                builder.AppendLine("                                    dictionary[entryKey] = value;");
                builder.AppendLine("                                    break;");
                builder.AppendLine("                            }");
                builder.AppendLine("                        }");
                builder.AppendLine("                        else");
                builder.AppendLine("                        {");
                builder.AppendLine("                            dictionary[entryKey] = value;");
                builder.AppendLine("                        }");
            }
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    ").Append(member.AssignExpression("dictionary")).AppendLine(";");
            builder.AppendLine("                }");
            return;
        }

        if (indexByType.TryGetValue(member.Type, out var typeIndex))
        {
            builder.Append("                ").Append(member.AssignExpression($"ReadValue{typeIndex}(reader)")).AppendLine(";");
            return;
        }

        builder.AppendLine("                throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"The generated YAML serializer does not support this member type.\");");
    }

    private static void EmitReadMemberValueWithCustomConverter(StringBuilder builder, MemberModel member, Dictionary<ITypeSymbol, int> indexByType)
    {
        var memberTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (member.AttributeConverterTypeName is not null)
        {
            builder.AppendLine("                {");
            builder.Append("                    var attributeConverter = (global::SharpYaml.Serialization.YamlConverter)new ")
                .Append(member.AttributeConverterTypeName).AppendLine("();");
            builder.AppendLine("                    if (attributeConverter is global::SharpYaml.Serialization.YamlConverterFactory factory)");
            builder.AppendLine("                    {");
            builder.Append("                        attributeConverter = factory.CreateConverter(typeof(").Append(memberTypeName).AppendLine("), options);");
            builder.AppendLine("                        if (attributeConverter is null || !attributeConverter.CanConvert(typeof(" + memberTypeName + ")))");
            builder.AppendLine("                        {");
            builder.AppendLine("                            throw new global::System.InvalidOperationException(\"Attribute converter factory returned an invalid converter.\");");
            builder.AppendLine("                        }");
            builder.AppendLine("                    }");
            builder.Append("                    var untyped = attributeConverter.Read(reader, typeof(").Append(memberTypeName).AppendLine("));");
            builder.AppendLine("                    if (untyped is null)");
            builder.AppendLine("                    {");
            builder.Append("                        ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    }");
            builder.AppendLine("                    else");
            builder.AppendLine("                    {");
            builder.Append("                        ").Append(member.AssignExpression($"({memberTypeName})untyped")).AppendLine(";");
            builder.AppendLine("                    }");
            builder.AppendLine("                }");
            return;
        }

        builder.Append("                if (hasCustomConverters && reader.TryGetCustomConverter(typeof(").Append(memberTypeName).AppendLine("), out var memberCustomConverter) && memberCustomConverter is not null)");
        builder.AppendLine("                {");
        builder.Append("                    var untyped = memberCustomConverter.Read(reader, typeof(").Append(memberTypeName).AppendLine("));");
        builder.AppendLine("                    if (untyped is null)");
        builder.AppendLine("                    {");
            builder.Append("                        ").Append(member.AssignExpression("default")).AppendLine(";");
        builder.AppendLine("                    }");
        builder.AppendLine("                    else");
        builder.AppendLine("                    {");
        builder.Append("                        ").Append(member.AssignExpression($"({memberTypeName})untyped")).AppendLine(";");
        builder.AppendLine("                    }");
        builder.AppendLine("                }");
        builder.AppendLine("                else");
        builder.AppendLine("                {");
        EmitReadMemberValue(builder, member, indexByType);
        builder.AppendLine("                }");
    }

    private static bool TryEmitWriteScalar(StringBuilder builder, ITypeSymbol typeSymbol, string valueExpression, string indent)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.Append(indent).Append("writer.WriteString(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType is SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Decimal)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol is INamedTypeSymbol systemType &&
            string.Equals(systemType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            if (string.Equals(systemType.Name, "DateTime", StringComparison.Ordinal) ||
                string.Equals(systemType.Name, "DateTimeOffset", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
                return true;
            }

            if (string.Equals(systemType.Name, "Guid", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"D\"));");
                return true;
            }

            if (string.Equals(systemType.Name, "TimeSpan", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"c\", global::System.Globalization.CultureInfo.InvariantCulture));");
                return true;
            }

            if (string.Equals(systemType.Name, "DateOnly", StringComparison.Ordinal) ||
                string.Equals(systemType.Name, "TimeOnly", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture));");
                return true;
            }

            if (string.Equals(systemType.Name, "Half", StringComparison.Ordinal) ||
                string.Equals(systemType.Name, "Int128", StringComparison.Ordinal) ||
                string.Equals(systemType.Name, "UInt128", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
                return true;
            }
        }

        if (typeSymbol is INamedTypeSymbol named && named.TypeKind == TypeKind.Enum)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString());");
            return true;
        }

        return false;
    }

    private static void EmitReadScalar(StringBuilder builder, ITypeSymbol typeSymbol, string valueVarName, string textExpression = "reader.ScalarValue", string indent = "        ")
    {
        // Caller ensures TokenType == Scalar; this helper only parses and assigns into a local variable.
        var spanExpression = "reader";

        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.Append(indent).Append("var ").Append(valueVarName).Append(" = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(").Append(spanExpression).AppendLine(", out var parsedBool))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidBooleanScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedBool;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int32)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(").Append(spanExpression).AppendLine(", out var parsedInt32))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedInt32;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int64)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedInt64))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedInt64;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt32)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(").Append(spanExpression).AppendLine(", out var parsedUInt32))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt32Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedUInt32;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt64)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUInt64))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt64Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedUInt64;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Byte)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedByte) || parsedByte > byte.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidByteScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (byte)parsedByte;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_SByte)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedSByte) || parsedSByte is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidSByteScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (sbyte)parsedSByte;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int16)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedInt16) || parsedInt16 is < short.MinValue or > short.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt16Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (short)parsedInt16;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt16)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUInt16) || parsedUInt16 > ushort.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt16Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (ushort)parsedUInt16;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(").Append(spanExpression).AppendLine(", out var parsedDouble))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDouble;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(").Append(spanExpression).AppendLine(", out var parsedSingle))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (float)parsedSingle;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Decimal)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(").Append(spanExpression).AppendLine(", out var parsedDecimal))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDecimalScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDecimal;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedIntPtr))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNIntScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (nint)parsedIntPtr;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUIntPtr))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNUIntScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (nuint)parsedUIntPtr;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.Append(indent).Append("var textChar = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            builder.Append(indent).AppendLine("if (textChar.Length != 1) { throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidCharScalar(reader, textChar); }");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = textChar[0];");
            return;
        }

        if (typeSymbol is INamedTypeSymbol systemType &&
            string.Equals(systemType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            if (string.Equals(systemType.Name, "DateTime", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.DateTime.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDateTime))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateTimeScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDateTime;");
                return;
            }

            if (string.Equals(systemType.Name, "DateTimeOffset", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.DateTimeOffset.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDateTimeOffset))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateTimeOffsetScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDateTimeOffset;");
                return;
            }

            if (string.Equals(systemType.Name, "Guid", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.Guid.TryParse(").Append(textExpression).AppendLine(", out var parsedGuid))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidGuidScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedGuid;");
                return;
            }

            if (string.Equals(systemType.Name, "TimeSpan", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.TimeSpan.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, out var parsedTimeSpan))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidTimeSpanScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedTimeSpan;");
                return;
            }

            if (string.Equals(systemType.Name, "DateOnly", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.DateOnly.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedDateOnly))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateOnlyScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDateOnly;");
                return;
            }

            if (string.Equals(systemType.Name, "TimeOnly", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.TimeOnly.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedTimeOnly))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidTimeOnlyScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedTimeOnly;");
                return;
            }

            if (string.Equals(systemType.Name, "Half", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.Half.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedHalf))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidHalfScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedHalf;");
                return;
            }

            if (string.Equals(systemType.Name, "Int128", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.Int128.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedInt128))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt128Scalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedInt128;");
                return;
            }

            if (string.Equals(systemType.Name, "UInt128", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.UInt128.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedUInt128))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt128Scalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedUInt128;");
                return;
            }
        }

        if (typeSymbol is INamedTypeSymbol named && named.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append(indent).Append("var enumText = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            builder.Append(indent).Append(enumTypeName).Append(' ').Append(valueVarName).AppendLine(";");
            builder.Append(indent).Append("if (global::System.Enum.TryParse<").Append(enumTypeName).AppendLine(">(enumText, ignoreCase: true, out var parsedEnum))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("    ").Append(valueVarName).AppendLine(" = parsedEnum;");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var numeric))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("    ").Append(valueVarName).Append(" = (").Append(enumTypeName).AppendLine(")global::System.Enum.ToObject(typeof(" + enumTypeName + "), numeric);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidEnumScalar(reader, enumText);");
            builder.Append(indent).AppendLine("}");
            return;
        }

        builder.Append(indent).AppendLine("throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"The generated YAML serializer does not support this scalar type.\");");
    }

    private static void EmitReadScalarAssignment(StringBuilder builder, ITypeSymbol typeSymbol, string targetExpression, string textExpression = "reader.ScalarValue", string indent = "        ")
    {
        // Caller ensures TokenType == Scalar; this helper only parses and assigns into an existing local variable.
        var spanExpression = textExpression + ".AsSpan()";

        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.Append(indent).Append(targetExpression).Append(" = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(").Append(spanExpression).AppendLine(", out var parsedBool))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidBooleanScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedBool;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int32)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(").Append(spanExpression).AppendLine(", out var parsedInt32))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedInt32;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int64)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedInt64))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidIntegerScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedInt64;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt32)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(").Append(spanExpression).AppendLine(", out var parsedUInt32))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt32Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedUInt32;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt64)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUInt64))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt64Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedUInt64;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Byte)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedByte) || parsedByte > byte.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidByteScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (byte)parsedByte;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_SByte)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedSByte) || parsedSByte is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidSByteScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (sbyte)parsedSByte;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int16)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedInt16) || parsedInt16 is < short.MinValue or > short.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt16Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (short)parsedInt16;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt16)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUInt16) || parsedUInt16 > ushort.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt16Scalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (ushort)parsedUInt16;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(").Append(spanExpression).AppendLine(", out var parsedDouble))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedDouble;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(").Append(spanExpression).AppendLine(", out var parsedSingle))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidFloatScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (float)parsedSingle;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Decimal)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(").Append(spanExpression).AppendLine(", out var parsedDecimal))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDecimalScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = parsedDecimal;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedIntPtr))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNIntScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (nint)parsedIntPtr;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUIntPtr))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidNUIntScalar(reader);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append(targetExpression).AppendLine(" = (nuint)parsedUIntPtr;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.Append(indent).Append("var textChar = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            builder.Append(indent).AppendLine("if (textChar.Length != 1) { throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidCharScalar(reader, textChar); }");
            builder.Append(indent).Append(targetExpression).AppendLine(" = textChar[0];");
            return;
        }

        if (typeSymbol is INamedTypeSymbol systemType &&
            string.Equals(systemType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            if (string.Equals(systemType.Name, "DateTime", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.DateTime.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDateTime))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateTimeScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedDateTime;");
                return;
            }

            if (string.Equals(systemType.Name, "DateTimeOffset", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.DateTimeOffset.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDateTimeOffset))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateTimeOffsetScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedDateTimeOffset;");
                return;
            }

            if (string.Equals(systemType.Name, "Guid", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.Guid.TryParse(").Append(textExpression).AppendLine(", out var parsedGuid))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidGuidScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedGuid;");
                return;
            }

            if (string.Equals(systemType.Name, "TimeSpan", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.TimeSpan.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, out var parsedTimeSpan))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidTimeSpanScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedTimeSpan;");
                return;
            }

            if (string.Equals(systemType.Name, "DateOnly", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.DateOnly.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedDateOnly))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidDateOnlyScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedDateOnly;");
                return;
            }

            if (string.Equals(systemType.Name, "TimeOnly", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.TimeOnly.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedTimeOnly))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidTimeOnlyScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedTimeOnly;");
                return;
            }

            if (string.Equals(systemType.Name, "Half", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.Half.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedHalf))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidHalfScalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedHalf;");
                return;
            }

            if (string.Equals(systemType.Name, "Int128", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.Int128.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedInt128))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidInt128Scalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedInt128;");
                return;
            }

            if (string.Equals(systemType.Name, "UInt128", StringComparison.Ordinal))
            {
                builder.Append(indent).Append("if (!global::System.UInt128.TryParse(").Append(textExpression).AppendLine(", global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var parsedUInt128))");
                builder.Append(indent).AppendLine("{");
                builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidUInt128Scalar(reader);");
                builder.Append(indent).AppendLine("}");
                builder.Append(indent).Append(targetExpression).AppendLine(" = parsedUInt128;");
                return;
            }
        }

        if (typeSymbol is INamedTypeSymbol named && named.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append(indent).Append("var enumText = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            builder.Append(indent).Append("if (global::System.Enum.TryParse<").Append(enumTypeName).AppendLine(">(enumText, ignoreCase: true, out var parsedEnum))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("    ").Append(targetExpression).AppendLine(" = parsedEnum;");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader, out var numeric))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("    ").Append(targetExpression).Append(" = (").Append(enumTypeName).AppendLine(")global::System.Enum.ToObject(typeof(" + enumTypeName + "), numeric);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowInvalidEnumScalar(reader, enumText);");
            builder.Append(indent).AppendLine("}");
            return;
        }

        builder.Append(indent).AppendLine("throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"The generated YAML serializer does not support this scalar type.\");");
    }

    private static bool TryGetArrayElementType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        if (type is IArrayTypeSymbol arrayType && arrayType.Rank == 1)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        elementType = null!;
        return false;
    }

    private static bool TryGetListElementType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        if (type is INamedTypeSymbol named
            && named.IsGenericType
            && string.Equals(named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::System.Collections.Generic.List<T>", StringComparison.Ordinal)
            && named.TypeArguments.Length == 1)
        {
            elementType = named.TypeArguments[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private enum SequenceKind
    {
        List,
        Enumerable,
        Set,
        ImmutableArray,
        ImmutableList,
        ImmutableHashSet,
    }

    private static bool TryGetSequenceElementType(ITypeSymbol type, out ITypeSymbol elementType, out SequenceKind kind)
    {
        if (TryGetListElementType(type, out elementType))
        {
            kind = SequenceKind.List;
            return true;
        }

        if (type is INamedTypeSymbol named
            && named.IsGenericType
            && named.TypeArguments.Length == 1)
        {
            var constructed = named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (string.Equals(constructed, "global::System.Collections.Generic.IEnumerable<T>", StringComparison.Ordinal) ||
                string.Equals(constructed, "global::System.Collections.Generic.IReadOnlyList<T>", StringComparison.Ordinal) ||
                string.Equals(constructed, "global::System.Collections.Generic.IReadOnlyCollection<T>", StringComparison.Ordinal) ||
                string.Equals(constructed, "global::System.Collections.Generic.IList<T>", StringComparison.Ordinal) ||
                string.Equals(constructed, "global::System.Collections.Generic.ICollection<T>", StringComparison.Ordinal))
            {
                elementType = named.TypeArguments[0];
                kind = SequenceKind.Enumerable;
                return true;
            }

            if (string.Equals(constructed, "global::System.Collections.Generic.HashSet<T>", StringComparison.Ordinal) ||
                string.Equals(constructed, "global::System.Collections.Generic.ISet<T>", StringComparison.Ordinal))
            {
                elementType = named.TypeArguments[0];
                kind = SequenceKind.Set;
                return true;
            }

            if (string.Equals(constructed, "global::System.Collections.Immutable.ImmutableArray<T>", StringComparison.Ordinal))
            {
                elementType = named.TypeArguments[0];
                kind = SequenceKind.ImmutableArray;
                return true;
            }

            if (string.Equals(constructed, "global::System.Collections.Immutable.ImmutableList<T>", StringComparison.Ordinal))
            {
                elementType = named.TypeArguments[0];
                kind = SequenceKind.ImmutableList;
                return true;
            }

            if (string.Equals(constructed, "global::System.Collections.Immutable.ImmutableHashSet<T>", StringComparison.Ordinal))
            {
                elementType = named.TypeArguments[0];
                kind = SequenceKind.ImmutableHashSet;
                return true;
            }
        }

        elementType = null!;
        kind = default;
        return false;
    }

    private static bool TryGetDictionaryValueType(ITypeSymbol type, out ITypeSymbol valueType)
    {
        if (type is INamedTypeSymbol named
            && named.IsGenericType
            && string.Equals(named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::System.Collections.Generic.Dictionary<TKey, TValue>", StringComparison.Ordinal)
            && named.TypeArguments.Length == 2
            && named.TypeArguments[0].SpecialType == SpecialType.System_String)
        {
            valueType = named.TypeArguments[1];
            return true;
        }

        valueType = null!;
        return false;
    }

    private enum DictionaryKind
    {
        Dictionary,
        IDictionary,
        IReadOnlyDictionary,
    }

    private static bool TryGetDictionaryTypes(ITypeSymbol type, out ITypeSymbol keyType, out ITypeSymbol valueType, out DictionaryKind kind)
    {
        if (type is INamedTypeSymbol named
            && named.IsGenericType
            && named.TypeArguments.Length == 2)
        {
            var constructed = named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (string.Equals(constructed, "global::System.Collections.Generic.Dictionary<TKey, TValue>", StringComparison.Ordinal))
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                kind = DictionaryKind.Dictionary;
                return true;
            }

            if (string.Equals(constructed, "global::System.Collections.Generic.IDictionary<TKey, TValue>", StringComparison.Ordinal))
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                kind = DictionaryKind.IDictionary;
                return true;
            }

            if (string.Equals(constructed, "global::System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>", StringComparison.Ordinal))
            {
                keyType = named.TypeArguments[0];
                valueType = named.TypeArguments[1];
                kind = DictionaryKind.IReadOnlyDictionary;
                return true;
            }
        }

        keyType = null!;
        valueType = null!;
        kind = default;
        return false;
    }

    private static bool IsSupportedDictionaryKeyType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        if (type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        return IsKnownScalar(type);
    }

    private static void EmitWriteKnownType(StringBuilder builder, ITypeSymbol typeSymbol, Dictionary<ITypeSymbol, int> indexByType, string valueExpression, string indent)
    {
        var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        builder.Append(indent).Append("if (hasCustomConverters && writer.TryGetCustomConverter(typeof(").Append(typeName).AppendLine("), out var elementCustomConverter) && elementCustomConverter is not null)");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).Append("    elementCustomConverter.Write(writer, ").Append(valueExpression).AppendLine(");");
        builder.Append(indent).AppendLine("}");
        builder.Append(indent).AppendLine("else");
        builder.Append(indent).AppendLine("{");

        var innerIndent = indent + "    ";
        if (TryEmitWriteScalar(builder, typeSymbol, valueExpression, innerIndent))
        {
            builder.Append(indent).AppendLine("}");
            return;
        }

        if (indexByType.TryGetValue(typeSymbol, out var typeIndex))
        {
            builder.Append(innerIndent).Append("WriteValue").Append(typeIndex).Append("(writer, ").Append(valueExpression).AppendLine(");");
            builder.Append(indent).AppendLine("}");
            return;
        }

        builder.Append(innerIndent).AppendLine("throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this element type.\");");
        builder.Append(indent).AppendLine("}");
    }

    private static void EmitReadKnownType(StringBuilder builder, ITypeSymbol typeSymbol, Dictionary<ITypeSymbol, int> indexByType, string valueVarName, string indent)
    {
        var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        builder.Append(indent).Append("var ").Append(valueVarName).Append(" = default(").Append(typeName).AppendLine(");");
        builder.Append(indent).Append("if (hasCustomConverters && reader.TryGetCustomConverter(typeof(").Append(typeName).AppendLine("), out var elementCustomConverter) && elementCustomConverter is not null)");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).Append("    var untyped = elementCustomConverter.Read(reader, typeof(").Append(typeName).AppendLine("));");
        builder.Append(indent).AppendLine("    if (untyped is not null)");
        builder.Append(indent).AppendLine("    {");
        builder.Append(indent).Append("        ").Append(valueVarName).Append(" = (").Append(typeName).AppendLine(")untyped;");
        builder.Append(indent).AppendLine("    }");
        builder.Append(indent).AppendLine("}");
        builder.Append(indent).AppendLine("else");
        builder.Append(indent).AppendLine("{");

        var innerIndent = indent + "    ";
        if (IsKnownScalar(typeSymbol))
        {
            builder.Append(innerIndent).AppendLine("if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.Append(innerIndent).AppendLine("{");
            builder.Append(innerIndent).AppendLine("    throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowExpectedScalar(reader);");
            builder.Append(innerIndent).AppendLine("}");
            EmitReadScalarAssignment(builder, typeSymbol, valueVarName, indent: innerIndent);
            builder.Append(innerIndent).AppendLine("reader.Read();");
            builder.Append(indent).AppendLine("}");
            return;
        }

        if (indexByType.TryGetValue(typeSymbol, out var typeIndex))
        {
            builder.Append(innerIndent).Append(valueVarName).Append(" = ReadValue").Append(typeIndex).AppendLine("(reader);");
            builder.Append(indent).AppendLine("}");
            return;
        }

        builder.Append(innerIndent).AppendLine("throw global::SharpYaml.Serialization.YamlThrowHelper.ThrowNotSupported(reader, \"The generated YAML serializer does not support this element type.\");");
        builder.Append(indent).AppendLine("}");
    }

    private static bool IsKnownScalar(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return IsKnownScalar(nullableType.TypeArguments[0]);
        }

        if (type is INamedTypeSymbol named && named.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type is INamedTypeSymbol systemType &&
            string.Equals(systemType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal))
        {
             // Common non-primitive scalars supported out of the box (mirrors STJ built-ins).
             if (string.Equals(systemType.Name, "DateTime", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "DateTimeOffset", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "Guid", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "TimeSpan", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "DateOnly", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "TimeOnly", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "Half", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "Int128", StringComparison.Ordinal) ||
                 string.Equals(systemType.Name, "UInt128", StringComparison.Ordinal))
             {
                 return true;
             }
         }

        return type.SpecialType is SpecialType.System_String
            or SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_IntPtr
            or SpecialType.System_UIntPtr
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal
            or SpecialType.System_Char;
    }

    private static bool ImplementsAnyYamlLifecycleCallback(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            var name = iface.ToDisplayString();
            if (string.Equals(name, "SharpYaml.Serialization.IYamlOnDeserializing", StringComparison.Ordinal) ||
                string.Equals(name, "SharpYaml.Serialization.IYamlOnDeserialized", StringComparison.Ordinal) ||
                string.Equals(name, "SharpYaml.Serialization.IYamlOnSerializing", StringComparison.Ordinal) ||
                string.Equals(name, "SharpYaml.Serialization.IYamlOnSerialized", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySelectDeserializationConstructor(INamedTypeSymbol type, out IMethodSymbol? selectedConstructor, out string? notSupportedMessage)
    {
        selectedConstructor = null;
        notSupportedMessage = null;

        IMethodSymbol? attributed = null;
        foreach (var ctor in type.InstanceConstructors)
        {
            if (HasAttribute(ctor, "SharpYaml.Serialization.YamlConstructorAttribute") ||
                HasAttribute(ctor, "System.Text.Json.Serialization.JsonConstructorAttribute"))
            {
                if (attributed is not null)
                {
                    notSupportedMessage = $"Type '{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' defines multiple constructors annotated with [YamlConstructor] or [JsonConstructor].";
                    return false;
                }

                attributed = ctor;
            }
        }

        if (attributed is not null)
        {
            selectedConstructor = attributed;
            return true;
        }

        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0)
            {
                selectedConstructor = ctor;
                return true;
            }
        }

        var publicCtors = type.InstanceConstructors.Where(static ctor => ctor.DeclaredAccessibility == Accessibility.Public).ToArray();
        if (publicCtors.Length == 1)
        {
            selectedConstructor = publicCtors[0];
            return true;
        }

        if (publicCtors.Length == 0)
        {
            notSupportedMessage = $"Type '{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' does not have a public constructor. Use [YamlConstructor] or [JsonConstructor] to opt into a non-public constructor.";
            return false;
        }

        notSupportedMessage = $"Type '{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' defines multiple public constructors. Use [YamlConstructor] or [JsonConstructor] to select the constructor to use for deserialization.";
        return false;
    }

    private static bool IsConstructorAccessibleFromGeneratedContext(IMethodSymbol constructor)
        => constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    private static string GetOptionalParameterDefaultValueExpression(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            throw new InvalidOperationException("Parameter does not define an explicit default value.");
        }

        var value = parameter.ExplicitDefaultValue;
        if (value is null)
        {
            return "default";
        }

        if (value is string str)
        {
            return ToLiteral(str);
        }

        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }

        if (value is char ch)
        {
            return "'" + (ch == '\'' ? "\\'" : ch.ToString()) + "'";
        }

        if (parameter.Type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var numeric = Convert.ToInt64(value);
            return $"({enumTypeName}){numeric}";
        }

        // Numeric primitives and other literals.
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default";
    }

    private sealed class MemberModel
    {
        public MemberModel(
            ISymbol symbol,
            ITypeSymbol type,
            string serializedNameExpressionForRead,
            string serializedNameExpressionForWrite,
            string accessExpression,
            Func<string, string> assignExpression,
            string ignoreConditionExpression,
            string? attributeConverterTypeName,
            bool isRequired,
            bool isInitOnly)
        {
            Symbol = symbol;
            Type = type;
            SerializedNameExpressionForRead = serializedNameExpressionForRead;
            SerializedNameExpressionForWrite = serializedNameExpressionForWrite;
            AccessExpression = accessExpression;
            AssignExpression = assignExpression;
            IgnoreConditionExpression = ignoreConditionExpression;
            AttributeConverterTypeName = attributeConverterTypeName;
            IsRequired = isRequired;
            IsInitOnly = isInitOnly;
        }

        public ISymbol Symbol { get; }
        public ITypeSymbol Type { get; }
        public string SerializedNameExpressionForRead { get; }
        public string SerializedNameExpressionForWrite { get; }
        public string AccessExpression { get; }
        public Func<string, string> AssignExpression { get; }
        public string IgnoreConditionExpression { get; }
        public string? AttributeConverterTypeName { get; }
        public bool IsRequired { get; }
        public bool IsInitOnly { get; }
    }

    private enum ExtensionDataKind
    {
        Dictionary,
        Mapping,
    }

    private sealed class ExtensionDataMemberModel
    {
        public ExtensionDataMemberModel(
            ISymbol symbol,
            ITypeSymbol type,
            ExtensionDataKind kind,
            ITypeSymbol? dictionaryValueType,
            string accessExpression,
            Func<string, string>? assignExpression,
            bool canAssign,
            bool isInitOnly)
        {
            Symbol = symbol;
            Type = type;
            Kind = kind;
            DictionaryValueType = dictionaryValueType;
            AccessExpression = accessExpression;
            AssignExpression = assignExpression;
            CanAssign = canAssign;
            IsInitOnly = isInitOnly;
        }

        public ISymbol Symbol { get; }
        public ITypeSymbol Type { get; }
        public ExtensionDataKind Kind { get; }
        public ITypeSymbol? DictionaryValueType { get; }
        public string AccessExpression { get; }
        public Func<string, string>? AssignExpression { get; }
        public bool CanAssign { get; }
        public bool IsInitOnly { get; }
    }

    private static MemberModel CreateMemberModel(ISymbol member, JsonNamingPolicy? propertyNamingPolicy)
    {
        var (nameForRead, nameForWrite) = GetSerializedMemberNameExpressions(member, propertyNamingPolicy);
        var type = GetMemberType(member) ?? throw new InvalidOperationException("Member type could not be determined.");
        var accessExpression = member is IPropertySymbol prop ? "value." + prop.Name : "value." + member.Name;
        Func<string, string> assign = member is IPropertySymbol propAssign
            ? rhs => "instance." + propAssign.Name + " = " + rhs
            : rhs => "instance." + member.Name + " = " + rhs;
        var ignoreConditionExpression = GetIgnoreConditionExpression(member);
        var converterTypeName = GetYamlConverterAttributeTypeName(member);
        var isRequired = HasAttribute(member, "SharpYaml.Serialization.YamlRequiredAttribute") || HasAttribute(member, "System.Text.Json.Serialization.JsonRequiredAttribute");
        var isInitOnly = member is IPropertySymbol property && IsInitOnlyProperty(property);
        return new MemberModel(member, type, nameForRead, nameForWrite, accessExpression, assign, ignoreConditionExpression, converterTypeName, isRequired, isInitOnly);
    }

    private static (string ForRead, string ForWrite) GetSerializedMemberNameExpressions(ISymbol member, JsonNamingPolicy? propertyNamingPolicy)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (string.Equals(attribute.AttributeClass.ToDisplayString(), "SharpYaml.Serialization.YamlPropertyNameAttribute", StringComparison.Ordinal))
            {
                if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string yamlName)
                {
                    var nameLiteral = ToLiteral(yamlName);
                    return (nameLiteral, nameLiteral);
                }
            }
        }

        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (string.Equals(attribute.AttributeClass.ToDisplayString(), "System.Text.Json.Serialization.JsonPropertyNameAttribute", StringComparison.Ordinal))
            {
                if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string jsonName)
                {
                    var nameLiteral = ToLiteral(jsonName);
                    return (nameLiteral, nameLiteral);
                }
            }
        }

        var name = ApplyNamingPolicy(member.Name, propertyNamingPolicy);
        var resolvedLiteral = ToLiteral(name);
        return (resolvedLiteral, resolvedLiteral);
    }

    private static string ApplyNamingPolicy(string name, JsonNamingPolicy? policy)
    {
        if (policy is null || string.IsNullOrEmpty(name))
        {
            return name;
        }

        return policy.ConvertName(name);
    }

    private static JsonNamingPolicy? ResolveJsonNamingPolicy(string? policyName)
    {
        if (string.IsNullOrEmpty(policyName) || string.Equals(policyName, "Unspecified", StringComparison.Ordinal))
        {
            return null;
        }

        var property = typeof(JsonNamingPolicy).GetProperty(policyName, BindingFlags.Public | BindingFlags.Static);
        if (property is null)
        {
            return null;
        }

        return property.GetValue(null) as JsonNamingPolicy;
    }

    private static string GetIgnoreConditionExpression(ISymbol member)
    {
        // Member-level ignore overrides options default. YAML ignore is treated as Always (handled by member filtering).
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (!string.Equals(attribute.AttributeClass.ToDisplayString(), "System.Text.Json.Serialization.JsonIgnoreAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var pair in attribute.NamedArguments)
            {
                if (!string.Equals(pair.Key, "Condition", StringComparison.Ordinal))
                {
                    continue;
                }

                if (pair.Value.Value is int conditionValue)
                {
                    return conditionValue switch
                    {
                        1 => "global::SharpYaml.YamlIgnoreCondition.WhenWritingNull",
                        2 => "global::SharpYaml.YamlIgnoreCondition.WhenWritingDefault",
                        _ => "global::SharpYaml.YamlIgnoreCondition.Never",
                    };
                }
            }
        }

        return "options.DefaultIgnoreCondition";
    }

    private static string? GetYamlConverterAttributeTypeName(ISymbol member)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (!string.Equals(attribute.AttributeClass.ToDisplayString(), "SharpYaml.Serialization.YamlConverterAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];
            if (argument.Kind != TypedConstantKind.Type || argument.Value is not ITypeSymbol converterType)
            {
                continue;
            }

            return converterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        return null;
    }

    private static ImmutableArray<ISymbol> GetSerializableMembers(INamedTypeSymbol type)
    {
        // Arrays/collections/dictionaries are handled by dedicated generated code paths, not as object graphs.
        if (TryGetArrayElementType(type, out _) ||
            TryGetSequenceElementType(type, out _, out _) ||
            TryGetDictionaryTypes(type, out _, out _, out _))
        {
            return ImmutableArray<ISymbol>.Empty;
        }

        // Include base members for parity with reflection/STJ behavior, but prefer the most-derived
        // member when a derived type hides/overrides a base member with the same CLR name.
        var members = new List<ISymbol>();
        var indexByClrName = new Dictionary<string, int>(StringComparer.Ordinal);

        var hierarchy = new Stack<INamedTypeSymbol>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.SpecialType == SpecialType.System_Object)
            {
                break;
            }

            hierarchy.Push(current);
        }

        while (hierarchy.Count != 0)
        {
            var current = hierarchy.Pop();
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol property)
                {
                    if (property.IsIndexer)
                    {
                        continue;
                    }

                    var hasIncludeAttr = HasAttribute(property, "SharpYaml.Serialization.YamlIncludeAttribute") || HasAttribute(property, "System.Text.Json.Serialization.JsonIncludeAttribute");
                    var canRead = property.GetMethod is { DeclaredAccessibility: Accessibility.Public } || hasIncludeAttr;
                    if (!canRead)
                    {
                        continue;
                    }

                    if (HasAttribute(property, "SharpYaml.Serialization.YamlIgnoreAttribute") || HasJsonIgnoreAlways(property))
                    {
                        continue;
                    }

                    if (indexByClrName.TryGetValue(property.Name, out var existingIndex))
                    {
                        members[existingIndex] = property;
                    }
                    else
                    {
                        indexByClrName.Add(property.Name, members.Count);
                        members.Add(property);
                    }

                    continue;
                }

                if (member is IFieldSymbol field)
                {
                    if (!HasAttribute(field, "SharpYaml.Serialization.YamlIncludeAttribute") && !HasAttribute(field, "System.Text.Json.Serialization.JsonIncludeAttribute"))
                    {
                        continue;
                    }

                    if (HasAttribute(field, "SharpYaml.Serialization.YamlIgnoreAttribute") || HasJsonIgnoreAlways(field))
                    {
                        continue;
                    }

                    if (indexByClrName.TryGetValue(field.Name, out var existingIndex))
                    {
                        members[existingIndex] = field;
                    }
                    else
                    {
                        indexByClrName.Add(field.Name, members.Count);
                        members.Add(field);
                    }
                }
            }
        }

        return members.ToImmutableArray();
    }

    private static ImmutableArray<ISymbol> GetExtensionDataMembers(INamedTypeSymbol type)
    {
        var matches = ImmutableArray.CreateBuilder<ISymbol>();

        var hierarchy = new Stack<INamedTypeSymbol>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.SpecialType == SpecialType.System_Object)
            {
                break;
            }

            hierarchy.Push(current);
        }

        while (hierarchy.Count != 0)
        {
            var current = hierarchy.Pop();
            foreach (var member in current.GetMembers())
            {
                if (member is not IPropertySymbol && member is not IFieldSymbol)
                {
                    continue;
                }

                if (HasAttribute(member, "SharpYaml.Serialization.YamlExtensionDataAttribute") ||
                    HasAttribute(member, "System.Text.Json.Serialization.JsonExtensionDataAttribute"))
                {
                    matches.Add(member);
                }
            }
        }

        return matches.ToImmutable();
    }

    private static bool IsSupportedExtensionDataMemberType(ITypeSymbol type)
    {
        if (string.Equals(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::SharpYaml.Model.YamlMapping", StringComparison.Ordinal))
        {
            return true;
        }

        if (TryGetDictionaryValueType(type, out var valueType))
        {
            if (valueType.SpecialType == SpecialType.System_Object)
            {
                return true;
            }

            if (IsYamlNodeType(valueType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsYamlNodeType(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = (current as INamedTypeSymbol)?.BaseType)
        {
            if (string.Equals(current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::SharpYaml.Model.YamlNode", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ExtensionDataMemberModel? TryCreateExtensionDataMemberModel(INamedTypeSymbol type)
    {
        var extensionDataMembers = GetExtensionDataMembers(type);
        if (extensionDataMembers.Length != 1)
        {
            return null;
        }

        var symbol = extensionDataMembers[0];
        var memberType = GetMemberType(symbol);
        if (memberType is null || !IsSupportedExtensionDataMemberType(memberType))
        {
            return null;
        }

        ExtensionDataKind kind;
        ITypeSymbol? dictionaryValueType = null;
        if (string.Equals(memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::SharpYaml.Model.YamlMapping", StringComparison.Ordinal))
        {
            kind = ExtensionDataKind.Mapping;
        }
        else
        {
            kind = ExtensionDataKind.Dictionary;
            _ = TryGetDictionaryValueType(memberType, out dictionaryValueType);
        }

        var accessExpression = "instance." + symbol.Name;
        Func<string, string>? assignExpression = null;
        var canAssign = false;
        var isInitOnly = false;

        if (symbol is IPropertySymbol property)
        {
            isInitOnly = IsInitOnlyProperty(property);
            if (property.SetMethod is not null)
            {
                canAssign = !isInitOnly;
                if (canAssign)
                {
                    assignExpression = rhs => "instance." + property.Name + " = " + rhs;
                }
            }
        }
        else if (symbol is IFieldSymbol field)
        {
            canAssign = !field.IsConst && !field.IsReadOnly;
            if (canAssign)
            {
                assignExpression = rhs => "instance." + field.Name + " = " + rhs;
            }
        }

        return new ExtensionDataMemberModel(symbol, memberType, kind, dictionaryValueType, accessExpression, assignExpression, canAssign, isInitOnly);
    }

    private static bool IsWritableMember(ISymbol member)
    {
        if (member is IPropertySymbol property)
        {
            if (property.SetMethod is null)
            {
                return false;
            }

            var hasIncludeAttr = HasAttribute(property, "SharpYaml.Serialization.YamlIncludeAttribute") || HasAttribute(property, "System.Text.Json.Serialization.JsonIncludeAttribute");
            return property.SetMethod.DeclaredAccessibility == Accessibility.Public || hasIncludeAttr;
        }

        if (member is IFieldSymbol field)
        {
            if (field.IsConst || field.IsReadOnly)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsInitOnlyProperty(IPropertySymbol property)
    {
        if (property.SetMethod is not { } setMethod)
        {
            return false;
        }

        foreach (var modifier in setMethod.ReturnTypeCustomModifiers)
        {
            if (!modifier.IsOptional &&
                string.Equals(modifier.Modifier.ToDisplayString(), "System.Runtime.CompilerServices.IsExternalInit", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? GetMemberType(ISymbol member)
        => member switch
        {
            IPropertySymbol p => p.Type,
            IFieldSymbol f => f.Type,
            _ => null,
        };

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (string.Equals(attribute.AttributeClass.ToDisplayString(), metadataName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasJsonIgnoreAlways(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            if (!string.Equals(attribute.AttributeClass.ToDisplayString(), "System.Text.Json.Serialization.JsonIgnoreAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var pair in attribute.NamedArguments)
            {
                if (!string.Equals(pair.Key, "Condition", StringComparison.Ordinal))
                {
                    continue;
                }

                if (pair.Value.Value is int conditionValue && conditionValue == 0 /* JsonIgnoreCondition.Always */)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool DerivesFromYamlSerializerContext(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), "SharpYaml.Serialization.YamlSerializerContext", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateSerializableTypeModel(AttributeData attribute, out SerializableTypeModel model)
    {
        model = null!;

        if (!IsYamlSerializableAttribute(attribute) && !IsJsonSerializableAttribute(attribute))
        {
            return false;
        }

        if (attribute.ConstructorArguments.Length != 1)
        {
            return false;
        }

        var argument = attribute.ConstructorArguments[0];
        if (argument.Kind != TypedConstantKind.Type || argument.Value is not ITypeSymbol typeSymbol)
        {
            return false;
        }

        model = new SerializableTypeModel(typeSymbol, GetTypeInfoPropertyNameOverride(attribute));
        return true;
    }

    private static bool IsYamlSerializableAttribute(AttributeData attribute)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), "SharpYaml.Serialization.YamlSerializableAttribute", StringComparison.Ordinal);

    private static bool IsJsonSerializableAttribute(AttributeData attribute)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Text.Json.Serialization.JsonSerializableAttribute", StringComparison.Ordinal);

    private static bool IsJsonSourceGenerationOptionsAttribute(AttributeData attribute)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute", StringComparison.Ordinal);

    private static bool IsYamlSourceGenerationOptionsAttribute(AttributeData attribute)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), "SharpYaml.Serialization.YamlSourceGenerationOptionsAttribute", StringComparison.Ordinal);

    private static string? GetTypeInfoPropertyNameOverride(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (string.Equals(namedArgument.Key, "TypeInfoPropertyName", StringComparison.Ordinal) &&
                namedArgument.Value.Value is string typeInfoPropertyName)
            {
                return typeInfoPropertyName;
            }
        }

        return null;
    }

    private static IEnumerable<ITypeSymbol> GetPolymorphicDerivedTypes(INamedTypeSymbol baseType)
    {
        if (TryGetPolymorphismInfo(baseType, out var info))
        {
            for (var i = 0; i < info.DerivedTypes.Length; i++)
            {
                yield return info.DerivedTypes[i].DerivedType;
            }
        }
    }

    private readonly struct DerivedTypeInfoModel
    {
        public DerivedTypeInfoModel(ITypeSymbol derivedType, string? discriminator, string? tag)
        {
            DerivedType = derivedType;
            Discriminator = discriminator;
            Tag = tag;
        }

        public ITypeSymbol DerivedType { get; }

        public string? Discriminator { get; }

        public string? Tag { get; }
    }

    private sealed class PolymorphismInfoModel
    {
        public PolymorphismInfoModel(
            string? discriminatorPropertyNameOverride,
            int? discriminatorStyleOverrideValue,
            int? unknownDerivedTypeHandlingOverrideValue,
            ImmutableArray<DerivedTypeInfoModel> derivedTypes,
            ITypeSymbol? defaultDerivedType)
        {
            DiscriminatorPropertyNameOverride = discriminatorPropertyNameOverride;
            DiscriminatorStyleOverrideValue = discriminatorStyleOverrideValue;
            UnknownDerivedTypeHandlingOverrideValue = unknownDerivedTypeHandlingOverrideValue;
            DerivedTypes = derivedTypes;
            DefaultDerivedType = defaultDerivedType;
        }

        public string? DiscriminatorPropertyNameOverride { get; }

        public int? DiscriminatorStyleOverrideValue { get; }

        public int? UnknownDerivedTypeHandlingOverrideValue { get; }

        public ImmutableArray<DerivedTypeInfoModel> DerivedTypes { get; }

        public ITypeSymbol? DefaultDerivedType { get; }
    }

    private static bool TryGetPolymorphismInfo(INamedTypeSymbol baseType, out PolymorphismInfoModel info)
    {
        string? discriminatorPropertyNameOverride = null;
        int? discriminatorStyleOverrideValue = null;
        int? unknownOverrideValue = null;
        int? yamlUnknownOverrideValue = null;

        var derivedTypes = ImmutableArray.CreateBuilder<DerivedTypeInfoModel>();
        var seenDerived = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var attribute in baseType.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            if (string.Equals(attributeName, "SharpYaml.Serialization.YamlPolymorphicAttribute", StringComparison.Ordinal))
            {
                foreach (var pair in attribute.NamedArguments)
                {
                    if (string.Equals(pair.Key, "TypeDiscriminatorPropertyName", StringComparison.Ordinal) && pair.Value.Value is string name)
                    {
                        discriminatorPropertyNameOverride = name;
                    }
                    else if (string.Equals(pair.Key, "DiscriminatorStyle", StringComparison.Ordinal) && pair.Value.Value is int styleValue)
                    {
                        discriminatorStyleOverrideValue = styleValue;
                    }
                    else if (string.Equals(pair.Key, "UnknownDerivedTypeHandling", StringComparison.Ordinal) && pair.Value.Value is int unknownValue)
                    {
                        yamlUnknownOverrideValue = unknownValue;
                    }
                }
            }
            else if (string.Equals(attributeName, "System.Text.Json.Serialization.JsonPolymorphicAttribute", StringComparison.Ordinal))
            {
                foreach (var pair in attribute.NamedArguments)
                {
                    if (string.Equals(pair.Key, "TypeDiscriminatorPropertyName", StringComparison.Ordinal) && pair.Value.Value is string name)
                    {
                        discriminatorPropertyNameOverride = name;
                    }
                    else if (string.Equals(pair.Key, "UnknownDerivedTypeHandling", StringComparison.Ordinal) && pair.Value.Value is int unknownValue)
                    {
                        // JsonUnknownDerivedTypeHandling.FallBackToBaseType is treated as fallback; all other values fail.
                        unknownOverrideValue = unknownValue == 1 ? 1 : 0;
                    }
                }
            }
        }

        // YamlPolymorphicAttribute.UnknownDerivedTypeHandling takes priority over JsonPolymorphicAttribute
        // -1 is YamlUnknownDerivedTypeHandling.Unspecified
        if (yamlUnknownOverrideValue is not null && yamlUnknownOverrideValue.Value != -1)
        {
            unknownOverrideValue = yamlUnknownOverrideValue;
        }

        // YamlDerivedTypeAttribute(Type derivedType) or YamlDerivedTypeAttribute(Type derivedType, string|int discriminator) { string? Tag }
        ITypeSymbol? defaultDerivedType = null;
        foreach (var attribute in baseType.GetAttributes())
        {
            if (!string.Equals(attribute.AttributeClass?.ToDisplayString(), "SharpYaml.Serialization.YamlDerivedTypeAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length < 1)
            {
                continue;
            }

            var derivedArg = attribute.ConstructorArguments[0];
            if (derivedArg.Kind != TypedConstantKind.Type || derivedArg.Value is not ITypeSymbol derivedType)
            {
                continue;
            }

            string? discriminator = null;
            if (attribute.ConstructorArguments.Length >= 2)
            {
                discriminator = attribute.ConstructorArguments[1].Value switch
                {
                    string s => s,
                    int i => i.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
                    _ => null,
                };
            }

            string? tag = null;
            foreach (var pair in attribute.NamedArguments)
            {
                if (string.Equals(pair.Key, "Tag", StringComparison.Ordinal) && pair.Value.Value is string tagValue)
                {
                    tag = tagValue;
                }
            }

            if (seenDerived.Add(derivedType))
            {
                if (discriminator is null)
                {
                    defaultDerivedType = derivedType;
                }

                derivedTypes.Add(new DerivedTypeInfoModel(derivedType, discriminator, tag));
            }
        }

        // JsonDerivedTypeAttribute(Type derivedType) or JsonDerivedTypeAttribute(Type derivedType, string|int discriminator)
        foreach (var attribute in baseType.GetAttributes())
        {
            if (!string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Text.Json.Serialization.JsonDerivedTypeAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length < 1)
            {
                continue;
            }

            var derivedArg = attribute.ConstructorArguments[0];
            if (derivedArg.Kind != TypedConstantKind.Type || derivedArg.Value is not ITypeSymbol derivedType)
            {
                continue;
            }

            string? discriminator = null;
            if (attribute.ConstructorArguments.Length >= 2)
            {
                discriminator = attribute.ConstructorArguments[1].Value switch
                {
                    string s => s,
                    int i => i.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
                    _ => null,
                };
            }

            if (seenDerived.Add(derivedType))
            {
                if (discriminator is null)
                {
                    defaultDerivedType ??= derivedType;
                }

                derivedTypes.Add(new DerivedTypeInfoModel(derivedType, discriminator, tag: null));
            }
        }

        if (derivedTypes.Count == 0 && discriminatorPropertyNameOverride is null && discriminatorStyleOverrideValue is null)
        {
            info = null!;
            return false;
        }

        info = new PolymorphismInfoModel(
            discriminatorPropertyNameOverride,
            discriminatorStyleOverrideValue,
            unknownOverrideValue,
            derivedTypes.ToImmutable(),
            defaultDerivedType);
        return true;
    }

    private static ImmutableArray<string> CreateTypeInfoPropertyNames(ContextModel model, ImmutableArray<ITypeSymbol> types)
    {
        var names = ImmutableArray.CreateBuilder<string>(types.Length);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var requestedNames = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);

        for (var i = 0; i < model.SerializableTypes.Length; i++)
        {
            var requestedName = model.SerializableTypes[i].TypeInfoPropertyName;
            if (!string.IsNullOrWhiteSpace(requestedName) &&
                !requestedNames.ContainsKey(model.SerializableTypes[i].TypeSymbol))
            {
                requestedNames.Add(model.SerializableTypes[i].TypeSymbol, requestedName!);
            }
        }

        foreach (var member in model.ContextSymbol.GetMembers())
        {
            usedNames.Add(member.Name);
        }

        usedNames.Add("Default");
        usedNames.Add("Options");
        usedNames.Add("TypeInfo");
        usedNames.Add("GetTypeInfo");

        for (var i = 0; i < types.Length; i++)
        {
            var baseName = requestedNames.TryGetValue(types[i], out var requestedName)
                ? SanitizeTypeInfoPropertyName(requestedName)
                : SanitizeTypeInfoPropertyName(BuildTypeInfoPropertyBaseName(types[i]));
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "TypeInfo";
            }

            var candidate = baseName;
            var suffix = 1;
            while (!SyntaxFacts.IsValidIdentifier(candidate) || usedNames.Contains(candidate))
            {
                candidate = baseName + suffix.ToString();
                suffix++;
            }

            usedNames.Add(candidate);
            names.Add(candidate);
        }

        return names.ToImmutable();
    }

    private static string BuildTypeInfoPropertyBaseName(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementName = BuildTypeInfoPropertyBaseName(arrayType.ElementType);
            return arrayType.Rank == 1
                ? elementName + "Array"
                : elementName + arrayType.Rank.ToString() + "DArray";
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && namedType.TypeArguments.Length == 1)
            {
                return "Nullable" + BuildTypeInfoPropertyBaseName(namedType.TypeArguments[0]);
            }

            var name = new StringBuilder();
            AppendContainingTypeNames(name, namedType.ContainingType);
            name.Append(StripGenericArity(namedType.Name));
            if (namedType.IsGenericType)
            {
                for (var i = 0; i < namedType.TypeArguments.Length; i++)
                {
                    name.Append(BuildTypeInfoPropertyBaseName(namedType.TypeArguments[i]));
                }
            }

            return name.ToString();
        }

        if (!string.IsNullOrEmpty(typeSymbol.Name))
        {
            return typeSymbol.Name;
        }

        return typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    private static void AppendContainingTypeNames(StringBuilder builder, INamedTypeSymbol? containingType)
    {
        if (containingType is null)
        {
            return;
        }

        AppendContainingTypeNames(builder, containingType.ContainingType);
        builder.Append(StripGenericArity(containingType.Name));
    }

    private static string StripGenericArity(string typeName)
    {
        var tickIndex = typeName.IndexOf('`');
        return tickIndex >= 0 ? typeName.Substring(0, tickIndex) : typeName;
    }

    private static string SanitizeTypeInfoPropertyName(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "TypeInfo";
        }

        var builder = new StringBuilder(text.Length + 1);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(c);
            }
        }

        if (builder.Length == 0)
        {
            return "TypeInfo";
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, '_');
        }

        var candidate = builder.ToString();
        if (SyntaxFacts.GetKeywordKind(candidate) != SyntaxKind.None)
        {
            candidate += "Value";
        }

        return candidate;
    }

    private static void ApplyJsonSourceGenerationOptionsAttribute(AttributeData attribute, SourceGenerationOptionsModel model)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case "WriteIndented":
                    model.WriteIndented = argument.Value.Value as bool?;
                    break;
                case "IndentSize":
                    model.IndentSize = argument.Value.Value as int?;
                    break;
                case "PropertyNameCaseInsensitive":
                    model.PropertyNameCaseInsensitive = argument.Value.Value as bool?;
                    break;
                case "DefaultIgnoreCondition":
                    model.DefaultIgnoreCondition = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "PropertyNamingPolicy":
                    model.PropertyNamingPolicy = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "DictionaryKeyPolicy":
                    model.DictionaryKeyPolicy = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
            }
        }
    }

    private static void ApplyYamlSourceGenerationOptionsAttribute(AttributeData attribute, SourceGenerationOptionsModel model)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case "WriteIndented":
                    model.WriteIndented = argument.Value.Value as bool?;
                    break;
                case "IndentSize":
                    model.IndentSize = argument.Value.Value as int?;
                    break;
                case "PropertyNameCaseInsensitive":
                    model.PropertyNameCaseInsensitive = argument.Value.Value as bool?;
                    break;
                case "DefaultIgnoreCondition":
                    model.DefaultIgnoreCondition = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "PropertyNamingPolicy":
                    model.PropertyNamingPolicy = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "DictionaryKeyPolicy":
                    model.DictionaryKeyPolicy = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "MappingOrder":
                    model.MappingOrder = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "Schema":
                    model.Schema = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "UseSchema":
                    model.UseSchema = argument.Value.Value as bool?;
                    break;
                case "DuplicateKeyHandling":
                    model.DuplicateKeyHandling = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "UnsafeAllowDeserializeFromTagTypeName":
                    model.UnsafeAllowDeserializeFromTagTypeName = argument.Value.Value as bool?;
                    break;
                case "ReferenceHandling":
                    model.ReferenceHandling = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "SourceName":
                    model.SourceName = argument.Value.Value as string;
                    break;
                case "PreferPlainStyle":
                    model.PreferPlainStyle = argument.Value.Value as bool?;
                    break;
                case "PreferQuotedForAmbiguousScalars":
                    model.PreferQuotedForAmbiguousScalars = argument.Value.Value as bool?;
                    break;
                case "DiscriminatorStyle":
                    model.DiscriminatorStyle = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "TypeDiscriminatorPropertyName":
                    model.TypeDiscriminatorPropertyName = argument.Value.Value as string;
                    break;
                case "UnknownDerivedTypeHandling":
                    model.UnknownDerivedTypeHandling = NormalizeEnumName(argument.Value.ToCSharpString());
                    break;
                case "Converters":
                    if (argument.Value.Kind == TypedConstantKind.Array)
                    {
                        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();
                        foreach (var item in argument.Value.Values)
                        {
                            if (item.Kind == TypedConstantKind.Type && item.Value is ITypeSymbol converterType)
                            {
                                builder.Add(converterType);
                            }
                        }

                        model.ConverterTypes = builder.ToImmutable();
                    }
                    break;
            }
        }
    }

    private static string? NormalizeEnumName(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.ToString();
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lastDot = text.LastIndexOf('.');
        return lastDot >= 0 && lastDot < text.Length - 1 ? text.Substring(lastDot + 1) : text;
    }

    private static void AppendOptionAssignments(StringBuilder builder, SourceGenerationOptionsModel options)
    {
        if (options.WriteIndented.HasValue)
        {
            builder.Append("            WriteIndented = ")
                .Append(options.WriteIndented.Value ? "true" : "false")
                .AppendLine(",");
        }

        if (options.IndentSize.HasValue)
        {
            builder.Append("            IndentSize = ")
                .Append(options.IndentSize.Value)
                .AppendLine(",");
        }

        if (options.PropertyNameCaseInsensitive.HasValue)
        {
            builder.Append("            PropertyNameCaseInsensitive = ")
                .Append(options.PropertyNameCaseInsensitive.Value ? "true" : "false")
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.DefaultIgnoreCondition))
        {
            builder.Append("            DefaultIgnoreCondition = global::SharpYaml.YamlIgnoreCondition.")
                .Append(options.DefaultIgnoreCondition)
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.PropertyNamingPolicy))
        {
            builder.Append("            PropertyNamingPolicy = global::System.Text.Json.JsonNamingPolicy.")
                .Append(options.PropertyNamingPolicy)
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.DictionaryKeyPolicy))
        {
            builder.Append("            DictionaryKeyPolicy = global::System.Text.Json.JsonNamingPolicy.")
                .Append(options.DictionaryKeyPolicy)
                .AppendLine(",");
        }

        if (options.SourceName is { Length: > 0 } sourceName)
        {
            builder.Append("            SourceName = ")
                .Append(ToLiteral(sourceName))
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.MappingOrder))
        {
            builder.Append("            MappingOrder = global::SharpYaml.YamlMappingOrderPolicy.")
                .Append(options.MappingOrder)
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.Schema))
        {
            builder.Append("            Schema = global::SharpYaml.YamlSchemaKind.")
                .Append(options.Schema)
                .AppendLine(",");
        }

        if (options.UseSchema.HasValue)
        {
            builder.Append("            UseSchema = ")
                .Append(options.UseSchema.Value ? "true" : "false")
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.DuplicateKeyHandling))
        {
            builder.Append("            DuplicateKeyHandling = global::SharpYaml.YamlDuplicateKeyHandling.")
                .Append(options.DuplicateKeyHandling)
                .AppendLine(",");
        }

        if (options.UnsafeAllowDeserializeFromTagTypeName.HasValue)
        {
            builder.Append("            UnsafeAllowDeserializeFromTagTypeName = ")
                .Append(options.UnsafeAllowDeserializeFromTagTypeName.Value ? "true" : "false")
                .AppendLine(",");
        }

        if (!string.IsNullOrEmpty(options.ReferenceHandling))
        {
            builder.Append("            ReferenceHandling = global::SharpYaml.YamlReferenceHandling.")
                .Append(options.ReferenceHandling)
                .AppendLine(",");
        }

        if (options.PreferPlainStyle.HasValue || options.PreferQuotedForAmbiguousScalars.HasValue)
        {
            builder.AppendLine("            ScalarStylePreferences = new global::SharpYaml.YamlScalarStylePreferences");
            builder.AppendLine("            {");
            if (options.PreferPlainStyle.HasValue)
            {
                builder.Append("                PreferPlainStyle = ")
                    .Append(options.PreferPlainStyle.Value ? "true" : "false")
                    .AppendLine(",");
            }

            if (options.PreferQuotedForAmbiguousScalars.HasValue)
            {
                builder.Append("                PreferQuotedForAmbiguousScalars = ")
                    .Append(options.PreferQuotedForAmbiguousScalars.Value ? "true" : "false")
                    .AppendLine(",");
            }

            builder.AppendLine("            },");
        }

        if (!string.IsNullOrEmpty(options.DiscriminatorStyle) ||
            options.TypeDiscriminatorPropertyName is not null ||
            !string.IsNullOrEmpty(options.UnknownDerivedTypeHandling))
        {
            builder.AppendLine("            PolymorphismOptions = new global::SharpYaml.YamlPolymorphismOptions");
            builder.AppendLine("            {");
            if (!string.IsNullOrEmpty(options.DiscriminatorStyle))
            {
                builder.Append("                DiscriminatorStyle = global::SharpYaml.YamlTypeDiscriminatorStyle.")
                    .Append(options.DiscriminatorStyle)
                    .AppendLine(",");
            }

            if (options.TypeDiscriminatorPropertyName is string typeDiscriminatorPropertyName)
            {
                builder.Append("                TypeDiscriminatorPropertyName = ")
                    .Append(ToLiteral(typeDiscriminatorPropertyName))
                    .AppendLine(",");
            }

            if (!string.IsNullOrEmpty(options.UnknownDerivedTypeHandling))
            {
                builder.Append("                UnknownDerivedTypeHandling = global::SharpYaml.YamlUnknownDerivedTypeHandling.")
                    .Append(options.UnknownDerivedTypeHandling)
                    .AppendLine(",");
            }

            builder.AppendLine("            },");
        }

        if (!options.ConverterTypes.IsDefaultOrEmpty)
        {
            builder.AppendLine("            Converters = new global::SharpYaml.Serialization.YamlConverter[]");
            builder.AppendLine("            {");
            for (var i = 0; i < options.ConverterTypes.Length; i++)
            {
                var converterType = options.ConverterTypes[i].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.Append("                new ").Append(converterType).AppendLine("(),");
            }
            builder.AppendLine("            },");
        }
    }

    private static string ToLiteral(string value)
        => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private static string TrimGlobalPrefix(string fullyQualified)
        => fullyQualified.StartsWith("global::", StringComparison.Ordinal) ? fullyQualified.Substring("global::".Length) : fullyQualified;
}
