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

Use `YamlConverterAttribute` on a type or member:

```csharp
using SharpYaml.Serialization;

[YamlConverter(typeof(MyTypeConverter))]
public sealed class MyType
{
}
```

## Converter shape

Converters operate on `YamlReader` and `YamlWriter`:

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

