---
title: API overview
---

# SharpYaml 3 API Reference

## Overview

SharpYaml 3 exposes a `System.Text.Json`-style object mapping API:

- `SharpYaml.YamlSerializer`
- `SharpYaml.YamlSerializerOptions`
- `SharpYaml.YamlTypeInfo` / `SharpYaml.YamlTypeInfo<T>`
- `SharpYaml.IYamlTypeInfoResolver`
- `SharpYaml.Serialization.YamlSerializerContext`

Object mapping is intentionally distinct from the low-level syntax APIs (`SharpYaml.Syntax`) used for lossless text roundtrip and span tracking.

## YamlSerializer

Core APIs:

- `Serialize<T>(T value, YamlSerializerOptions? options = null)`
- `Serialize(object? value, Type inputType, YamlSerializerOptions? options = null)`
- `Serialize<T>(T value, YamlSerializerContext context)`
- `Serialize(object? value, Type inputType, YamlSerializerContext context)`
- `Serialize<T>(TextWriter writer, T value, YamlSerializerOptions? options = null)`
- `Serialize(TextWriter writer, object? value, Type inputType, YamlSerializerOptions? options = null)`
- `Serialize<T>(TextWriter writer, T value, YamlSerializerContext context)`
- `Serialize(TextWriter writer, object? value, Type inputType, YamlSerializerContext context)`
- `Deserialize<T>(string yaml, YamlSerializerOptions? options = null)`
- `Deserialize(string yaml, Type returnType, YamlSerializerOptions? options = null)`
- `Deserialize<T>(string yaml, YamlSerializerContext context)`
- `Deserialize(string yaml, Type returnType, YamlSerializerContext context)`
- `Deserialize<T>(TextReader reader, YamlSerializerOptions? options = null)`
- `Deserialize(TextReader reader, Type returnType, YamlSerializerOptions? options = null)`
- `Deserialize<T>(TextReader reader, YamlSerializerContext context)`
- `Deserialize(TextReader reader, Type returnType, YamlSerializerContext context)`
- `Deserialize<T>(ReadOnlySpan<char> yaml, YamlSerializerOptions? options = null)`
- `Deserialize(ReadOnlySpan<char> yaml, Type returnType, YamlSerializerOptions? options = null)`
- `Deserialize<T>(ReadOnlySpan<char> yaml, YamlSerializerContext context)`
- `Deserialize(ReadOnlySpan<char> yaml, Type returnType, YamlSerializerContext context)`

Metadata APIs:

- `Serialize<T>(T value, YamlTypeInfo<T> typeInfo)`
- `Deserialize<T>(string yaml, YamlTypeInfo<T> typeInfo)`
- `Deserialize<T>(ReadOnlySpan<char> yaml, YamlTypeInfo<T> typeInfo)`

Reflection switch:

- `YamlSerializer.IsReflectionEnabledByDefault`
- AppContext key: `"SharpYaml.YamlSerializer.IsReflectionEnabledByDefault"`

When reflection is disabled and no resolver provides metadata, non-`YamlTypeInfo` overloads throw `InvalidOperationException`.

## YamlSerializerOptions

- Naming:
- `PropertyNamingPolicy`
- `DictionaryKeyPolicy`
- `PropertyNameCaseInsensitive`
- Ignore/write behavior:
- `DefaultIgnoreCondition`
- Formatting:
- `WriteIndented`
- `IndentSize`
- Mapping order:
- `MappingOrder` (`Declaration` default, `Sorted` optional)
- YAML semantics:
- `Schema`
- `DuplicateKeyHandling`
- `ReferenceHandling`
- `ScalarStylePreferences`
- `UnsafeAllowDeserializeFromTagTypeName`
- Polymorphism:
- `PolymorphismOptions`
- Metadata:
- `TypeInfoResolver`

## Attributes

SharpYaml attributes (`SharpYaml.Serialization`):

- `YamlPropertyNameAttribute`
- `YamlPropertyOrderAttribute`
- `YamlIgnoreAttribute`
- `YamlIncludeAttribute`
- `YamlConstructorAttribute`
- `YamlRequiredAttribute`
- `YamlExtensionDataAttribute`
- `YamlConverterAttribute`
- `YamlPolymorphicAttribute`
- `YamlDerivedTypeAttribute`

`System.Text.Json.Serialization` attributes supported for object members:

- `JsonPropertyNameAttribute`
- `JsonPropertyOrderAttribute`
- `JsonIgnoreAttribute` (`Always`, `WhenWritingNull`, `WhenWritingDefault`, `Never`)
- `JsonIncludeAttribute`
- `JsonRequiredAttribute`
- `JsonExtensionDataAttribute`
- `JsonConstructorAttribute`
- `JsonPolymorphicAttribute`
- `JsonDerivedTypeAttribute`

Member precedence:

1. YAML attributes
2. JSON attributes
3. Options policies

## Source Generation

SharpYaml provides an incremental source generator in `src/SharpYaml.SourceGenerator`.

Define contexts with `JsonSerializable` roots:

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonSerializable(typeof(MyModel))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

Use generated metadata:

```csharp
var context = new MyYamlContext();
var options = new YamlSerializerOptions { TypeInfoResolver = context };
var typeInfo = context.GetTypeInfo<MyModel>(options);
var yaml = YamlSerializer.Serialize(model, typeInfo);
var model2 = YamlSerializer.Deserialize(yaml, typeInfo);

var yamlViaProperty = YamlSerializer.Serialize(model, context.MyModel);
var modelViaProperty = YamlSerializer.Deserialize(yamlViaProperty, context.MyModel);

var yamlFromContext = YamlSerializer.Serialize(model, typeof(MyModel), MyYamlContext.Default);
var modelFromContext = (MyModel?)YamlSerializer.Deserialize(yamlFromContext, typeof(MyModel), MyYamlContext.Default);

var options = new YamlSerializerOptions
{
    TypeInfoResolver = MyYamlContext.Default
};
var yamlFromResolver = YamlSerializer.Serialize(model, options);
var modelFromResolver = YamlSerializer.Deserialize<MyModel>(yamlFromResolver, options);
```

Generated type info property naming follows Json-style patterns:

- Simple CLR types: `Boolean`, `Int32`, `WeatherForecast`
- Nullable: `NullableInt32`
- Arrays: `Int32Array`
- Closed generics: `ListInt32`, `DictionaryStringInt32`

## Removed Legacy Surface

SharpYaml 3 is a breaking release. Legacy object-mapping APIs were removed from the public surface, including:

- `SharpYaml.Serialization.Serializer`
- `SharpYaml.Serialization.SerializerSettings`
- `SharpYaml.Serialization.IYamlSerializable`
- Legacy serialization attributes such as `YamlMemberAttribute`, `YamlTagAttribute`, `YamlStyleAttribute`, `YamlRemapAttribute`
