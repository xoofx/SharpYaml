---
title: YamlSerializer and options
---

SharpYaml provides a [`JsonSerializer`](xref:System.Text.Json.JsonSerializer)-style serialization API.

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

## Buffer writers (zero-copy)

If you want to avoid `string` allocations when emitting YAML, you can write directly to an `IBufferWriter<char>`:

```csharp
using System.Buffers;
using SharpYaml;

var buffer = new ArrayBufferWriter<char>();
YamlSerializer.Serialize(buffer, new { Name = "Ada" });

var yaml = new string(buffer.WrittenSpan);
```

## Options

[`YamlSerializerOptions`](xref:SharpYaml.YamlSerializerOptions) is immutable and can be cached and reused.

Common options:

- [`PropertyNamingPolicy`](xref:SharpYaml.YamlSerializerOptions.PropertyNamingPolicy) and [`DictionaryKeyPolicy`](xref:SharpYaml.YamlSerializerOptions.DictionaryKeyPolicy) ([`JsonNamingPolicy`](xref:System.Text.Json.JsonNamingPolicy))
- [`WriteIndented`](xref:SharpYaml.YamlSerializerOptions.WriteIndented) and [`IndentSize`](xref:SharpYaml.YamlSerializerOptions.IndentSize)
- [`DefaultIgnoreCondition`](xref:SharpYaml.YamlSerializerOptions.DefaultIgnoreCondition)
- [`PropertyNameCaseInsensitive`](xref:SharpYaml.YamlSerializerOptions.PropertyNameCaseInsensitive)
- [`ReferenceHandling`](xref:SharpYaml.YamlSerializerOptions.ReferenceHandling) (anchors/aliases)

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

By default, [`YamlSerializerOptions.PropertyNamingPolicy`](xref:SharpYaml.YamlSerializerOptions.PropertyNamingPolicy) is `null`, meaning CLR member names are used as-is for YAML mapping keys.
This matches the default behavior of [`JsonSerializer`](xref:System.Text.Json.JsonSerializer) (outside of ASP.NET defaults).

If you want camelCase keys, set `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

### Option reference

| Option | Default | Meaning |
| --- | --- | --- |
| [`PropertyNamingPolicy`](xref:SharpYaml.YamlSerializerOptions.PropertyNamingPolicy) | `null` | Optional renaming for CLR member names. |
| [`DictionaryKeyPolicy`](xref:SharpYaml.YamlSerializerOptions.DictionaryKeyPolicy) | `null` | Optional renaming for dictionary keys during serialization. |
| [`PropertyNameCaseInsensitive`](xref:SharpYaml.YamlSerializerOptions.PropertyNameCaseInsensitive) | `false` | Case-insensitive property matching when reading. |
| [`DefaultIgnoreCondition`](xref:SharpYaml.YamlSerializerOptions.DefaultIgnoreCondition) | [`YamlIgnoreCondition.Never`](xref:SharpYaml.YamlIgnoreCondition.Never) | Skips `null`/default values when writing. |
| [`WriteIndented`](xref:SharpYaml.YamlSerializerOptions.WriteIndented) | `true` | Enables indentation. |
| [`IndentSize`](xref:SharpYaml.YamlSerializerOptions.IndentSize) | `2` | Spaces per indent level when `WriteIndented` is enabled. |
| [`MappingOrder`](xref:SharpYaml.YamlSerializerOptions.MappingOrder) | [`YamlMappingOrderPolicy.Declaration`](xref:SharpYaml.YamlMappingOrderPolicy.Declaration) | Preserves declaration order by default (diff-friendly in code review). |
| [`Schema`](xref:SharpYaml.YamlSerializerOptions.Schema) | [`YamlSchemaKind.Core`](xref:SharpYaml.YamlSchemaKind.Core) | Controls scalar resolution rules (YAML 1.2). |
| [`DuplicateKeyHandling`](xref:SharpYaml.YamlSerializerOptions.DuplicateKeyHandling) | [`YamlDuplicateKeyHandling.Error`](xref:SharpYaml.YamlDuplicateKeyHandling.Error) | Controls behavior when duplicate keys are encountered. |
| [`ReferenceHandling`](xref:SharpYaml.YamlSerializerOptions.ReferenceHandling) | [`YamlReferenceHandling.None`](xref:SharpYaml.YamlReferenceHandling.None) | Enables anchor/alias preservation when needed. |
| [`ScalarStylePreferences`](xref:SharpYaml.YamlSerializerOptions.ScalarStylePreferences) | new | Controls scalar emission styles. |
| [`PolymorphismOptions`](xref:SharpYaml.YamlSerializerOptions.PolymorphismOptions) | new | Controls polymorphism behaviors. |
| [`UnsafeAllowDeserializeFromTagTypeName`](xref:SharpYaml.YamlSerializerOptions.UnsafeAllowDeserializeFromTagTypeName) | `false` | Allows tag-based activation by runtime type name (use only with trusted input). |
| [`TypeInfoResolver`](xref:SharpYaml.YamlSerializerOptions.TypeInfoResolver) | `null` | Provides metadata (generated or custom) for reflection-free serialization. |
| [`SourceName`](xref:SharpYaml.YamlSerializerOptions.SourceName) | `null` | Used for error messages (file/path) when throwing [`YamlException`](xref:SharpYaml.YamlException). |

## Reflection vs metadata

SharpYaml can resolve serialization metadata in two ways:

1. **Generated metadata** using [`YamlSerializerContext`](xref:SharpYaml.Serialization.YamlSerializerContext) and [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) (recommended for NativeAOT).
2. **Reflection fallback** (enabled by default).

If you disable reflection (see below), POCO/object mapping requires metadata via [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) or [`YamlSerializerOptions.TypeInfoResolver`](xref:SharpYaml.YamlSerializerOptions.TypeInfoResolver).
Built-in primitives and untyped containers remain supported without reflection.

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

## Working with a context

You can use a generated context in three styles:

1. Use the generated [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) property (recommended).

```csharp
var yaml = YamlSerializer.Serialize(value, MyYamlContext.Default.MyConfig);
```

2. Use the overloads that accept a context (needed for APIs that take `Type`).

```csharp
var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), MyYamlContext.Default);
```

Prefer the overloads that accept a [`YamlSerializerContext`](xref:SharpYaml.Serialization.YamlSerializerContext) or a [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) directly to avoid reflection and reduce configuration overhead.
