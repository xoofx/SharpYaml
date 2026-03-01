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

## Streams (UTF-8)

SharpYaml also provides `Stream` overloads (UTF-8) to avoid `StreamReader`/`StreamWriter` boilerplate:

```csharp
using System.IO;
using SharpYaml;

using var stream = File.OpenRead("config.yml");
var config = YamlSerializer.Deserialize<MyConfig>(stream);
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

## Naming policy defaults

By default, `YamlSerializerOptions.PropertyNamingPolicy` is `null`, meaning CLR member names are used as-is for YAML mapping keys.
This matches the default behavior of `System.Text.Json` (outside of ASP.NET defaults).

If you want camelCase keys, set `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

### Option reference

| Option | Default | Meaning |
| --- | --- | --- |
| `PropertyNamingPolicy` | `null` | Optional renaming for CLR member names. |
| `DictionaryKeyPolicy` | `null` | Optional renaming for dictionary keys during serialization. |
| `PropertyNameCaseInsensitive` | `false` | Case-insensitive property matching when reading. |
| `DefaultIgnoreCondition` | `Never` | Skips `null`/default values when writing. |
| `WriteIndented` | `true` | Enables indentation. |
| `IndentSize` | `2` | Spaces per indent level when `WriteIndented` is enabled. |
| `MappingOrder` | `Declaration` | Preserves declaration order by default (diff-friendly in code review). |
| `Schema` | `Core` | Controls scalar resolution rules (YAML 1.2). |
| `DuplicateKeyHandling` | `Error` | Controls behavior when duplicate keys are encountered. |
| `ReferenceHandling` | `None` | Enables anchor/alias preservation when needed. |
| `ScalarStylePreferences` | new | Controls scalar emission styles. |
| `PolymorphismOptions` | new | Controls polymorphism behaviors. |
| `UnsafeAllowDeserializeFromTagTypeName` | `false` | Allows tag-based activation by runtime type name (use only with trusted input). |
| `TypeInfoResolver` | `null` | Provides metadata (generated or custom) for reflection-free serialization. |
| `SourceName` | `null` | Used for error messages (file/path) when throwing `YamlException`. |

## Reflection vs metadata

SharpYaml can resolve serialization metadata in two ways:

1. **Generated metadata** using `YamlSerializerContext` and `YamlTypeInfo<T>` (recommended for NativeAOT).
2. **Reflection fallback** (enabled by default).

If you disable reflection (see below), POCO/object mapping requires metadata via `YamlTypeInfo<T>` or `YamlSerializerOptions.TypeInfoResolver`.
Built-in primitives and untyped containers remain supported without reflection.

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

## Working with a context

You can use a generated context in three styles:

1. Use the generated `YamlTypeInfo<T>` property (recommended).

```csharp
var yaml = YamlSerializer.Serialize(value, MyYamlContext.Default.MyConfig);
```

2. Use the overloads that accept a context (needed for APIs that take `Type`).

```csharp
var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), MyYamlContext.Default);
```

3. Use the context's options instance (when you need to call an `options`-based overload).

```csharp
var options = MyYamlContext.Default.Options;
var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), options);
```
