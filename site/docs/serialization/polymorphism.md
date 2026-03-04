---
title: Polymorphism
---

SharpYaml supports polymorphism in a JSON-like way by default: using a discriminator property within a mapping.

It also supports YAML tags as an alternative discriminator style.

## JSON-like discriminator property

Use [`JsonPolymorphicAttribute`](xref:System.Text.Json.Serialization.JsonPolymorphicAttribute) and [`JsonDerivedTypeAttribute`](xref:System.Text.Json.Serialization.JsonDerivedTypeAttribute):

```csharp
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Dog), "dog")]
[JsonDerivedType(typeof(Cat), "cat")]
public abstract class Animal
{
    public string Name { get; set; } = "";
}
```

When serialized, a discriminator property is emitted:

```yaml
$type: dog
Name: Rex
```

## YAML tag discriminator

Use [`YamlPolymorphicAttribute`](xref:SharpYaml.Serialization.YamlPolymorphicAttribute) and [`YamlDerivedTypeAttribute`](xref:SharpYaml.Serialization.YamlDerivedTypeAttribute):

```csharp
using SharpYaml.Serialization;

[YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
[YamlDerivedType(typeof(Dog), "dog", Tag = "!dog")]
public abstract class TaggedAnimal
{
}
```

## Notes

- For safety, runtime activation from tag type names is disabled by default.
- When using tags, prefer an explicit registry ([`YamlDerivedTypeAttribute`](xref:SharpYaml.Serialization.YamlDerivedTypeAttribute) / [`JsonDerivedTypeAttribute`](xref:System.Text.Json.Serialization.JsonDerivedTypeAttribute)) over free-form type names.
