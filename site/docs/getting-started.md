---
title: Getting started
---

## Install

```sh
dotnet add package SharpYaml
```

SharpYaml targets `net10.0`.

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

SharpYaml reuses `System.Text.Json` source-generation attributes.

1. Declare a context with `[JsonSerializable]` roots:

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonSerializable(typeof(MyConfig))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

2. Use the generated `YamlTypeInfo<T>`:

```csharp
var context = MyYamlContext.Default;

var yaml = YamlSerializer.Serialize(config, context.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, context.MyConfig);
```

If you need to resolve metadata by type at runtime (for example `Serialize(object, Type, ...)`), configure options with a resolver:

```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolver = MyYamlContext.Default,
};

var yaml = YamlSerializer.Serialize(config, typeof(MyConfig), options);
```

## Next

- [Serialization overview](serialization/overview.md)
- [Low-level APIs](low-level/readme.md)

