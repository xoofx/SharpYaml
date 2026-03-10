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

- `YamlMemberAttribute` -> [`YamlPropertyNameAttribute`](xref:SharpYaml.Serialization.YamlPropertyNameAttribute) and/or [`YamlPropertyOrderAttribute`](xref:SharpYaml.Serialization.YamlPropertyOrderAttribute)
- `YamlIgnoreAttribute` -> `YamlIgnoreAttribute` (kept)
- private-member inclusion via [`YamlIncludeAttribute`](xref:SharpYaml.Serialization.YamlIncludeAttribute) or [`JsonIncludeAttribute`](xref:System.Text.Json.Serialization.JsonIncludeAttribute)
- polymorphism:
- [`JsonPolymorphicAttribute`](xref:System.Text.Json.Serialization.JsonPolymorphicAttribute), [`JsonDerivedTypeAttribute`](xref:System.Text.Json.Serialization.JsonDerivedTypeAttribute)
- or [`YamlPolymorphicAttribute`](xref:SharpYaml.Serialization.YamlPolymorphicAttribute), [`YamlDerivedTypeAttribute`](xref:SharpYaml.Serialization.YamlDerivedTypeAttribute)

Removed legacy attributes:

- `YamlMemberAttribute`
- `YamlTagAttribute`
- `YamlStyleAttribute`
- `YamlRemapAttribute`

## JSON Attribute Compatibility

v3 supports key `System.Text.Json.Serialization` attributes directly for member mapping:

- [`JsonPropertyNameAttribute`](xref:System.Text.Json.Serialization.JsonPropertyNameAttribute)
- [`JsonPropertyOrderAttribute`](xref:System.Text.Json.Serialization.JsonPropertyOrderAttribute)
- [`JsonIgnoreAttribute`](xref:System.Text.Json.Serialization.JsonIgnoreAttribute)
- [`JsonIncludeAttribute`](xref:System.Text.Json.Serialization.JsonIncludeAttribute)

YAML-specific attributes take precedence when both are present.

## Reflection and Metadata

Reflection fallback is available by default and can be disabled:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When disabled, POCO/object mapping requires metadata via [`YamlSerializerOptions.TypeInfoResolver`](xref:SharpYaml.YamlSerializerOptions.TypeInfoResolver) (typically from a [`YamlSerializerContext`](xref:SharpYaml.Serialization.YamlSerializerContext)).
Built-in primitives and untyped containers remain supported without reflection.

## Source Generation

Create a context class and declare serializable roots with [`YamlSerializableAttribute`](xref:SharpYaml.Serialization.YamlSerializableAttribute):

```csharp
using SharpYaml.Serialization;

[YamlSerializable(typeof(MyType))]
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

- Use the syntax APIs (for example [`YamlSyntaxTree`](xref:SharpYaml.Syntax.YamlSyntaxTree)) for lossless roundtrip and source span tooling.
- [`YamlSerializer`](xref:SharpYaml.YamlSerializer) maps to .NET objects and does not preserve formatting/comments.

## SerializerSettings Migration

Most `SerializerSettings` switches from v2 were removed because v3 follows the `YamlSerializerOptions`/`YamlSerializerContext` model and aligns with `System.Text.Json` behavior where practical.

| v2 setting | v3 equivalent | Notes |
| --- | --- | --- |
| `NamingConvention = new CamelCaseNamingConvention()` | `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` | Use `System.Text.Json.JsonNamingPolicy` for CLR property names, and `DictionaryKeyPolicy` for dictionary keys. |
| `IgnoreUnmatchedProperties = true` | default behavior | SharpYaml v3 skips unmatched YAML members by default, matching `System.Text.Json`. |
| `IgnoreUnmatchedProperties = false` | `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` | Use the options-level setting or `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` on a specific type to fail on unknown members. |
| `EmitTags = false` | default behavior | v3 does not emit tags for ordinary object serialization unless they are required for polymorphism or explicitly written by a converter. |
| `ResetAlias = true` | default behavior | v3 does not preserve object identity unless `ReferenceHandling = YamlReferenceHandling.Preserve` is enabled. Serializer reuse does not carry aliases between operations. |

### Unmatched members and extension data

If a type has [`YamlExtensionDataAttribute`](xref:SharpYaml.Serialization.YamlExtensionDataAttribute) or [`JsonExtensionDataAttribute`](xref:System.Text.Json.Serialization.JsonExtensionDataAttribute), unmatched YAML members are captured there instead of being rejected.

```csharp
using SharpYaml;
using System.Text.Json.Serialization;

var options = new YamlSerializerOptions
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
};
```

```csharp
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MyModel
{
    public string Name { get; set; } = string.Empty;

    [YamlExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
```

In the example above, unknown YAML members are still stored in `Extra`; extension data takes precedence over `UnmappedMemberHandling`.
