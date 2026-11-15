---
title: Benchmark suite
---

# SharpYaml Benchmark Suite

The benchmark suite lives in `src/SharpYaml.Benchmarks/` and targets `net10.0`.

In these benchmarks, SharpYaml is typically **~5x to ~25x faster** than YamlDotNet and allocates **~7x to ~9x less memory**, depending on the scenario.

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

## Reading the ShapeShift.Yaml numbers

`ShapeShift.Yaml` is not a general YAML parser: its decoder reads a line-based subset of YAML, one `key: value` or `- item` per line, without flow collections (`[a, b]`, `{a: b}`), block scalars (`|`, `>`), multi-line scalars, anchors and aliases, tags or several documents. It slices its scalars out of the input and has no token or event layer. Each library therefore deserializes the text it wrote itself, which has the same shape for both.

Its deserialization time is best read as a lower bound for this document rather than as the cost of parsing YAML: SharpYaml runs the full YAML 1.2 grammar, with its depth and alias limits, in about 1.8 times that time and with about the same allocations. On serialization, SharpYaml is about twice as fast and allocates less than half.

Under NativeAOT, where the JIT's profile-guided optimizations are not available, both libraries are slower. Measured on the same machine with the same object graph: SharpYaml serializes in about 180 us against 342 us for ShapeShift.Yaml, and deserializes in about 780 us against 410 us.