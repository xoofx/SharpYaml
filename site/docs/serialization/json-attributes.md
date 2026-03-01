---
title: JSON attribute interop
---

SharpYaml supports a subset of `System.Text.Json.Serialization` attributes to make it easy to reuse models across JSON and YAML.

## Supported JSON attributes

Member-level attributes:

| Attribute | Supported | Notes |
| --- | --- | --- |
| `JsonPropertyNameAttribute` | Yes | Overrides the serialized name. |
| `JsonIgnoreAttribute` | Yes | Supports `Always`, `Never`, `WhenWritingNull`, `WhenWritingDefault`. |
| `JsonIncludeAttribute` | Yes | Enables non-public members in supported scenarios. |
| `JsonPropertyOrderAttribute` | Yes | Controls member ordering within mappings. |
| `JsonRequiredAttribute` | Yes | Missing required members throw `YamlException`. |
| `JsonExtensionDataAttribute` | Yes | Supports dictionary- and mapping-based extension data. |

Type-level attributes:

| Attribute | Supported | Notes |
| --- | --- | --- |
| `JsonConstructorAttribute` | Yes | Selects which constructor to use for deserialization. |
| `JsonPolymorphicAttribute` | Yes | Supported via property discriminators (JSON-like). |
| `JsonDerivedTypeAttribute` | Yes | Registers derived types and discriminators. |

## Unsupported JSON attributes

The following JSON attributes are not currently supported by SharpYaml:

| Attribute | Alternative |
| --- | --- |
| `JsonConverterAttribute` | Use `YamlConverterAttribute` or register converters via options. |
| Converter-specific attributes (number handling, etc.) | Use converters. |

## Precedence rules

When both YAML and JSON attributes are present, YAML-specific attributes take precedence.

## Examples

### Property naming

```csharp
using System.Text.Json.Serialization;

public sealed class Person
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = "";
}
```

### Constructor selection

```csharp
using System.Text.Json.Serialization;

public sealed class Endpoint
{
    public string Url { get; }

    [JsonConstructor]
    public Endpoint(string url) => Url = url;
}
```

