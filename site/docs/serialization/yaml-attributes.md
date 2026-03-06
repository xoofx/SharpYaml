---
title: SharpYaml attributes
---

SharpYaml provides YAML-focused attributes in `SharpYaml.Serialization`. These complement the supported `System.Text.Json` attributes.

## Attribute reference

| Attribute | Target | Reflection | Source generated | Purpose |
| --- | --- | --- | --- |
| [`YamlPropertyNameAttribute`](xref:SharpYaml.Serialization.YamlPropertyNameAttribute) | Member | Yes | Yes | Overrides the serialized property name. |
| [`YamlPropertyOrderAttribute`](xref:SharpYaml.Serialization.YamlPropertyOrderAttribute) | Member | Yes | Yes | Controls ordering within a mapping. |
| [`YamlIgnoreAttribute`](xref:SharpYaml.Serialization.YamlIgnoreAttribute) | Member | Yes | Yes | Ignores a member for serialization/deserialization. |
| [`YamlIncludeAttribute`](xref:SharpYaml.Serialization.YamlIncludeAttribute) | Member | Yes | Yes | Includes a non-public member when supported. |
| [`YamlRequiredAttribute`](xref:SharpYaml.Serialization.YamlRequiredAttribute) | Member | Yes | Yes | Missing required members throw [`YamlException`](xref:SharpYaml.YamlException). |
| [`YamlExtensionDataAttribute`](xref:SharpYaml.Serialization.YamlExtensionDataAttribute) | Member | Yes | Yes | Captures extra keys into a dictionary or [`YamlMapping`](xref:SharpYaml.Model.YamlMapping). |
| [`YamlConverterAttribute`](xref:SharpYaml.Serialization.YamlConverterAttribute) | Type/Member | Yes | Yes | Assigns a custom converter. |
| [`YamlConstructorAttribute`](xref:SharpYaml.Serialization.YamlConstructorAttribute) | Constructor | Yes | Yes | Selects which constructor to use for deserialization. Source generation supports constructors accessible to generated code (`public`, `internal`, `protected internal`). |
| [`YamlPolymorphicAttribute`](xref:SharpYaml.Serialization.YamlPolymorphicAttribute) | Type | Yes | Yes | Configures polymorphism (discriminator style). |
| [`YamlDerivedTypeAttribute`](xref:SharpYaml.Serialization.YamlDerivedTypeAttribute) | Type | Yes | Yes | Registers a derived type and optional tag/discriminator. |

## Precedence with JSON attributes

If you use both YAML and JSON attributes (for example [`YamlPropertyNameAttribute`](xref:SharpYaml.Serialization.YamlPropertyNameAttribute) and [`JsonPropertyNameAttribute`](xref:System.Text.Json.Serialization.JsonPropertyNameAttribute)), YAML attributes win.

Reflection-based serialization can also use non-public constructors marked with `[YamlConstructor]`.
