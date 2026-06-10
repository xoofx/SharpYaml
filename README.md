# SharpYaml [![ci](https://github.com/xoofx/SharpYaml/actions/workflows/ci.yml/badge.svg)](https://github.com/xoofx/SharpYaml/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/SharpYaml.svg)](https://www.nuget.org/packages/SharpYaml/)

<img align="right" width="256px" height="256px" src="https://raw.githubusercontent.com/xoofx/SharpYaml/master/img/SharpYaml.png">

SharpYaml is a high-performance .NET YAML parser, emitter, and object serializer - NativeAOT ready.

> **Note**: SharpYaml v3 is a major redesign with breaking changes from v2. It uses a **`System.Text.Json`-style API** with `YamlSerializer`, `YamlSerializerOptions`, and resolver-based metadata (`IYamlTypeInfoResolver`). See the [migration guide](https://xoofx.github.io/SharpYaml/migration) for details.

## ✨ Features

- **`System.Text.Json`-style API**: familiar surface with `YamlSerializer`, `YamlSerializerOptions`, `YamlTypeInfo<T>`
- **YAML 1.2 Core Schema**: spec-compliant parsing with configurable schema (Failsafe, JSON, Core, Extended)
- **Source generation**: NativeAOT / trimming friendly via `YamlSerializerContext` with `[YamlSerializable]` roots
- **`System.Text.Json` attribute interop**: reuse `[JsonPropertyName]`, `[JsonIgnore]`, `[JsonPropertyOrder]`, `[JsonConstructor]`
- **Flexible I/O**: serialize/deserialize from `string`, `ReadOnlySpan<char>`, `TextReader`, `TextWriter`
- **Rich options**: naming policies, indent control, null handling, duplicate key behavior, reference handling, polymorphism
- **Low-level access**: full scanner, parser, emitter, and syntax tree APIs for advanced YAML processing
- **NativeAOT and trimming oriented** (`IsAotCompatible`, `IsTrimmable`)

## 📐 Requirements

SharpYaml targets `net8.0`, `net10.0`, and `netstandard2.0`.

- Consuming the NuGet package works on any runtime that supports `netstandard2.0` (including .NET Framework) or modern .NET (`net8.0+`).
- Building SharpYaml from source requires the .NET 10 SDK (C# 14).

## 📦 Install

```sh
dotnet add package SharpYaml
```

SharpYaml ships the source generator in-package (`analyzers/dotnet/cs`) - no extra package needed.

## 🚀 Quick Start

```csharp
using SharpYaml;

// Serialize
var yaml = YamlSerializer.Serialize(new { Name = "Ada", Age = 37 });

// Deserialize
var person = YamlSerializer.Deserialize<Person>(yaml);
```

### Options

```csharp
var options = new YamlSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    IndentSize = 4,
    DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
};

var yaml = YamlSerializer.Serialize(config, options);
var model = YamlSerializer.Deserialize<MyConfig>(yaml, options);
```

By default, `PropertyNamingPolicy` is `null`, meaning CLR member names are used as-is for YAML mapping keys (same default as `System.Text.Json`).

### Source Generation

Declare a context with `[YamlSerializable]` roots:

```csharp
using SharpYaml.Serialization;

[YamlSerializable(typeof(MyConfig))]
internal partial class MyYamlContext : YamlSerializerContext { }
```

Then consume generated metadata:

```csharp
var context = MyYamlContext.Default;
var yaml = YamlSerializer.Serialize(config, context.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, context.MyConfig);
```

### Cross-Project Polymorphism

When a base type lives in one assembly and the derived types live in another, you can register mappings where the composition root already references both sides.

Reflection-based serialization can register mappings at runtime:

```csharp
var options = new YamlSerializerOptions
{
    PolymorphismOptions = new YamlPolymorphismOptions
    {
        DerivedTypeMappings =
        {
            [typeof(Animal)] =
            [
                new YamlDerivedType(typeof(Dog), "dog") { Tag = "!dog" },
                new YamlDerivedType(typeof(Cat), "cat") { Tag = "!cat" },
            ]
        }
    }
};
```

Source-generated contexts can register the same relationship at compile time:

```csharp
[YamlSerializable(typeof(Zoo))]
[YamlDerivedTypeMapping(typeof(Animal), typeof(Dog), "dog", Tag = "!dog")]
[YamlDerivedTypeMapping(typeof(Animal), typeof(Cat), "cat", Tag = "!cat")]
internal partial class ZooYamlContext : YamlSerializerContext { }
```

Context-level mappings are additive to `[YamlDerivedType]` and `JsonDerivedType` attributes. Attribute-based entries win when the same derived type or discriminator is registered more than once.

### Reflection Control

Reflection fallback can be disabled globally before first serializer use:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When reflection is disabled, POCO/object mapping requires metadata (use generated `YamlTypeInfo<T>`, the overloads that accept a `YamlSerializerContext`, or pass `MyYamlContext.Default.Options` to an `options`-based overload). Primitive scalars and untyped containers (`object`, `Dictionary<string, object>`, `List<object>`, `object[]`) still work without reflection.

When publishing with NativeAOT (`PublishAot=true`), the SharpYaml NuGet package disables reflection-based serialization by default.
You can override the default by setting the following MSBuild property in your app project:

```xml
<PropertyGroup>
  <SharpYamlIsReflectionEnabledByDefault>true</SharpYamlIsReflectionEnabledByDefault>
</PropertyGroup>
```

## 🚀 Benchmarks

In the included benchmarks, SharpYaml is typically **~2x to ~6x faster** than YamlDotNet and uses **~2x to ~9x less memory allocations**, depending on the scenario (POCO vs generic vs source-generated).

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4


```

| Type                           | Method                                 | Categories                  | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------- |--------------------------------------- |---------------------------- |-----------:|---------:|---------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco             | Deserialize_Poco            | 1,341.5 μs |  3.07 μs |  2.56 μs | 1,342.5 μs |  1.00 |    0.00 | 119.1406 |  70.3125 |        - | 1947.94 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Deserialize_Poco            | Deserialize_Poco            | 2,764.9 μs | 42.75 μs | 35.70 μs | 2,765.6 μs |  2.06 |    0.03 | 250.0000 | 218.7500 |        - | 4135.91 KB |        2.12 |
|                                |                                        |                             |            |          |          |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_GenericDictionary  | Serialize_GenericDictionary |   326.7 μs |  6.45 μs | 11.95 μs |   318.7 μs |  1.00 |    0.05 |  83.0078 |  83.0078 |  83.0078 |  303.42 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_GenericDictionary | Serialize_GenericDictionary | 2,062.5 μs |  4.53 μs |  3.54 μs | 2,062.0 μs |  6.32 |    0.23 | 152.3438 | 148.4375 |  74.2188 | 2579.25 KB |        8.50 |
|                                |                                        |                             |            |          |          |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Serialize_Poco               | Serialize_Poco              |   354.5 μs |  1.01 μs |  0.84 μs |   354.3 μs |  1.00 |    0.00 |  83.0078 |  83.0078 |  83.0078 |  330.99 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Serialize_Poco              | Serialize_Poco              | 2,608.8 μs | 35.76 μs | 31.70 μs | 2,608.6 μs |  7.36 |    0.09 | 125.0000 |  62.5000 |  62.5000 | 2529.24 KB |        7.64 |
|                                |                                        |                             |            |          |          |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize    | Serialize_SourceGenerated   |   296.2 μs |  0.67 μs |  0.60 μs |   296.4 μs |  1.00 |    0.00 |  83.0078 |  83.0078 |  83.0078 |  295.37 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Serialize   | Serialize_SourceGenerated   | 2,120.9 μs |  5.14 μs |  4.29 μs | 2,121.4 μs |  7.16 |    0.02 | 152.3438 |  74.2188 |  74.2188 | 2404.09 KB |        8.14 |
|                                |                                        |                             |            |          |          |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_StringList         | Serialize_StringList        |   433.8 μs |  0.98 μs |  0.82 μs |   433.9 μs |  1.00 |    0.00 |  99.6094 |  99.6094 |  99.6094 |  329.25 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_StringList        | Serialize_StringList        | 2,637.8 μs | 10.95 μs |  9.14 μs | 2,638.3 μs |  6.08 |    0.02 | 218.7500 | 214.8438 | 109.3750 | 3019.75 KB |        9.17 |


## 📖 Documentation

Full documentation is available at https://xoofx.github.io/SharpYaml.

## 🪪 License

This software is released under the [MIT license](https://opensource.org/licenses/MIT).

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
