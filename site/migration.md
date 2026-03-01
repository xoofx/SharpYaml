---
title: Migration (v2 to v3)
---

# SharpYaml v2 to v3 Migration Guide

SharpYaml 3 is a breaking-change release aligned with modern .NET serialization patterns.

## Target Framework

- v3 targets `net8.0`, `net10.0`, and `netstandard2.0`.

## Main API Replacement

Old:

```csharp
var serializer = new Serializer(new SerializerSettings());
var yaml = serializer.Serialize(value);
var model = serializer.Deserialize<MyType>(yaml);
```

New:

```csharp
var options = new YamlSerializerOptions();
var yaml = YamlSerializer.Serialize(value, options);
var model = YamlSerializer.Deserialize<MyType>(yaml, options);
```

## Removed Public APIs

The following legacy APIs are no longer public in v3:

- `Serializer`
- `SerializerSettings`
- `SerializerContext`
- `IYamlSerializable` / `IYamlSerializableFactory`
- Legacy descriptor/object-factory contracts and serializer backend types

## Attribute Migration

Use these replacements:

- `YamlMemberAttribute` -> `YamlPropertyNameAttribute` and/or `YamlPropertyOrderAttribute`
- `YamlIgnoreAttribute` -> `YamlIgnoreAttribute` (kept)
- private-member inclusion via `YamlIncludeAttribute` or `JsonIncludeAttribute`
- polymorphism:
- `JsonPolymorphicAttribute`, `JsonDerivedTypeAttribute`
- or `YamlPolymorphicAttribute`, `YamlDerivedTypeAttribute`

Removed legacy attributes:

- `YamlMemberAttribute`
- `YamlTagAttribute`
- `YamlStyleAttribute`
- `YamlRemapAttribute`

## JSON Attribute Compatibility

v3 supports key `System.Text.Json.Serialization` attributes directly for member mapping:

- `JsonPropertyName`
- `JsonPropertyOrder`
- `JsonIgnore`
- `JsonInclude`

YAML-specific attributes take precedence when both are present.

## Reflection and Metadata

Reflection fallback is available by default and can be disabled:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When disabled, POCO/object mapping requires metadata via `YamlSerializerOptions.TypeInfoResolver` (typically from a `YamlSerializerContext`).
Built-in primitives and untyped containers remain supported without reflection.

## Source Generation

Create a context class and declare serializable roots with `JsonSerializable`:

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonSerializable(typeof(MyType))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

Then consume typed metadata:

```csharp
var context = MyYamlContext.Default;
var yaml = YamlSerializer.Serialize(value, context.MyType);
var model = YamlSerializer.Deserialize(yaml, context.MyType);
```

## Low-Level Roundtrip vs Object Mapping

- Use `SharpYaml.Syntax` for lossless roundtrip and source span tooling.
- `YamlSerializer` maps to .NET objects and does not preserve formatting/comments.
