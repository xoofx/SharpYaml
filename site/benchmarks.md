---
title: Benchmark suite
---

# SharpYaml Benchmark Suite

The benchmark suite lives in `src/SharpYaml.Benchmarks/` and targets `net10.0`.

In these benchmarks, SharpYaml is typically **~5x to ~29x faster** than YamlDotNet and allocates **~9x less memory**, depending on the scenario.

It compares SharpYaml with:

- `YamlDotNet` `16.3.0` (dynamic serializer/deserializer).
- `Vecc.YamlDotNet.Analyzers.StaticGenerator` `16.3.0` (static generator path).
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
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco                          | Deserialize_Poco                      |   511.023 μs |  2.0530 μs |  1.6029 μs |  1.00 |    0.00 |  26.3672 |  13.6719 |        - |  444.29 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Deserialize_Poco                         | Deserialize_Poco                      | 2,655.698 μs | 15.3801 μs | 14.3866 μs |  5.20 |    0.03 | 253.9063 | 171.8750 |        - | 4169.07 KB |        9.38 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys              | Deserialize_Poco_Unmapped             |   510.324 μs |  1.8729 μs |  1.7519 μs |  1.00 |    0.00 |  26.3672 |  14.6484 |        - |  445.42 KB |        1.00 |
| PocoBenchmarks                 | SharpYaml_Deserialize_Poco_UnknownKeys_WithCallback | Deserialize_Poco_Unmapped             |   529.160 μs |  2.9955 μs |  2.8020 μs |  1.04 |    0.01 |  27.3438 |  12.6953 |        - |  460.33 KB |        1.03 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize               | Deserialize_SourceGenerated           |   469.362 μs |  2.1276 μs |  1.8861 μs |  1.00 |    0.01 |  26.3672 |  13.1836 |        - |  435.47 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Deserialize              | Deserialize_SourceGenerated           | 2,378.478 μs | 14.9589 μs | 12.4913 μs |  5.07 |    0.03 | 246.0938 | 160.1563 |        - | 4030.26 KB |        9.25 |
| SourceGeneratedBenchmarks      | ShapeShift_Deserialize                              | Deserialize_SourceGenerated           |   270.188 μs |  2.0489 μs |  1.9166 μs |  0.58 |    0.00 |  29.2969 |  17.0898 |        - |  480.26 KB |        1.10 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_MergeKeys     | Deserialize_SourceGenerated_MergeKeys |     1.495 μs |  0.0101 μs |  0.0090 μs |  1.00 |    0.01 |   0.2289 |   0.0019 |        - |    3.77 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Deserialize_Stream        | Deserialize_SourceGenerated_Stream    |   533.234 μs |  2.8816 μs |  2.6954 μs |  1.00 |    0.01 | 102.5391 |  86.9141 |  76.1719 |  705.91 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_GenericDictionary               | Serialize_GenericDictionary           |    87.243 μs |  0.7431 μs |  0.6951 μs |  1.00 |    0.01 |  71.4111 |  71.0449 |  71.0449 |  273.99 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_GenericDictionary              | Serialize_GenericDictionary           | 2,194.724 μs |  9.9533 μs |  9.3103 μs | 25.16 |    0.22 | 152.3438 | 148.4375 |  74.2188 | 2579.25 KB |        9.41 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| PocoBenchmarks                 | SharpYaml_Serialize_Poco                            | Serialize_Poco                        |    92.957 μs |  0.6951 μs |  0.6502 μs |  1.00 |    0.01 |  72.8760 |  71.1670 |  71.1670 |   294.4 KB |        1.00 |
| PocoBenchmarks                 | YamlDotNet_Serialize_Poco                           | Serialize_Poco                        | 2,732.950 μs | 13.2915 μs | 12.4329 μs | 29.40 |    0.24 | 152.3438 | 148.4375 |  74.2188 | 2551.29 KB |        8.67 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize                 | Serialize_SourceGenerated             |    79.345 μs |  0.7915 μs |  0.7404 μs |  1.00 |    0.01 |  71.1670 |  71.1670 |  71.1670 |  268.12 KB |        1.00 |
| SourceGeneratedBenchmarks      | YamlDotNet_StaticGenerator_Serialize                | Serialize_SourceGenerated             | 2,030.131 μs | 28.2981 μs | 26.4701 μs | 25.59 |    0.40 | 195.3125 | 128.9063 |  66.4063 | 2404.19 KB |        8.97 |
| SourceGeneratedBenchmarks      | ShapeShift_Serialize                                | Serialize_SourceGenerated             |   121.271 μs |  2.4186 μs |  2.3754 μs |  1.53 |    0.03 |  98.7549 |  88.5010 |  78.8574 |  617.43 KB |        2.30 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| SourceGeneratedBenchmarks      | SharpYaml_SourceGenerated_Serialize_Stream          | Serialize_SourceGenerated_Stream      |    81.210 μs |  0.7479 μs |  0.6996 μs |  1.00 |    0.01 |  38.8184 |  38.4521 |  38.4521 |  139.51 KB |        1.00 |
|                                |                                                     |                                       |              |            |            |       |         |          |          |          |            |             |
| GenericSerializationBenchmarks | SharpYaml_Serialize_StringList                      | Serialize_StringList                  |   140.623 μs |  2.8096 μs |  2.7594 μs |  1.00 |    0.03 |  84.2285 |  84.2285 |  84.2285 |  329.19 KB |        1.00 |
| GenericSerializationBenchmarks | YamlDotNet_Serialize_StringList                     | Serialize_StringList                  | 2,492.551 μs | 23.2387 μs | 21.7375 μs | 17.73 |    0.36 | 218.7500 | 214.8438 | 109.3750 | 3019.75 KB |        9.17 |

## Reading the ShapeShift.Yaml numbers

`ShapeShift.Yaml` is not a general YAML parser: its decoder reads a line-based subset of YAML, one `key: value` or `- item` per line, without flow collections (`[a, b]`, `{a: b}`), block scalars (`|`, `>`), multi-line scalars, anchors and aliases, tags or several documents. It slices its scalars out of the input and has no token or event layer. Each library therefore deserializes the text it wrote itself, which has the same shape for both.

Its deserialization time is best read as a lower bound for this document rather than as the cost of parsing YAML: SharpYaml runs the full YAML 1.2 grammar, with its depth and alias limits, in about 1.7 times that time and with fewer allocations. On serialization, SharpYaml is about 1.5 times as fast and allocates less than half.

Under NativeAOT, where the JIT's profile-guided optimizations are not available, both libraries are slower. Measured on the same machine with the same object graph: SharpYaml serializes in about 180 us against 310 us for ShapeShift.Yaml, and deserializes in about 710 us against 390 us.