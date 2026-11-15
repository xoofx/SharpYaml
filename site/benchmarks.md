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

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9711/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
```

| Type                           | Method                                              | Categories                            | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------- |---------------------------------------------------- |-------------------------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco                          | Deserialize_Poco                      |   505.536 μs |  2.4498 μs |  2.2916 μs |  1.00 |    0.01 |  26.3672 |  13.6719 |        - |  444.29 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Deserialize_Poco                         | Deserialize_Poco                      | 2,763.339 μs | 10.4740 μs |  9.2850 μs |  5.47 |    0.03 | 253.9063 | 164.0625 |        - |  4169.3 KB |        9.38 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys              | Deserialize_Poco_Unmapped             |   505.401 μs |  1.7912 μs |  1.5879 μs |  1.00 |    0.00 |  26.3672 |  14.6484 |        - |  445.42 KB |        1.00 |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys_WithCallback | Deserialize_Poco_Unmapped             |   544.490 μs |  2.9702 μs |  2.7784 μs |  1.08 |    0.01 |  27.3438 |  12.6953 |        - |  460.33 KB |        1.03 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize               | Deserialize_SourceGenerated           |   468.473 μs |  1.2262 μs |  1.0870 μs |  1.00 |    0.00 |  26.3672 |  13.1836 |        - |  435.47 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Deserialize              | Deserialize_SourceGenerated           | 2,664.425 μs | 17.0235 μs | 15.9238 μs |  5.69 |    0.04 | 257.8125 | 152.3438 |        - |  4231.7 KB |        9.72 |
| SourceGeneratedBenchmarks      | ShapeShift_Deserialize                              | Deserialize_SourceGenerated           |   292.334 μs |  2.0747 μs |  1.9407 μs |  0.62 |    0.00 |  29.2969 |  17.0898 |        - |  480.26 KB |        1.10 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_MergeKeys     | Deserialize_SourceGenerated_MergeKeys |     1.498 μs |  0.0097 μs |  0.0086 μs |  1.00 |    0.01 |   0.2289 |   0.0019 |        - |    3.77 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_Stream        | Deserialize_SourceGenerated_Stream    |   523.617 μs |  4.0130 μs |  3.7538 μs |  1.00 |    0.01 | 102.5391 |  88.8672 |  76.1719 |  705.95 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_GenericDictionary               | Serialize_GenericDictionary           |    85.354 μs |  0.9347 μs |  0.8286 μs |  1.00 |    0.01 |  71.4111 |  71.0449 |  71.0449 |  273.99 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_GenericDictionary              | Serialize_GenericDictionary           | 2,121.643 μs | 13.6679 μs | 12.7849 μs | 24.86 |    0.27 | 152.3438 | 148.4375 |  74.2188 | 2579.29 KB |        9.41 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Serialize_Poco                            | Serialize_Poco                        |    94.908 μs |  0.6357 μs |  0.5309 μs |  1.00 |    0.01 |  72.7539 |  71.0449 |  71.0449 |  294.43 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Serialize_Poco                           | Serialize_Poco                        | 2,722.752 μs | 13.2831 μs | 12.4251 μs | 28.69 |    0.20 | 152.3438 | 148.4375 |  74.2188 | 2551.32 KB |        8.67 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize                 | Serialize_SourceGenerated             |    80.478 μs |  1.1015 μs |  1.0303 μs |  1.00 |    0.02 |  71.0449 |  71.0449 |  71.0449 |  268.11 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Serialize                | Serialize_SourceGenerated             | 2,035.167 μs | 22.9459 μs | 19.1609 μs | 25.29 |    0.39 | 195.3125 | 128.9063 |  66.4063 |  2404.2 KB |        8.97 |
| SourceGeneratedBenchmarks      | ShapeShift_Serialize                                | Serialize_SourceGenerated             |   119.991 μs |  2.3379 μs |  2.6923 μs |  1.49 |    0.04 |  96.9238 |  87.4023 |  77.1484 |  617.47 KB |        2.30 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize_Stream          | Serialize_SourceGenerated_Stream      |    82.079 μs |  0.7330 μs |  0.6856 μs |  1.00 |    0.01 |  38.6963 |  38.3301 |  38.3301 |  139.52 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_StringList                      | Serialize_StringList                  |   142.537 μs |  2.4203 μs |  2.0211 μs |  1.00 |    0.02 |  84.2285 |  84.2285 |  84.2285 |   329.2 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_StringList                     | Serialize_StringList                  | 2,554.043 μs | 23.3765 μs | 21.8664 μs | 17.92 |    0.29 | 218.7500 | 214.8438 | 109.3750 | 3019.78 KB |        9.17 |

## Reading the ShapeShift.Yaml numbers

`ShapeShift.Yaml` is not a general YAML parser: its decoder reads a line-based subset of YAML, one `key: value` or `- item` per line, without flow collections (`[a, b]`, `{a: b}`), block scalars (`|`, `>`), multi-line scalars, anchors and aliases, tags or several documents. It slices its scalars out of the input and has no token or event layer. Each library therefore deserializes the text it wrote itself, which has the same shape for both.

Its deserialization time is best read as a lower bound for this document rather than as the cost of parsing YAML: SharpYaml runs the full YAML 1.2 grammar, with its depth and alias limits, in about 1.6 times that time and with fewer allocations. On serialization, SharpYaml is about 1.5 times as fast and allocates less than half.

Under NativeAOT, where the JIT's profile-guided optimizations are not available, both libraries are slower. Measured on the same machine with the same object graph: SharpYaml serializes in about 180 us against 310 us for ShapeShift.Yaml, and deserializes in about 710 us against 390 us.