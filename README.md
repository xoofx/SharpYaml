# SharpYaml [![ci](https://github.com/xoofx/SharpYaml/actions/workflows/ci.yml/badge.svg)](https://github.com/xoofx/SharpYaml/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/SharpYaml.svg)](https://www.nuget.org/packages/SharpYaml/)

<img align="right" width="256px" height="256px" src="https://raw.githubusercontent.com/xoofx/SharpYaml/master/img/SharpYaml.png">

SharpYaml is a high-performance .NET YAML parser, emitter, and object serializer - NativeAOT ready.

> **Note**: SharpYaml v3 is a major redesign with breaking changes from v2. It uses a **`System.Text.Json`-style API** with `YamlSerializer`, `YamlSerializerOptions`, and resolver-based metadata (`IYamlTypeInfoResolver`). See the [migration guide](https://xoofx.github.io/SharpYaml/migration) for details.

## ✨ Features

- **`System.Text.Json`-style API**: familiar surface with `YamlSerializer`, `YamlSerializerOptions`, `YamlTypeInfo<T>`
- **YAML 1.2 Core Schema**: spec-compliant parsing with configurable schema (Failsafe, JSON, Core, Extended)
- **Source generation**: NativeAOT / trimming friendly via `YamlSerializerContext` - reuses `[JsonSerializable]` attributes
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

Declare a context with `[JsonSerializable]` roots:

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonSerializable(typeof(MyConfig))]
internal partial class MyYamlContext : YamlSerializerContext { }
```

Then consume generated metadata:

```csharp
var context = MyYamlContext.Default;
var yaml = YamlSerializer.Serialize(config, context.MyConfig);
var roundTrip = YamlSerializer.Deserialize(yaml, context.MyConfig);
```

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

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 7950X 4.50GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v4
```

| Type                           | Method                                 | Categories                  | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------- |--------------------------------------- |---------------------------- |-----------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco             | Deserialize_Poco            | 1,565.4 us | 28.06 us | 26.25 us |  1.00 |    0.02 | 109.3750 |  68.3594 |        - | 1803.33 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Deserialize_Poco            | Deserialize_Poco            | 3,130.9 us | 49.63 us | 46.42 us |  2.00 |    0.04 | 250.0000 | 175.7813 |        - | 4135.78 KB |        2.29 |
|                                |                                        |                             |            |          |          |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_GenericDictionary  | Serialize_GenericDictionary |   225.2 us |  4.41 us |  4.13 us |  1.00 |    0.03 |  83.2520 |  83.2520 |  83.2520 |  277.02 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_GenericDictionary | Serialize_GenericDictionary | 2,680.6 us |  4.37 us |  3.65 us | 11.91 |    0.21 | 152.3438 | 148.4375 |  74.2188 | 2579.25 KB |        9.31 |
|                                |                                        |                             |            |          |          |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Serialize_Poco               | Serialize_Poco              |   252.0 us |  2.06 us |  1.72 us |  1.00 |    0.01 |  83.0078 |  83.0078 |  83.0078 |  292.92 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Serialize_Poco              | Serialize_Poco              | 3,396.3 us | 46.01 us | 43.04 us | 13.48 |    0.19 | 152.3438 | 148.4375 |  74.2188 | 2529.12 KB |        8.63 |
|                                |                                        |                             |            |          |          |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize    | Serialize_SourceGenerated   |   201.1 us |  3.27 us |  3.36 us |  1.00 |    0.02 |  83.2520 |  83.2520 |  83.2520 |  268.92 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Serialize   | Serialize_SourceGenerated   | 2,620.2 us | 20.60 us | 18.26 us | 13.03 |    0.23 | 152.3438 |  74.2188 |  74.2188 | 2404.09 KB |        8.94 |
|                                |                                        |                             |            |          |          |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_StringList         | Serialize_StringList        |   217.9 us |  2.46 us |  2.30 us |  1.00 |    0.01 |  99.8535 |  99.8535 |  99.8535 |  329.25 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_StringList        | Serialize_StringList        | 3,269.2 us | 13.52 us | 12.64 us | 15.01 |    0.16 | 218.7500 | 214.8438 | 109.3750 | 3019.75 KB |        9.17 |

## 📖 Documentation

Full documentation is available at https://xoofx.github.io/SharpYaml.

## 🪪 License

This software is released under the [MIT license](https://opensource.org/licenses/MIT).

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
