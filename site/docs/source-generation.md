---
title: Source generation and NativeAOT
---

SharpYaml supports an incremental source generator for high-performance, NativeAOT-friendly serialization.

## When to use source generation

Use source generation when:

- You publish with `PublishAot=true`
- You trim aggressively (`PublishTrimmed=true`)
- You want to avoid reflection and reduce startup overhead

## Define a context

SharpYaml uses [`YamlSerializableAttribute`](xref:SharpYaml.Serialization.YamlSerializableAttribute) to declare source-generated roots.

```csharp
using SharpYaml.Serialization;

[YamlSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = System.Text.Json.JsonKnownNamingPolicy.CamelCase,
    PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate)]
[YamlSerializable(typeof(MyConfig))]
[YamlSerializable(typeof(List<int>))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

The context type must be `partial` so the generator can add metadata properties.

Root types pull in statically discoverable member types transitively, including nested object properties,
collection elements, and dictionary values with supported key types. The discovered types can be declared
in the same assembly or in referenced assemblies. Add extra `[YamlSerializable]` entries only when you
need to serialize a type as an independent root or want to give its generated `YamlTypeInfo<T>` property
an explicit name. Untyped `object` members are handled by SharpYaml's built-in dynamic converter instead
of being generated as a concrete object graph.

## Compile-time options

Use [`YamlSourceGenerationOptionsAttribute`](xref:SharpYaml.Serialization.YamlSourceGenerationOptionsAttribute) to fix a context's default [`YamlSerializerOptions`](xref:SharpYaml.YamlSerializerOptions) at build time (including converter registration):

```csharp
[YamlSourceGenerationOptions(
    WriteIndented = false,
    BlockSequenceMappingStyle = SharpYaml.YamlSequenceItemStyle.Compact,
    PropertyNamingPolicy = System.Text.Json.JsonKnownNamingPolicy.CamelCase,
    PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate,
    Converters = new[] { typeof(MyCustomConverter) })]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

`PreferredObjectCreationHandling` works the same way as [`YamlSerializerOptions.PreferredObjectCreationHandling`](xref:SharpYaml.YamlSerializerOptions.PreferredObjectCreationHandling): `Replace` is the default, and `Populate` reuses existing mutable members when possible.

## Use generated metadata

Use the generated [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) properties (recommended):

```csharp
var yaml = YamlSerializer.Serialize(value, MyYamlContext.Default.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, MyYamlContext.Default.MyConfig);
```

The same `YamlTypeInfo<T>` metadata can be passed to reader, writer, and stream overloads when you want to avoid intermediate strings:

```csharp
YamlSerializer.Serialize(writer, value, MyYamlContext.Default.MyConfig);
var roundTrip = YamlSerializer.Deserialize(reader, MyYamlContext.Default.MyConfig);
```

For APIs that take a `Type`, prefer the overloads that accept a context:

```csharp
var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), MyYamlContext.Default);
var roundTrip = YamlSerializer.Deserialize(yaml, typeof(MyConfig), MyYamlContext.Default);
```

Prefer the overloads that accept a [`YamlSerializerContext`](xref:SharpYaml.Serialization.YamlSerializerContext) or a [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) directly. This avoids reflection and works well with trimming and NativeAOT.

Generated string members use the same scalar quoting rules as reflection-based serialization.
By default, ambiguous strings such as `"null"`, `"true"`, and `"123"` are quoted to preserve their
string values on round-trip; actual null references are written as YAML nulls. Setting
`ScalarStylePreferences.PreferQuotedForAmbiguousScalars = false` disables this protection,
so a string such as `"null"` can deserialize as a null reference.

## Runtime converters with generated contexts

Converters declared in [`YamlSourceGenerationOptionsAttribute.Converters`](xref:SharpYaml.Serialization.YamlSourceGenerationOptionsAttribute.Converters) are resolved by the source generator at build time. If you need to provide runtime converter instances, create options whose [`TypeInfoResolver`](xref:SharpYaml.YamlSerializerOptions.TypeInfoResolver) is the generated context:

```csharp
var options = MyYamlContext.Default.CreateOptions(o => o with
{
    Converters = [new MyRuntimeConverter()],
});

var typeInfo = (YamlTypeInfo<MyConfig>)MyYamlContext.Default.GetTypeInfo(typeof(MyConfig), options)!;
var yaml = YamlSerializer.Serialize(value, typeInfo);
```

Generated contexts resolve supported runtime converter replacements when the generated `YamlTypeInfo<T>` is initialized. Serialization and deserialization do not scan [`YamlSerializerOptions.Converters`](xref:SharpYaml.YamlSerializerOptions.Converters) for generated types. Runtime converters can only replace converter decisions for types that are already part of the generated converter graph; they do not make arbitrary new runtime-only types serializable.

When you reuse the same runtime converter set repeatedly, cache and reuse the generated `YamlTypeInfo<T>` resolved with those options. Passing a separate options instance with `TypeInfoResolver = MyYamlContext.Default` to options-based overloads can require option-specific generated type-info initialization again for each operation.

## Naming policy and generated code

For source generation, member names are resolved at build time using:

- [`YamlPropertyNameAttribute`](xref:SharpYaml.Serialization.YamlPropertyNameAttribute) / [`JsonPropertyNameAttribute`](xref:System.Text.Json.Serialization.JsonPropertyNameAttribute) when present (these override naming policies)
- otherwise, [`YamlSourceGenerationOptionsAttribute.PropertyNamingPolicy`](xref:SharpYaml.Serialization.YamlSourceGenerationOptionsAttribute.PropertyNamingPolicy) (or no policy when unspecified)

The generated serializer stores the resolved names directly and does not call `ConvertName(...)` at runtime for object members.

## Constructor selection

Source-generated deserialization honors [`YamlConstructorAttribute`](xref:SharpYaml.Serialization.YamlConstructorAttribute) and [`JsonConstructorAttribute`](xref:System.Text.Json.Serialization.JsonConstructorAttribute).

- `public`, `internal`, and `protected internal` constructors can be called directly from generated code.
- `private`, `protected`, and `private protected` constructors remain reflection-only; use reflection-based serialization for those models.
- When no constructor attribute is present, source generation follows the same default-constructor/public-constructor selection rules as reflection-based serialization.

## Reflection control

Reflection fallback can be disabled globally before first serializer use:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When reflection is disabled, you must provide metadata via [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) or [`YamlSerializerOptions.TypeInfoResolver`](xref:SharpYaml.YamlSerializerOptions.TypeInfoResolver).
This applies to .NET object mapping (POCOs, collections of POCOs, etc.).
Built-in primitives and untyped containers remain supported without reflection.

## NativeAOT defaults

When publishing with NativeAOT (`PublishAot=true`), the SharpYaml NuGet package disables reflection-based serialization by default via a feature switch.

You can override the default by setting this MSBuild property in your app project:

```xml
<PropertyGroup>
  <SharpYamlIsReflectionEnabledByDefault>true</SharpYamlIsReflectionEnabledByDefault>
</PropertyGroup>
```

Even when reflection-based object mapping is disabled, SharpYaml still supports serialization/deserialization for:

- scalar primitives (`bool`, numeric types, `string`, `char`, `decimal`)
- untyped containers: `object`, `Dictionary<string, object>`, `List<object>`, `object[]`

## Troubleshooting

- If generated properties are missing, ensure the project references the `SharpYaml` NuGet package (the generator is shipped in-package under `analyzers/dotnet/cs`).
- Ensure the context class is `partial`.
- Ensure roots are declared via `[YamlSerializable(typeof(...))]`.
