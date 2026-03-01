---
title: Benchmark suite
---

# SharpYaml Benchmark Suite

The benchmark suite lives in `src/SharpYaml.Benchmarks/` and targets `net10.0`.

It compares SharpYaml with:

- `YamlDotNet` `16.3.0` (dynamic serializer/deserializer).
- `Vecc.YamlDotNet.Analyzers.StaticGenerator` `16.3.0` (static generator path).

## Covered scenarios

- Serialization of a large generic document:
  - `Dictionary<string, object?>`
  - `List<string>`
- Serialization and deserialization of a large strongly typed object graph.
- Source-generated/static-context serialization of the same strongly typed object graph.

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