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

In the included benchmarks, SharpYaml is typically **~5x to ~25x faster** than YamlDotNet and allocates **~7x to ~9x less memory**, depending on the scenario (POCO vs generic vs source-generated). See the [benchmark page](https://xoofx.github.io/SharpYaml/benchmarks/) for the scenarios and for the comparison with ShapeShift.Yaml.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9711/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
```

| Type                           | Method                                              | Categories                            | Mean         | Error      | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------- |---------------------------------------------------- |-------------------------------------- |-------------:|-----------:|-----------:|-------------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco                          | Deserialize_Poco                      |   575.891 μs |  7.4058 μs |  6.1842 μs |   572.542 μs |  1.00 |    0.01 |  32.2266 |  15.6250 |        - |  526.73 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Deserialize_Poco                         | Deserialize_Poco                      | 2,814.069 μs | 40.4824 μs | 35.8866 μs | 2,797.839 μs |  4.89 |    0.08 | 242.1875 | 152.3438 |        - | 3967.78 KB |        7.53 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys              | Deserialize_Poco_Unmapped             |   609.725 μs |  8.0721 μs |  7.1557 μs |   608.133 μs |  1.00 |    0.02 |  32.2266 |  19.5313 |        - |  527.94 KB |        1.00 |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys_WithCallback | Deserialize_Poco_Unmapped             |   597.764 μs |  6.0313 μs |  5.0364 μs |   596.347 μs |  0.98 |    0.01 |  32.2266 |  19.5313 |        - |  530.12 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize               | Deserialize_SourceGenerated           |   521.404 μs |  2.4262 μs |  2.1508 μs |   521.208 μs |  1.00 |    0.01 |  31.2500 |  17.0898 |        - |  517.51 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Deserialize              | Deserialize_SourceGenerated           | 2,636.226 μs | 21.9571 μs | 18.3352 μs | 2,632.869 μs |  5.06 |    0.04 | 246.0938 | 160.1563 |        - | 4030.26 KB |        7.79 |
| SourceGeneratedBenchmarks      | ShapeShift_Deserialize                              | Deserialize_SourceGenerated           |   292.813 μs |  1.4817 μs |  1.1568 μs |   292.657 μs |  0.56 |    0.00 |  29.2969 |  17.0898 |        - |  480.26 KB |        0.93 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_MergeKeys     | Deserialize_SourceGenerated_MergeKeys |     1.623 μs |  0.0119 μs |  0.0100 μs |     1.626 μs |  1.00 |    0.01 |   0.2880 |   0.0038 |        - |    4.73 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_Stream        | Deserialize_SourceGenerated_Stream    |   584.814 μs | 11.3160 μs | 11.1138 μs |   579.469 μs |  1.00 |    0.03 | 106.4453 |  90.8203 |  75.1953 |  787.95 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_GenericDictionary               | Serialize_GenericDictionary           |   102.641 μs |  1.2638 μs |  1.0553 μs |   102.393 μs |  1.00 |    0.01 |  71.4111 |  71.0449 |  71.0449 |     274 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_GenericDictionary              | Serialize_GenericDictionary           | 2,321.592 μs | 38.3120 μs | 33.9625 μs | 2,315.858 μs | 22.62 |    0.39 | 152.3438 | 148.4375 |  74.2188 | 2579.25 KB |        9.41 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Serialize_Poco                            | Serialize_Poco                        |   111.059 μs |  2.2183 μs |  2.6408 μs |   111.132 μs |  1.00 |    0.03 |  72.8760 |  71.1670 |  71.1670 |  294.36 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Serialize_Poco                           | Serialize_Poco                        | 2,882.347 μs | 18.9760 μs | 14.8152 μs | 2,883.020 μs | 25.97 |    0.61 | 152.3438 | 148.4375 |  74.2188 | 2551.29 KB |        8.67 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize                 | Serialize_SourceGenerated             |    88.165 μs |  1.6690 μs |  1.7858 μs |    88.065 μs |  1.00 |    0.03 |  71.1670 |  71.1670 |  71.1670 |  268.12 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Serialize                | Serialize_SourceGenerated             | 2,231.336 μs | 36.6335 μs | 32.4747 μs | 2,231.815 μs | 25.32 |    0.61 | 195.3125 | 132.8125 |  66.4063 | 2404.17 KB |        8.97 |
| SourceGeneratedBenchmarks      | ShapeShift_Serialize                                | Serialize_SourceGenerated             |   175.785 μs |  3.8823 μs | 11.2013 μs |   180.285 μs |  1.99 |    0.13 |  90.8203 |  90.8203 |  90.8203 |  617.01 KB |        2.30 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize_Stream          | Serialize_SourceGenerated_Stream      |    86.142 μs |  1.7034 μs |  1.5101 μs |    85.911 μs |  1.00 |    0.02 |  39.0625 |  38.6963 |  38.6963 |   139.5 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |              |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_StringList                      | Serialize_StringList                  |   186.646 μs |  3.6106 μs |  3.5460 μs |   186.899 μs |  1.00 |    0.03 |  88.6230 |  88.6230 |  88.6230 |  329.23 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_StringList                     | Serialize_StringList                  | 2,671.553 μs | 36.5366 μs | 32.3888 μs | 2,669.281 μs | 14.32 |    0.31 | 218.7500 | 214.8438 | 109.3750 | 3019.75 KB |        9.17 |


## 📖 Documentation

Full documentation is available at https://xoofx.github.io/SharpYaml.

## 🪪 License

This software is released under the [MIT license](https://opensource.org/licenses/MIT).

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
