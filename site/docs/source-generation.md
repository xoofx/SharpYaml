---
title: Source generation and NativeAOT
---

SharpYaml supports an incremental source generator for high-performance, NativeAOT-friendly serialization.

## When to use source generation

Use source generation when:

- You publish with `PublishAot=true`
- You trim aggressively (`PublishTrimmed=true`)
- You want to avoid reflection and reduce startup overhead

## Define a context

SharpYaml reuses `System.Text.Json.Serialization` generation attributes.

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonSerializable(typeof(MyConfig))]
[JsonSerializable(typeof(List<int>))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

The context type must be `partial` so the generator can add metadata properties.

## Use generated metadata

Use the generated `YamlTypeInfo<T>` properties (recommended):

```csharp
var yaml = YamlSerializer.Serialize(value, MyYamlContext.Default.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, MyYamlContext.Default.MyConfig);
```

For APIs that take a `Type`, configure options with a resolver:

```csharp
var options = new YamlSerializerOptions
{
    TypeInfoResolver = MyYamlContext.Default,
};

var yaml = YamlSerializer.Serialize(value, typeof(MyConfig), options);
```

## Reflection control

Reflection fallback can be disabled globally before first serializer use:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When reflection is disabled, you must provide metadata via `YamlTypeInfo<T>` or `YamlSerializerOptions.TypeInfoResolver`.

## Troubleshooting

- If generated properties are missing, ensure the project references the `SharpYaml` NuGet package (the generator is shipped in-package under `analyzers/dotnet/cs`).
- Ensure the context class is `partial`.
- Ensure roots are declared via `[JsonSerializable(typeof(...))]`.
