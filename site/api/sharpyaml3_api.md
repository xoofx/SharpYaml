# SharpYaml 3 API Reference (Draft)

## Overview

SharpYaml 3 exposes a `System.Text.Json`-style API centered on:

- `SharpYaml.YamlSerializer`
- `SharpYaml.YamlSerializerOptions`
- `SharpYaml.YamlTypeInfo` / `IYamlTypeInfoResolver`

The API is text-first (`string`, `TextReader`, `ReadOnlySpan<char>`) and is designed for both reflection-based and metadata-based serialization.

## Quick Start

```csharp
using SharpYaml;

var options = new YamlSerializerOptions
{
    PropertyNamingPolicy = YamlNamingPolicy.CamelCase,
    WriteIndented = true,
    Schema = YamlSchemaKind.Core
};

var yaml = YamlSerializer.Serialize(new { FirstName = "Ada", Age = 37 }, options);
var model = YamlSerializer.Deserialize<Person>(yaml, options);
```

## YamlSerializer

### Core overloads

- `Serialize<T>(T value, YamlSerializerOptions? options = null)`
- `Serialize(object? value, Type inputType, YamlSerializerOptions? options = null)`
- `Serialize<T>(TextWriter writer, T value, YamlSerializerOptions? options = null)`
- `Serialize(TextWriter writer, object? value, Type inputType, YamlSerializerOptions? options = null)`
- `Deserialize<T>(string yaml, YamlSerializerOptions? options = null)`
- `Deserialize(string yaml, Type returnType, YamlSerializerOptions? options = null)`
- `Deserialize<T>(TextReader reader, YamlSerializerOptions? options = null)`
- `Deserialize(TextReader reader, Type returnType, YamlSerializerOptions? options = null)`
- `Deserialize<T>(ReadOnlySpan<char> yaml, YamlSerializerOptions? options = null)`
- `Deserialize(ReadOnlySpan<char> yaml, Type returnType, YamlSerializerOptions? options = null)`

### Metadata overloads

- `Serialize<T>(T value, YamlTypeInfo<T> typeInfo)`
- `Deserialize<T>(string yaml, YamlTypeInfo<T> typeInfo)`
- `Deserialize<T>(ReadOnlySpan<char> yaml, YamlTypeInfo<T> typeInfo)`

### Reflection switch

`YamlSerializer.IsReflectionEnabledByDefault` follows the AppContext switch:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When reflection is disabled and no `TypeInfoResolver` is provided, calls throw `InvalidOperationException`.

## YamlSerializerOptions

`YamlSerializerOptions` currently provides:

- Naming:
- `PropertyNamingPolicy`
- `DictionaryKeyPolicy`
- `PropertyNameCaseInsensitive`
- Value handling:
- `DefaultIgnoreCondition`
- Formatting:
- `WriteIndented`
- `IndentSize`
- Mapping order:
- `MappingOrder` (`Declaration` by default, `Sorted` optional)
- YAML behavior:
- `Schema`
- `DuplicateKeyHandling`
- `ReferenceHandling`
- `ScalarStylePreferences`
- Polymorphism:
- `PolymorphismOptions`
- Metadata:
- `TypeInfoResolver`

## Attributes

YAML-specific attributes available in `SharpYaml.Serialization`:

- `YamlPropertyNameAttribute`
- `YamlPropertyOrderAttribute`
- `YamlIncludeAttribute`
- `YamlConstructorAttribute`
- `YamlPolymorphicAttribute`
- `YamlDerivedTypeAttribute`

## Metadata Model

Metadata primitives:

- `YamlTypeInfo`
- `YamlTypeInfo<T>`
- `IYamlTypeInfoResolver`
- `SharpYaml.Serialization.YamlSerializerContext`

These allow non-reflection metadata flows (source generation integration is added in later implementation steps).

