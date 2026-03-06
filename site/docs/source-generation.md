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
    PropertyNamingPolicy = System.Text.Json.JsonKnownNamingPolicy.CamelCase)]
[YamlSerializable(typeof(MyConfig))]
[YamlSerializable(typeof(List<int>))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

The context type must be `partial` so the generator can add metadata properties.

## Compile-time options

Use [`YamlSourceGenerationOptionsAttribute`](xref:SharpYaml.Serialization.YamlSourceGenerationOptionsAttribute) to fix a context's default [`YamlSerializerOptions`](xref:SharpYaml.YamlSerializerOptions) at build time (including converter registration):

```csharp
[YamlSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = System.Text.Json.JsonKnownNamingPolicy.CamelCase,
    Converters = new[] { typeof(MyCustomConverter) })]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

## Use generated metadata

Use the generated [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) properties (recommended):

```csharp
var yaml = YamlSerializer.Serialize(value, MyYamlContext.Default.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, MyYamlContext.Default.MyConfig);
```

For APIs that take a `Type`, prefer the overloads that accept a context:

```csharp
var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), MyYamlContext.Default);
var roundTrip = YamlSerializer.Deserialize(yaml, typeof(MyConfig), MyYamlContext.Default);
```

Prefer the overloads that accept a [`YamlSerializerContext`](xref:SharpYaml.Serialization.YamlSerializerContext) or a [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) directly. This avoids reflection and works well with trimming and NativeAOT.

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
