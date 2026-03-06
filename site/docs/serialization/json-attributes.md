---
title: JSON attribute interop
---

SharpYaml supports a subset of `System.Text.Json.Serialization` attributes to make it easy to reuse models across JSON and YAML.

## Supported JSON attributes

Member-level attributes:

| Attribute | Reflection | Source generated | Notes |
| --- | --- | --- |
| [`JsonPropertyNameAttribute`](xref:System.Text.Json.Serialization.JsonPropertyNameAttribute) | Yes | Yes | Overrides the serialized name. |
| [`JsonIgnoreAttribute`](xref:System.Text.Json.Serialization.JsonIgnoreAttribute) | Yes | Yes | Supports `Always`, `Never`, `WhenWritingNull`, `WhenWritingDefault`. |
| [`JsonIncludeAttribute`](xref:System.Text.Json.Serialization.JsonIncludeAttribute) | Yes | Yes | Enables non-public members in supported scenarios. |
| [`JsonPropertyOrderAttribute`](xref:System.Text.Json.Serialization.JsonPropertyOrderAttribute) | Yes | Yes | Controls member ordering within mappings. |
| [`JsonRequiredAttribute`](xref:System.Text.Json.Serialization.JsonRequiredAttribute) | Yes | Yes | Missing required members throw [`YamlException`](xref:SharpYaml.YamlException). |
| [`JsonExtensionDataAttribute`](xref:System.Text.Json.Serialization.JsonExtensionDataAttribute) | Yes | Yes | Supports dictionary- and mapping-based extension data. |

Type-level attributes:

| Attribute | Reflection | Source generated | Notes |
| --- | --- | --- |
| [`JsonConstructorAttribute`](xref:System.Text.Json.Serialization.JsonConstructorAttribute) | Yes | Yes | Selects which constructor to use for deserialization. Source generation supports constructors accessible to generated code (`public`, `internal`, `protected internal`). |
| [`JsonPolymorphicAttribute`](xref:System.Text.Json.Serialization.JsonPolymorphicAttribute) | Yes | Yes | Supported via discriminator properties (JSON-like). |
| [`JsonDerivedTypeAttribute`](xref:System.Text.Json.Serialization.JsonDerivedTypeAttribute) | Yes | Yes | Registers derived types and discriminators. |

## Unsupported JSON attributes

The following JSON attributes are not currently supported by SharpYaml:

| Attribute | Alternative |
| --- | --- |
| [`JsonConverterAttribute`](xref:System.Text.Json.Serialization.JsonConverterAttribute) | Use [`YamlConverterAttribute`](xref:SharpYaml.Serialization.YamlConverterAttribute) or register converters via options. |
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

Reflection-based serialization can also use non-public constructors marked with `[JsonConstructor]`.
