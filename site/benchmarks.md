---
title: Benchmark suite
---

# SharpYaml Benchmark Suite

The benchmark suite lives in `src/SharpYaml.Benchmarks/` and targets `net10.0`.

In these benchmarks, SharpYaml is typically **~5x to ~29x faster** than YamlDotNet and allocates **~9x less memory**, depending on the scenario.

It compares SharpYaml with:

- `YamlDotNet` `18.1.0` (dynamic serializer/deserializer).
- `Vecc.YamlDotNet.Analyzers.StaticGenerator` `18.1.0` (static generator path).
- `ShapeShift.Yaml` `0.1.1068-alpha` (PolyType-based serializer), on the source-generated scenarios.

## Covered scenarios

- Serialization of a large generic document:
  - `Dictionary<string, object?>`
  - `List<string>`
- Serialization and deserialization of a large strongly typed object graph (200 services with their endpoints and labels, about 137,000 characters of YAML).
- Source-generated/static-context serialization and deserialization of the same object graph, to a `string` and to a `Stream`.
- Deserialization with unknown keys and with merge keys.

## Running

From the repository root:

```bash
cd src
dotnet run -c Release --project SharpYaml.Benchmarks -- --filter "*"
```

Run only source-generated scenarios:

```bash
cd src
dotnet run -c Release --project SharpYaml.Benchmarks -- --filter "*SourceGenerated*"
```

## Results

The rows of each of the three benchmark classes come from a run during which the machine did nothing else.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9711/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
```

| Type                           | Method                                              | Categories                            |         Mean |      Error |     StdDev | Ratio | RatioSD |     Gen0 |     Gen1 |     Gen2 |  Allocated | Alloc Ratio |
|------------------------------- |---------------------------------------------------- |-------------------------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco                          | Deserialize_Poco                      |   507.005 μs |  1.8612 μs |  1.7410 μs |  1.00 |    0.00 |  26.3672 |  13.6719 |        - |   444.3 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Deserialize_Poco                         | Deserialize_Poco                      | 2,740.417 μs | 17.2190 μs | 16.1067 μs |  5.41 |    0.04 | 242.1875 | 152.3438 |        - |    3968 KB |        8.93 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys              | Deserialize_Poco_Unmapped             |   516.357 μs |  3.0080 μs |  2.6666 μs |  1.00 |    0.01 |  26.3672 |  14.6484 |        - |  445.43 KB |        1.00 |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys_WithCallback | Deserialize_Poco_Unmapped             |   548.025 μs |  1.7805 μs |  1.6654 μs |  1.06 |    0.01 |  27.3438 |  12.6953 |        - |  460.34 KB |        1.03 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize               | Deserialize_SourceGenerated           |   467.391 μs |  1.1993 μs |  1.0631 μs |  1.00 |    0.00 |  26.3672 |  13.1836 |        - |  435.48 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Deserialize              | Deserialize_SourceGenerated           | 2,622.444 μs | 10.0974 μs |  8.4318 μs |  5.61 |    0.02 | 257.8125 |  93.7500 |        - |  4231.7 KB |        9.72 |
| SourceGeneratedBenchmarks      | ShapeShift_Deserialize                              | Deserialize_SourceGenerated           |   278.236 μs |  1.2081 μs |  1.1301 μs |  0.60 |    0.00 |  29.2969 |  17.0898 |        - |  480.26 KB |        1.10 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_MergeKeys     | Deserialize_SourceGenerated_MergeKeys |     1.503 μs |  0.0053 μs |  0.0050 μs |  1.00 |    0.00 |   0.2308 |   0.0019 |        - |    3.77 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_Stream        | Deserialize_SourceGenerated_Stream    |   517.845 μs |  2.0786 μs |  1.9443 μs |  1.00 |    0.01 | 102.5391 |  86.9141 |  76.1719 |  705.88 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_GenericDictionary               | Serialize_GenericDictionary           |     90.11 μs |   1.014 μs |   0.899 μs |  1.00 |    0.01 |  71.4111 |  71.0449 |  71.0449 |  273.99 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_GenericDictionary              | Serialize_GenericDictionary           |  2,209.75 μs |  12.956 μs |  12.119 μs | 24.53 |    0.27 | 152.3438 | 148.4375 |  74.2188 | 2579.29 KB |        9.41 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Serialize_Poco                            | Serialize_Poco                        |    94.456 μs |  1.0788 μs |  0.9008 μs |  1.00 |    0.01 |  72.6318 |  70.9229 |  70.9229 |   294.4 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Serialize_Poco                           | Serialize_Poco                        | 2,694.651 μs | 19.1434 μs | 17.9067 μs | 28.53 |    0.32 | 152.3438 | 148.4375 |  74.2188 | 2551.32 KB |        8.67 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize                 | Serialize_SourceGenerated             |    80.249 μs |  1.3664 μs |  1.2113 μs |  1.00 |    0.02 |  70.9229 |  70.9229 |  70.9229 |  268.11 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Serialize                | Serialize_SourceGenerated             | 2,048.364 μs | 33.5822 μs | 31.4128 μs | 25.53 |    0.53 | 195.3125 | 128.9063 |  66.4063 |  2404.2 KB |        8.97 |
| SourceGeneratedBenchmarks      | ShapeShift_Serialize                                | Serialize_SourceGenerated             |   124.213 μs |  2.3880 μs |  2.3453 μs |  1.55 |    0.04 |  97.1680 |  86.4258 |  77.3926 |  617.48 KB |        2.30 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize_Stream          | Serialize_SourceGenerated_Stream      |    79.741 μs |  0.2988 μs |  0.2495 μs |  1.00 |    0.00 |  38.6963 |  38.3301 |  38.3301 |  139.51 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_StringList                      | Serialize_StringList                  |     86.65 μs |   0.614 μs |   0.544 μs |  1.00 |    0.01 |  82.3975 |  82.3975 |  82.3975 |   329.2 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_StringList                     | Serialize_StringList                  |  2,491.78 μs |  11.727 μs |  10.970 μs | 28.76 |    0.21 | 218.7500 | 214.8438 | 109.3750 | 3019.78 KB |        9.17 |

## Reading the ShapeShift.Yaml numbers

`ShapeShift.Yaml` is not a general YAML parser: its decoder reads a line-based subset of YAML, one `key: value` or `- item` per line, without flow collections (`[a, b]`, `{a: b}`), block scalars (`|`, `>`), multi-line scalars, anchors and aliases, tags or several documents. It slices its scalars out of the input and has no token or event layer. Each library therefore deserializes the text it wrote itself, which has the same shape for both.

Its deserialization time is best read as a lower bound for this document rather than as the cost of parsing YAML: SharpYaml runs the full YAML 1.2 grammar, with its depth and alias limits, in about 1.7 times that time and with fewer allocations. On serialization, SharpYaml is about 1.5 times as fast and allocates less than half.

Under NativeAOT, where the JIT's profile-guided optimizations are not available, both libraries are slower. Measured on the same machine with the same object graph: SharpYaml serializes in about 180 us against 310 us for ShapeShift.Yaml, and deserializes in about 710 us against 390 us.