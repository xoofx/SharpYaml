using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
        messageFormat: "Type '{0}' contains member '{1}' of unsupported type '{2}'. Add [JsonSerializable(typeof({2}))] to the context or change the member type.",
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
            ImmutableArray<ITypeSymbol> serializableTypes,
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
        public ImmutableArray<ITypeSymbol> SerializableTypes { get; }
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

        var compilationAndModels = context.CompilationProvider.Combine(candidateContexts.Collect());
        context.RegisterSourceOutput(compilationAndModels, static (spc, input) =>
        {
            var compilation = input.Left;
            var models = input.Right;

            var byMetadataName = new Dictionary<string, ContextModel>(StringComparer.Ordinal);
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

        var serializableTypes = ImmutableArray.CreateBuilder<ITypeSymbol>();
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

                serializableTypes.Add(typeSymbol);
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

        var isPartial = classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
        var containingNamespace = classSymbol.ContainingNamespace;
        var namespaceName = containingNamespace is { IsGlobalNamespace: false } ? containingNamespace.ToDisplayString() : string.Empty;
        var typeName = classSymbol.Name;

        return new ContextModel(
            classSymbol,
            namespaceName,
            typeName,
            serializableTypes.ToImmutable(),
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

        var resolvedTypes = model.SerializableTypes;

        var indexByType = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        for (var i = 0; i < resolvedTypes.Length; i++)
        {
            indexByType[resolvedTypes[i]] = i;
        }

        // Validate that member types are generated as well (or are known scalars).
        for (var i = 0; i < resolvedTypes.Length; i++)
        {
            if (resolvedTypes[i] is not INamedTypeSymbol named || (named.TypeKind != TypeKind.Class && named.TypeKind != TypeKind.Struct))
            {
                continue;
            }

            foreach (var member in GetSerializableMembers(named))
            {
                var memberType = GetMemberType(member);
                if (memberType is null)
                {
                    continue;
                }

                if (IsKnownScalar(memberType))
                {
                    continue;
                }

                if (TryGetArrayElementType(memberType, out var arrayElementType) || TryGetListElementType(memberType, out arrayElementType))
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

                if (TryGetDictionaryValueType(memberType, out var dictionaryValueType))
                {
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

    private static string GenerateContextSource(ContextModel model, ImmutableArray<ITypeSymbol> types, Dictionary<ITypeSymbol, int> indexByType)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Globalization;");
        builder.AppendLine("using System.IO;");
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
        builder.AppendLine("        var options = new global::SharpYaml.YamlSerializerOptions();");
        AppendOptionAssignments(builder, model.SourceGenerationOptions);
        builder.AppendLine("        return options;");
        builder.AppendLine("    }");
        builder.AppendLine();

        for (var index = 0; index < types.Length; index++)
        {
            var serializableType = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("    private global::SharpYaml.YamlTypeInfo<").Append(serializableType).Append(">? _typeInfo").Append(index).AppendLine(";");
        }

        builder.AppendLine();
        builder.AppendLine("    public override global::SharpYaml.YamlTypeInfo? GetTypeInfo(global::System.Type type, global::SharpYaml.YamlSerializerOptions options)");
        builder.AppendLine("    {");

        for (var index = 0; index < types.Length; index++)
        {
            var serializableType = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.Append("        if (type == typeof(").Append(serializableType).AppendLine("))");
            builder.AppendLine("        {");
            builder.Append("            return GetTypeInfo").Append(index).AppendLine("(options);");
            builder.AppendLine("        }");
            builder.AppendLine();
        }

        builder.AppendLine("        return null;");
        builder.AppendLine("    }");
        builder.AppendLine();

        for (var index = 0; index < types.Length; index++)
        {
            var serializableType = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
            builder.AppendLine("        public GeneratedTypeInfo" + index + "(global::SharpYaml.YamlSerializerOptions options) : base(options) { }");
            builder.AppendLine();
            builder.Append("        public override string Serialize(").Append(serializableType).AppendLine(" value)");
            builder.AppendLine("        {");
            builder.AppendLine("            using var writer = new global::System.IO.StringWriter(global::System.Globalization.CultureInfo.InvariantCulture);");
            builder.AppendLine("            var yamlWriter = new global::SharpYaml.Serialization.YamlWriter(writer, Options);");
            builder.Append("            WriteValue").Append(index).AppendLine("(yamlWriter, value, Options);");
            builder.AppendLine("            var text = writer.ToString();");
            builder.AppendLine("            return text.EndsWith(\"\\n\", global::System.StringComparison.Ordinal) ? text : text + \"\\n\";");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.Append("        public override ").Append(serializableType).AppendLine(" Deserialize(string yaml)");
            builder.AppendLine("        {");
            builder.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(yaml);");
            builder.AppendLine("            var reader = global::SharpYaml.Serialization.YamlReader.Create(yaml, Options);");
            builder.AppendLine("            if (!reader.Read())");
            builder.AppendLine("            {");
            builder.AppendLine("                return default;");
            builder.AppendLine("            }");
            builder.Append("            return ReadValue").Append(index).AppendLine("(ref reader, Options);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        for (var index = 0; index < types.Length; index++)
        {
            var typeName = types[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            EmitWriteValue(builder, index, types[index], typeName, indexByType);
            builder.AppendLine();
            EmitReadValue(builder, index, types[index], typeName, indexByType);
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void EmitWriteValue(StringBuilder builder, int index, ITypeSymbol typeSymbol, string typeName, Dictionary<ITypeSymbol, int> indexByType)
    {
        builder.Append("    private static void WriteValue").Append(index)
            .Append("(global::SharpYaml.Serialization.YamlWriter writer, ").Append(typeName).AppendLine(" value, global::SharpYaml.YamlSerializerOptions options)");
        builder.AppendLine("    {");

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
            builder.AppendLine("        writer.WriteScalar(value);");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.AppendLine("        writer.WriteScalar(value ? \"true\" : \"false\");");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
        {
            builder.AppendLine("        writer.WriteScalar(value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.AppendLine("        writer.WriteScalar(((long)value).ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.AppendLine("        writer.WriteScalar(((ulong)value).ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.AppendLine("        if (double.IsPositiveInfinity(value)) { writer.WriteScalar(\".inf\"); return; }");
            builder.AppendLine("        if (double.IsNegativeInfinity(value)) { writer.WriteScalar(\"-.inf\"); return; }");
            builder.AppendLine("        if (double.IsNaN(value)) { writer.WriteScalar(\".nan\"); return; }");
            builder.AppendLine("        writer.WriteScalar(value.ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture));");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.AppendLine("        if (float.IsPositiveInfinity(value)) { writer.WriteScalar(\".inf\"); return; }");
            builder.AppendLine("        if (float.IsNegativeInfinity(value)) { writer.WriteScalar(\"-.inf\"); return; }");
            builder.AppendLine("        if (float.IsNaN(value)) { writer.WriteScalar(\".nan\"); return; }");
            builder.AppendLine("        writer.WriteScalar(value.ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture));");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.AppendLine("        writer.WriteScalar(value.ToString());");
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

        if (TryGetListElementType(typeSymbol, out var listElementType))
        {
            builder.AppendLine("        if (value is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            writer.WriteNullValue();");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        writer.WriteStartSequence();");
            builder.AppendLine("        for (var i = 0; i < value.Count; i++)");
            builder.AppendLine("        {");
            builder.AppendLine("            var element = value[i];");
            EmitWriteKnownType(builder, listElementType, indexByType, "element", indent: "            ");
            builder.AppendLine("        }");
            builder.AppendLine("        writer.WriteEndSequence();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetDictionaryValueType(typeSymbol, out var dictionaryValueType))
        {
            builder.AppendLine("        if (value is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            writer.WriteNullValue();");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        writer.WriteStartMapping();");
            builder.AppendLine("        foreach (var pair in value)");
            builder.AppendLine("        {");
            builder.AppendLine("            var key = options.DictionaryKeyPolicy?.ConvertName(pair.Key) ?? pair.Key;");
            builder.AppendLine("            writer.WritePropertyName(key);");
            EmitWriteKnownType(builder, dictionaryValueType, indexByType, "pair.Value", indent: "            ");
            builder.AppendLine("        }");
            builder.AppendLine("        writer.WriteEndMapping();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol is INamedTypeSymbol named && (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct))
        {
            if (named.TypeKind == TypeKind.Class)
            {
                builder.AppendLine("        if (value is null)");
                builder.AppendLine("        {");
                builder.AppendLine("            writer.WriteNullValue();");
                builder.AppendLine("            return;");
                builder.AppendLine("        }");
            }

            builder.AppendLine("        writer.WriteStartMapping();");

            var members = GetSerializableMembers(named)
                .Select(m => CreateMemberModel(m))
                .ToImmutableArray();

            foreach (var member in members)
            {
                var memberValueVar = "__value" + index + "_" + member.Symbol.Name;
                var ignoreVar = "__ignore" + index + "_" + member.Symbol.Name;

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
                    builder.Append("                writer.WritePropertyName(").Append(member.SerializedNameExpression).AppendLine(");");
                    EmitWriteMemberValue(builder, member, indexByType, valueExpression: memberValueVar);
                    builder.AppendLine("            }");
                }
                else
                {
                    builder.AppendLine("            // Value types are never null.");
                    builder.Append("            writer.WritePropertyName(").Append(member.SerializedNameExpression).AppendLine(");");
                    EmitWriteMemberValue(builder, member, indexByType, valueExpression: memberValueVar);
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
                builder.Append("                writer.WritePropertyName(").Append(member.SerializedNameExpression).AppendLine(");");
                EmitWriteMemberValue(builder, member, indexByType, valueExpression: memberValueVar);
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine("        else");
                builder.AppendLine("        {");
                builder.Append("            writer.WritePropertyName(").Append(member.SerializedNameExpression).AppendLine(");");
                EmitWriteMemberValue(builder, member, indexByType, valueExpression: memberValueVar);
                builder.AppendLine("        }");
            }

            builder.AppendLine("        writer.WriteEndMapping();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            return;
        }

        builder.AppendLine("        throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this type.\");");
        builder.AppendLine("    }");
    }

    private static void EmitReadValue(StringBuilder builder, int index, ITypeSymbol typeSymbol, string typeName, Dictionary<ITypeSymbol, int> indexByType)
    {
        builder.Append("    private static ").Append(typeName).Append(typeSymbol.IsReferenceType ? "?" : string.Empty).Append(" ReadValue").Append(index)
            .AppendLine("(ref global::SharpYaml.Serialization.YamlReader reader, global::SharpYaml.YamlSerializerOptions options)");
        builder.AppendLine("    {");

        if (typeSymbol is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        var text = reader.ScalarValue ?? string.Empty;");
            builder.Append("        if (global::System.Enum.TryParse<").Append(typeName).AppendLine(">(text, ignoreCase: true, out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return parsed;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(text.AsSpan(), out var numeric))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.Append("            return (").Append(typeName).AppendLine(")global::System.Enum.ToObject(typeof(" + typeName + "), numeric);");
            builder.AppendLine("        }");
            builder.AppendLine("        throw new global::System.FormatException($\"Invalid enum scalar '{text}'.\");");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetArrayElementType(typeSymbol, out var arrayElementType))
        {
            var elementTypeName = arrayElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a sequence token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.Append("        var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
            builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
            builder.AppendLine("        {");
            EmitReadKnownType(builder, arrayElementType, indexByType, "element", indent: "            ");
            builder.AppendLine("            list.Add(element);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return list.ToArray();");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetListElementType(typeSymbol, out var listElementType))
        {
            var elementTypeName = listElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a sequence token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.Append("        var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
            builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
            builder.AppendLine("        {");
            EmitReadKnownType(builder, listElementType, indexByType, "element", indent: "            ");
            builder.AppendLine("            list.Add(element);");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return list;");
            builder.AppendLine("    }");
            return;
        }

        if (TryGetDictionaryValueType(typeSymbol, out var dictionaryValueType))
        {
            var valueTypeName = dictionaryValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("        {");
            builder.AppendLine("            reader.Read();");
            builder.AppendLine("            return default;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a mapping token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.Append("        var dictionary = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
            builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new global::System.InvalidOperationException($\"Expected a scalar key token but found '{reader.TokenType}'.\");");
            builder.AppendLine("            }");
            builder.AppendLine("            var key = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("            reader.Read();");
            EmitReadKnownType(builder, dictionaryValueType, indexByType, "value", indent: "            ");
            builder.AppendLine("            if (dictionary.ContainsKey(key))");
            builder.AppendLine("            {");
            builder.AppendLine("                switch (options.DuplicateKeyHandling)");
            builder.AppendLine("                {");
            builder.AppendLine("                    case global::SharpYaml.YamlDuplicateKeyHandling.Error:");
            builder.AppendLine("                        throw new global::System.InvalidOperationException($\"Duplicate mapping key '{key}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        var value = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return value;");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid boolean scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid integer scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid integer scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid uint32 scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid uint64 scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed > byte.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid byte scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid sbyte scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed is < short.MinValue or > short.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid int16 scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed) || parsed > ushort.MaxValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid uint16 scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid nint scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid nuint scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid float scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader.ScalarValue.AsSpan(), out var parsed))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid float scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(reader.ScalarValue.AsSpan(), out var result))");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.FormatException($\"Invalid decimal scalar '{reader.ScalarValue}'.\");");
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
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        var text = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("        if (text.Length != 1) { throw new global::System.FormatException($\"Invalid char scalar '{text}'.\"); }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return text[0];");
            builder.AppendLine("    }");
            return;
        }

        if (typeSymbol is INamedTypeSymbol named && (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct))
        {
            if (named.TypeKind == TypeKind.Class)
            {
                builder.AppendLine("        if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
                builder.AppendLine("        {");
                builder.AppendLine("            reader.Read();");
                builder.AppendLine("            return default;");
                builder.AppendLine("        }");
            }

            builder.AppendLine("        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
            builder.AppendLine("        {");
            builder.AppendLine("            throw new global::System.InvalidOperationException($\"Expected a mapping token but found '{reader.TokenType}'.\");");
            builder.AppendLine("        }");

            builder.Append("        var instance = new ").Append(typeName).AppendLine("();");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
            builder.AppendLine("        {");
            builder.AppendLine("            if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("            {");
            builder.AppendLine("                throw new global::System.InvalidOperationException($\"Expected a scalar key token but found '{reader.TokenType}'.\");");
            builder.AppendLine("            }");
            builder.AppendLine("            var key = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("            reader.Read();");

            var members = GetSerializableMembers(named)
                .Select(m => CreateMemberModel(m))
                .ToImmutableArray();

            builder.AppendLine("            var matched = false;");
            foreach (var member in members)
            {
                builder.Append("            if (!matched && global::System.String.Equals(key, ").Append(member.SerializedNameExpression)
                    .Append(", options.PropertyNameCaseInsensitive ? global::System.StringComparison.OrdinalIgnoreCase : global::System.StringComparison.Ordinal))");
                builder.AppendLine();
                builder.AppendLine("            {");
                builder.AppendLine("                matched = true;");
                EmitReadMemberValue(builder, member, indexByType);
                builder.AppendLine("            }");
            }

            builder.AppendLine("            if (!matched)");
            builder.AppendLine("            {");
            builder.AppendLine("                reader.Skip();");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("        reader.Read();");
            builder.AppendLine("        return instance;");
            builder.AppendLine("    }");
            return;
        }

        builder.AppendLine("        throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this type.\");");
        builder.AppendLine("    }");
    }

    private static void EmitWriteMemberValue(StringBuilder builder, MemberModel member, Dictionary<ITypeSymbol, int> indexByType, string valueExpression)
    {
        if (member.Type is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.Append("                if (!").Append(valueExpression).AppendLine(".HasValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteNullValue();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.Append("                    var underlying = ").Append(valueExpression).AppendLine(".Value;");

            if (!TryEmitWriteScalar(builder, underlyingType, "underlying", indent: "                    "))
            {
                builder.AppendLine("                    throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this nullable member type.\");");
            }

            builder.AppendLine("                }");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_String)
        {
            builder.Append("                writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append("                writer.WriteScalar(").Append(valueExpression).AppendLine(" ? \"true\" : \"false\");");
            return;
        }

        if (member.Type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
        {
            builder.Append("                writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append("                writer.WriteScalar(((long)").Append(valueExpression).AppendLine(").ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append("                writer.WriteScalar(((ulong)").Append(valueExpression).AppendLine(").ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Double)
        {
            builder.Append("                if (double.IsPositiveInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".inf\"); }");
            builder.Append("                else if (double.IsNegativeInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\"-.inf\"); }");
            builder.Append("                else if (double.IsNaN(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".nan\"); }");
            builder.Append("                else { writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture)); }");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Single)
        {
            builder.Append("                if (float.IsPositiveInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".inf\"); }");
            builder.Append("                else if (float.IsNegativeInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\"-.inf\"); }");
            builder.Append("                else if (float.IsNaN(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".nan\"); }");
            builder.Append("                else { writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture)); }");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Char)
        {
            builder.Append("                writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString());");
            return;
        }

        if (member.Type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            builder.Append("                writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString());");
            return;
        }

        if (TryGetArrayElementType(member.Type, out var arrayElementType))
        {
            builder.Append("                if (").Append(valueExpression).AppendLine(" is null)");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteNullValue();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteStartSequence();");
            builder.Append("                    for (var i = 0; i < ").Append(valueExpression).AppendLine(".Length; i++)");
            builder.AppendLine("                    {");
            builder.Append("                        var element = ").Append(valueExpression).AppendLine("[i];");
            EmitWriteKnownType(builder, arrayElementType, indexByType, "element", indent: "                        ");
            builder.AppendLine("                    }");
            builder.AppendLine("                    writer.WriteEndSequence();");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetListElementType(member.Type, out var listElementType))
        {
            builder.Append("                if (").Append(valueExpression).AppendLine(" is null)");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteNullValue();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteStartSequence();");
            builder.Append("                    for (var i = 0; i < ").Append(valueExpression).AppendLine(".Count; i++)");
            builder.AppendLine("                    {");
            builder.Append("                        var element = ").Append(valueExpression).AppendLine("[i];");
            EmitWriteKnownType(builder, listElementType, indexByType, "element", indent: "                        ");
            builder.AppendLine("                    }");
            builder.AppendLine("                    writer.WriteEndSequence();");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetDictionaryValueType(member.Type, out var dictionaryValueType))
        {
            builder.Append("                if (").Append(valueExpression).AppendLine(" is null)");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteNullValue();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    writer.WriteStartMapping();");
            builder.Append("                    foreach (var pair in ").Append(valueExpression).AppendLine(")");
            builder.AppendLine("                    {");
            builder.AppendLine("                        var key = options.DictionaryKeyPolicy?.ConvertName(pair.Key) ?? pair.Key;");
            builder.AppendLine("                        writer.WritePropertyName(key);");
            EmitWriteKnownType(builder, dictionaryValueType, indexByType, "pair.Value", indent: "                        ");
            builder.AppendLine("                    }");
            builder.AppendLine("                    writer.WriteEndMapping();");
            builder.AppendLine("                }");
            return;
        }

        if (indexByType.TryGetValue(member.Type, out var typeIndex))
        {
            builder.Append("                WriteValue").Append(typeIndex).Append("(writer, ").Append(valueExpression).AppendLine(", options);");
            return;
        }

        builder.AppendLine("                throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this member type.\");");
    }

    private static void EmitReadMemberValue(StringBuilder builder, MemberModel member, Dictionary<ITypeSymbol, int> indexByType)
    {
        if (member.Type is INamedTypeSymbol nullableType && nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = nullableType.TypeArguments[0];
            builder.AppendLine("                if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
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
                builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("reader.ScalarValue ?? string.Empty")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Boolean)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(reader.ScalarValue.AsSpan(), out var parsedBool))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid boolean scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedBool")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Byte)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsedByte) || parsedByte > byte.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid byte scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(byte)parsedByte")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_SByte)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsedSByte) || parsedSByte is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid sbyte scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(sbyte)parsedSByte")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Int16)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsedInt16) || parsedInt16 is < short.MinValue or > short.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid int16 scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(short)parsedInt16")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UInt16)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsedUInt16) || parsedUInt16 > ushort.MaxValue)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid uint16 scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(ushort)parsedUInt16")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Int32)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(reader.ScalarValue.AsSpan(), out var parsed))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid integer scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsed")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Int64)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsedInt64))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid integer scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedInt64")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UInt32)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(reader.ScalarValue.AsSpan(), out var parsedUInt32))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid uint32 scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedUInt32")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UInt64)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsedUInt64))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid uint64 scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedUInt64")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_IntPtr)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsedIntPtr))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid nint scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(nint)parsedIntPtr")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(reader.ScalarValue.AsSpan(), out var parsedUIntPtr))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid nuint scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(nuint)parsedUIntPtr")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Double)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader.ScalarValue.AsSpan(), out var parsedDouble))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid float scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedDouble")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Single)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(reader.ScalarValue.AsSpan(), out var parsedSingle))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid float scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("(float)parsedSingle")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Decimal)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(reader.ScalarValue.AsSpan(), out var parsedDecimal))");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid decimal scalar '{reader.ScalarValue}'.\");");
            builder.AppendLine("                }");
            builder.Append("                ").Append(member.AssignExpression("parsedDecimal")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type.SpecialType == SpecialType.System_Char)
        {
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                var text = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("                if (text.Length != 1) { throw new global::System.FormatException($\"Invalid char scalar '{text}'.\"); }");
            builder.Append("                ").Append(member.AssignExpression("text[0]")).AppendLine(";");
            builder.AppendLine("                reader.Read();");
            return;
        }

        if (member.Type is INamedTypeSymbol enumType && enumType.TypeKind == TypeKind.Enum)
        {
            var enumTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                }");
            builder.AppendLine("                var text = reader.ScalarValue ?? string.Empty;");
            builder.Append("                if (global::System.Enum.TryParse<").Append(enumTypeName).AppendLine(">(text, ignoreCase: true, out var parsedEnum))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("parsedEnum")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(text.AsSpan(), out var numeric))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression($"({enumTypeName})global::System.Enum.ToObject(typeof({enumTypeName}), numeric)")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new global::System.FormatException($\"Invalid enum scalar '{text}'.\");");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetArrayElementType(member.Type, out var arrayElementType))
        {
            var elementTypeName = arrayElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw new global::System.InvalidOperationException($\"Expected a sequence token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
            builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
            builder.AppendLine("                    {");
            EmitReadKnownType(builder, arrayElementType, indexByType, "element", indent: "                        ");
            builder.AppendLine("                        list.Add(element);");
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    ").Append(member.AssignExpression("list.ToArray()")).AppendLine(";");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetListElementType(member.Type, out var listElementType))
        {
            var elementTypeName = listElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartSequence)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw new global::System.InvalidOperationException($\"Expected a sequence token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    var list = new global::System.Collections.Generic.List<").Append(elementTypeName).AppendLine(">();");
            builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndSequence)");
            builder.AppendLine("                    {");
            EmitReadKnownType(builder, listElementType, indexByType, "element", indent: "                        ");
            builder.AppendLine("                        list.Add(element);");
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    ").Append(member.AssignExpression("list")).AppendLine(";");
            builder.AppendLine("                }");
            return;
        }

        if (TryGetDictionaryValueType(member.Type, out var dictionaryValueType))
        {
            var valueTypeName = dictionaryValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            builder.AppendLine("                if (reader.TokenType == global::SharpYaml.Serialization.YamlTokenType.Scalar && global::SharpYaml.Serialization.YamlScalar.IsNull(reader.ScalarValue.AsSpan()))");
            builder.AppendLine("                {");
            builder.Append("                    ").Append(member.AssignExpression("default")).AppendLine(";");
            builder.AppendLine("                    reader.Read();");
            builder.AppendLine("                }");
            builder.AppendLine("                else");
            builder.AppendLine("                {");
            builder.AppendLine("                    if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.StartMapping)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        throw new global::System.InvalidOperationException($\"Expected a mapping token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    var dictionary = new global::System.Collections.Generic.Dictionary<string, ").Append(valueTypeName)
                .AppendLine(">(options.PropertyNameCaseInsensitive ? global::System.StringComparer.OrdinalIgnoreCase : global::System.StringComparer.Ordinal);");
            builder.AppendLine("                    while (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.EndMapping)");
            builder.AppendLine("                    {");
            builder.AppendLine("                        if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.AppendLine("                        {");
            builder.AppendLine("                            throw new global::System.InvalidOperationException($\"Expected a scalar key token but found '{reader.TokenType}'.\");");
            builder.AppendLine("                        }");
            builder.AppendLine("                        var entryKey = reader.ScalarValue ?? string.Empty;");
            builder.AppendLine("                        reader.Read();");
            EmitReadKnownType(builder, dictionaryValueType, indexByType, "value", indent: "                        ");
            builder.AppendLine("                        if (dictionary.ContainsKey(entryKey))");
            builder.AppendLine("                        {");
            builder.AppendLine("                            switch (options.DuplicateKeyHandling)");
            builder.AppendLine("                            {");
            builder.AppendLine("                                case global::SharpYaml.YamlDuplicateKeyHandling.Error:");
            builder.AppendLine("                                    throw new global::System.InvalidOperationException($\"Duplicate mapping key '{entryKey}'.\");");
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
            builder.AppendLine("                    }");
            builder.AppendLine("                    reader.Read();");
            builder.Append("                    ").Append(member.AssignExpression("dictionary")).AppendLine(";");
            builder.AppendLine("                }");
            return;
        }

        if (indexByType.TryGetValue(member.Type, out var typeIndex))
        {
            builder.Append("                ").Append(member.AssignExpression($"ReadValue{typeIndex}(ref reader, options)")).AppendLine(";");
            return;
        }

        builder.AppendLine("                throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this member type.\");");
    }

    private static bool TryEmitWriteScalar(StringBuilder builder, ITypeSymbol typeSymbol, string valueExpression, string indent)
    {
        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(");");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(" ? \"true\" : \"false\");");
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
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append(indent).Append("writer.WriteScalar(((long)").Append(valueExpression).AppendLine(").ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append(indent).Append("writer.WriteScalar(((ulong)").Append(valueExpression).AppendLine(").ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.Append(indent).Append("if (double.IsPositiveInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".inf\"); }");
            builder.Append(indent).Append("else if (double.IsNegativeInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\"-.inf\"); }");
            builder.Append(indent).Append("else if (double.IsNaN(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".nan\"); }");
            builder.Append(indent).Append("else { writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture)); }");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.Append(indent).Append("if (float.IsPositiveInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".inf\"); }");
            builder.Append(indent).Append("else if (float.IsNegativeInfinity(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\"-.inf\"); }");
            builder.Append(indent).Append("else if (float.IsNaN(").Append(valueExpression).AppendLine(")) { writer.WriteScalar(\".nan\"); }");
            builder.Append(indent).Append("else { writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString(\"R\", global::System.Globalization.CultureInfo.InvariantCulture)); }");
            return true;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.Append(indent).Append("writer.WriteScalar(").Append(valueExpression).AppendLine(".ToString());");
            return true;
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
        var spanExpression = textExpression + ".AsSpan()";

        if (typeSymbol.SpecialType == SpecialType.System_String)
        {
            builder.Append(indent).Append("var ").Append(valueVarName).Append(" = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Boolean)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseBool(").Append(spanExpression).AppendLine(", out var parsedBool))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid boolean scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedBool;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int32)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt32(").Append(spanExpression).AppendLine(", out var parsedInt32))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid integer scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedInt32;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int64)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedInt64))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid integer scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedInt64;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt32)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt32(").Append(spanExpression).AppendLine(", out var parsedUInt32))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid uint32 scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedUInt32;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt64)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUInt64))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid uint64 scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedUInt64;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Byte)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedByte) || parsedByte > byte.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid byte scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (byte)parsedByte;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_SByte)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedSByte) || parsedSByte is < sbyte.MinValue or > sbyte.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid sbyte scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (sbyte)parsedSByte;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Int16)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedInt16) || parsedInt16 is < short.MinValue or > short.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid int16 scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (short)parsedInt16;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UInt16)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUInt16) || parsedUInt16 > ushort.MaxValue)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid uint16 scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (ushort)parsedUInt16;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Double)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(").Append(spanExpression).AppendLine(", out var parsedDouble))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid float scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDouble;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Single)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDouble(").Append(spanExpression).AppendLine(", out var parsedSingle))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid float scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (float)parsedSingle;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Decimal)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseDecimal(").Append(spanExpression).AppendLine(", out var parsedDecimal))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid decimal scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = parsedDecimal;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_IntPtr)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseInt64(").Append(spanExpression).AppendLine(", out var parsedIntPtr))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid nint scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (nint)parsedIntPtr;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_UIntPtr)
        {
            builder.Append(indent).Append("if (!global::SharpYaml.Serialization.YamlScalar.TryParseUInt64(").Append(spanExpression).AppendLine(", out var parsedUIntPtr))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid nuint scalar '{reader.ScalarValue}'.\");");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = (nuint)parsedUIntPtr;");
            return;
        }

        if (typeSymbol.SpecialType == SpecialType.System_Char)
        {
            builder.Append(indent).Append("var textChar = ").Append(textExpression).AppendLine(" ?? string.Empty;");
            builder.Append(indent).AppendLine("if (textChar.Length != 1) { throw new global::System.FormatException($\"Invalid char scalar '{textChar}'.\"); }");
            builder.Append(indent).Append("var ").Append(valueVarName).AppendLine(" = textChar[0];");
            return;
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
            builder.Append(indent).AppendLine("else if (global::SharpYaml.Serialization.YamlScalar.TryParseInt64(enumText.AsSpan(), out var numeric))");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("    ").Append(valueVarName).Append(" = (").Append(enumTypeName).AppendLine(")global::System.Enum.ToObject(typeof(" + enumTypeName + "), numeric);");
            builder.Append(indent).AppendLine("}");
            builder.Append(indent).AppendLine("else");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.FormatException($\"Invalid enum scalar '{enumText}'.\");");
            builder.Append(indent).AppendLine("}");
            return;
        }

        builder.Append(indent).AppendLine("throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this scalar type.\");");
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

    private static void EmitWriteKnownType(StringBuilder builder, ITypeSymbol typeSymbol, Dictionary<ITypeSymbol, int> indexByType, string valueExpression, string indent)
    {
        if (TryEmitWriteScalar(builder, typeSymbol, valueExpression, indent))
        {
            return;
        }

        if (indexByType.TryGetValue(typeSymbol, out var typeIndex))
        {
            builder.Append(indent).Append("WriteValue").Append(typeIndex).Append("(writer, ").Append(valueExpression).AppendLine(", options);");
            return;
        }

        builder.Append(indent).AppendLine("throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this element type.\");");
    }

    private static void EmitReadKnownType(StringBuilder builder, ITypeSymbol typeSymbol, Dictionary<ITypeSymbol, int> indexByType, string valueVarName, string indent)
    {
        if (IsKnownScalar(typeSymbol))
        {
            builder.Append(indent).AppendLine("if (reader.TokenType != global::SharpYaml.Serialization.YamlTokenType.Scalar)");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    throw new global::System.InvalidOperationException($\"Expected a scalar token but found '{reader.TokenType}'.\");");
            builder.Append(indent).AppendLine("}");
            EmitReadScalar(builder, typeSymbol, valueVarName, indent: indent);
            builder.Append(indent).AppendLine("reader.Read();");
            return;
        }

        if (indexByType.TryGetValue(typeSymbol, out var typeIndex))
        {
            builder.Append(indent).Append("var ").Append(valueVarName).Append(" = ReadValue").Append(typeIndex).AppendLine("(ref reader, options);");
            return;
        }

        builder.Append(indent).AppendLine("throw new global::System.NotSupportedException(\"The generated YAML serializer does not support this element type.\");");
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

    private sealed class MemberModel
    {
        public MemberModel(
            ISymbol symbol,
            ITypeSymbol type,
            string serializedNameExpression,
            string accessExpression,
            Func<string, string> assignExpression,
            string ignoreConditionExpression)
        {
            Symbol = symbol;
            Type = type;
            SerializedNameExpression = serializedNameExpression;
            AccessExpression = accessExpression;
            AssignExpression = assignExpression;
            IgnoreConditionExpression = ignoreConditionExpression;
        }

        public ISymbol Symbol { get; }
        public ITypeSymbol Type { get; }
        public string SerializedNameExpression { get; }
        public string AccessExpression { get; }
        public Func<string, string> AssignExpression { get; }
        public string IgnoreConditionExpression { get; }
    }

    private static MemberModel CreateMemberModel(ISymbol member)
    {
        var name = GetSerializedMemberNameExpression(member);
        var type = GetMemberType(member) ?? throw new InvalidOperationException("Member type could not be determined.");
        var accessExpression = member is IPropertySymbol prop ? "value." + prop.Name : "value." + member.Name;
        Func<string, string> assign = member is IPropertySymbol propAssign
            ? rhs => "instance." + propAssign.Name + " = " + rhs
            : rhs => "instance." + member.Name + " = " + rhs;
        var ignoreConditionExpression = GetIgnoreConditionExpression(member);
        return new MemberModel(member, type, name, accessExpression, assign, ignoreConditionExpression);
    }

    private static string GetSerializedMemberNameExpression(ISymbol member)
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
                    return ToLiteral(yamlName);
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
                    return ToLiteral(jsonName);
                }
            }
        }

        return $"options.PropertyNamingPolicy?.ConvertName({ToLiteral(member.Name)}) ?? {ToLiteral(member.Name)}";
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

    private static ImmutableArray<ISymbol> GetSerializableMembers(INamedTypeSymbol type)
    {
        var members = ImmutableArray.CreateBuilder<ISymbol>();
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                if (property.IsIndexer)
                {
                    continue;
                }

                var include = property.GetMethod is { DeclaredAccessibility: Accessibility.Public } &&
                              property.SetMethod is { DeclaredAccessibility: Accessibility.Public };

                if (!include && !HasAttribute(property, "SharpYaml.Serialization.YamlIncludeAttribute") && !HasAttribute(property, "System.Text.Json.Serialization.JsonIncludeAttribute"))
                {
                    continue;
                }

                if (HasAttribute(property, "SharpYaml.Serialization.YamlIgnoreAttribute") || HasJsonIgnoreAlways(property))
                {
                    continue;
                }

                members.Add(property);
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

                members.Add(field);
            }
        }

        return members.ToImmutable();
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

    private static bool IsJsonSerializableAttribute(AttributeData attribute)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Text.Json.Serialization.JsonSerializableAttribute", StringComparison.Ordinal);

    private static bool IsJsonSourceGenerationOptionsAttribute(AttributeData attribute)
        => string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute", StringComparison.Ordinal);

    private static void ApplySourceGenerationOptionsAttribute(AttributeData attribute, SourceGenerationOptionsModel model)
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

    private static string ToLiteral(string value)
        => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private static string TrimGlobalPrefix(string fullyQualified)
        => fullyQualified.StartsWith("global::", StringComparison.Ordinal) ? fullyQualified.Substring("global::".Length) : fullyQualified;
}
