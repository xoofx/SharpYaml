---
title: Getting started
---

## Install

```sh
dotnet add package SharpYaml
```

SharpYaml targets `net8.0`, `net10.0`, and `netstandard2.0`.

SharpYaml supports .NET `net8.0` through `net10.0` with fully optimized implementations, and also ships a `netstandard2.0` build for broad runtime compatibility.

## Serialize and deserialize

```csharp
using SharpYaml;

var yaml = YamlSerializer.Serialize(new { Name = "Ada", Age = 37 });

var person = YamlSerializer.Deserialize<Person>(yaml);
```

## Configure options

```csharp
using System.Text.Json;
using SharpYaml;

var options = new YamlSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    IndentSize = 4,
    DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
};

var yaml = YamlSerializer.Serialize(config, options);
var config2 = YamlSerializer.Deserialize<MyConfig>(yaml, options);
```

## Source generation (NativeAOT friendly)

SharpYaml source generation uses [`YamlSerializableAttribute`](xref:SharpYaml.Serialization.YamlSerializableAttribute) to declare root types.

1. Declare a context with [`YamlSerializableAttribute`](xref:SharpYaml.Serialization.YamlSerializableAttribute) roots:

```csharp
using SharpYaml.Serialization;

[YamlSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.JsonKnownNamingPolicy.CamelCase)]
[YamlSerializable(typeof(MyConfig))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

2. Use the generated [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1):

```csharp
var context = MyYamlContext.Default;

var yaml = YamlSerializer.Serialize(config, context.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, context.MyConfig);
```

If you need to resolve metadata by type at runtime (for example `Serialize(object, Type, ...)`), prefer the overloads that accept a context:

```csharp
var yaml = YamlSerializer.Serialize(config, typeof(MyConfig), context);
```

## Next

- [Serialization overview](serialization/overview.md)
- [Low-level APIs](low-level/readme.md)
