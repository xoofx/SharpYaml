---
title: YamlSerializer and options
---

SharpYaml provides a `System.Text.Json`-style serialization API.

## Basic usage

```csharp
using SharpYaml;

var yaml = YamlSerializer.Serialize(new { Name = "Ada" });
var model = YamlSerializer.Deserialize<Person>(yaml);
```

## Options

`YamlSerializerOptions` is immutable and can be cached and reused.

Common options:

- `PropertyNamingPolicy` and `DictionaryKeyPolicy` (`System.Text.Json.JsonNamingPolicy`)
- `WriteIndented` and `IndentSize`
- `DefaultIgnoreCondition`
- `PropertyNameCaseInsensitive`
- `ReferenceHandling` (anchors/aliases)

```csharp
using System.Text.Json;
using SharpYaml;

var options = new YamlSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
};
```

## Reflection vs metadata

SharpYaml can resolve serialization metadata in two ways:

1. **Generated metadata** using `YamlSerializerContext` and `YamlTypeInfo<T>` (recommended for NativeAOT).
2. **Reflection fallback** (enabled by default).

If you disable reflection (see below), you must provide metadata via `YamlTypeInfo<T>` or `YamlSerializerOptions.TypeInfoResolver`.

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

## Working with a context

You can use a generated context in three styles:

1. Use the generated `YamlTypeInfo<T>` property (recommended).

```csharp
var yaml = YamlSerializer.Serialize(value, MyYamlContext.Default.MyConfig);
```

2. Configure options with `TypeInfoResolver` (needed for APIs that take `Type`).

```csharp
var options = new YamlSerializerOptions { TypeInfoResolver = MyYamlContext.Default };
var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), options);
```

3. Resolve typed metadata explicitly.

```csharp
var options = new YamlSerializerOptions { TypeInfoResolver = MyYamlContext.Default };
var typeInfo = MyYamlContext.Default.GetTypeInfo<MyConfig>(options);
```

