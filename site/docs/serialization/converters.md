---
title: Converters
---

Converters allow custom serialization/deserialization for specific CLR types.

## Register a converter

You can register converters globally via options:

```csharp
var options = new YamlSerializerOptions
{
    Converters =
    [
        new MyConverter(),
    ],
};
```

## Attribute-based converters

Use [`YamlConverterAttribute`](xref:SharpYaml.Serialization.YamlConverterAttribute) on a type or member:

```csharp
using SharpYaml.Serialization;

[YamlConverter(typeof(MyTypeConverter))]
public sealed class MyType
{
}
```

## Converter shape

Converters operate on [`YamlReader`](xref:SharpYaml.Serialization.YamlReader) and [`YamlWriter`](xref:SharpYaml.Serialization.YamlWriter):

```csharp
public sealed class MyIntConverter : YamlConverter<int>
{
    public override int Read(YamlReader reader)
    {
        // Read and advance the reader.
        reader.Skip();
        return 123;
    }

    public override void Write(YamlWriter writer, int value)
        => writer.WriteScalar("123");
}
```

## Reader and writer basics

- [`YamlReader`](xref:SharpYaml.Serialization.YamlReader) is positioned on a token; converters must consume the current value and advance the reader.
- [`YamlWriter`](xref:SharpYaml.Serialization.YamlWriter) writes YAML in a streaming manner; converters should write a complete value (scalar/sequence/mapping).

For most custom scenarios, prefer writing scalars (`writer.WriteScalar(...)`) unless you need to emit complex YAML structures.
