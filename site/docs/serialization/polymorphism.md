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

## Default derived type

A derived type registered without a discriminator acts as the default when the discriminator property is missing or unrecognized. This works with both `JsonDerivedTypeAttribute` and `YamlDerivedTypeAttribute`:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Cat), "cat")]
[JsonDerivedType(typeof(OtherAnimal))]        // default -  no discriminator
public abstract class Animal
{
    public string Name { get; set; } = "";
}
```

```yaml
# Deserialized as Cat (discriminator matches)
type: cat
Name: Biscuit

# Deserialized as OtherAnimal (no discriminator → default)
Name: Cupcake
```

The equivalent using YAML attributes:

```csharp
[YamlPolymorphic]
[YamlDerivedType(typeof(Dog), "dog")]
[YamlDerivedType(typeof(OtherAnimal))]        // default -  no discriminator
public abstract class Animal { }
```

When serializing a default derived type, no discriminator property is emitted.

## Integer discriminators

Discriminator values can be integers instead of strings. Both `JsonDerivedTypeAttribute` and `YamlDerivedTypeAttribute` accept an `int` argument:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Dog), 1)]
[JsonDerivedType(typeof(Cat), 2)]
public abstract class Animal
{
    public string Name { get; set; } = "";
}
```

```csharp
[YamlPolymorphic]
[YamlDerivedType(typeof(Dog), 1)]
[YamlDerivedType(typeof(Cat), 2)]
public abstract class Animal
{
    public string Name { get; set; } = "";
}
```

The integer is emitted as a plain YAML scalar:

```yaml
$type: 1
Name: Rex
```

Integer discriminators are stored internally as strings (using invariant culture) so they work identically in both reflection mode and source-generated mode.

## Unknown discriminator handling

By default, an unrecognized discriminator value causes deserialization to throw. You can override this per-type via [`YamlPolymorphicAttribute`](xref:SharpYaml.Serialization.YamlPolymorphicAttribute) or globally via [`YamlPolymorphismOptions`](xref:SharpYaml.YamlPolymorphismOptions):

```csharp
// Per-type: fall back to the base type on unknown discriminators
[YamlPolymorphic(UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase)]
[YamlDerivedType(typeof(Circle), "circle")]
public class Shape { }
```

The `YamlPolymorphicAttribute.UnknownDerivedTypeHandling` property takes precedence over the corresponding `JsonPolymorphicAttribute` setting and the global `YamlPolymorphismOptions.UnknownDerivedTypeHandling`.

## Notes

- For safety, runtime activation from tag type names is disabled by default.
- When using tags, prefer an explicit registry ([`YamlDerivedTypeAttribute`](xref:SharpYaml.Serialization.YamlDerivedTypeAttribute) / [`JsonDerivedTypeAttribute`](xref:System.Text.Json.Serialization.JsonDerivedTypeAttribute)) over free-form type names.
