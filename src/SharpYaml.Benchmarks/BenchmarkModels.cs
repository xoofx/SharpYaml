using PolyType;

namespace SharpYaml.Benchmarks;

// The models are partial and carry [GenerateShape] for ShapeShift, which reads them through PolyType's generated shapes.
[GenerateShape]
public sealed partial class BenchmarkDocument
{
    public string Environment { get; set; } = string.Empty;

    public int Version { get; set; }

    public DatabaseConfiguration Database { get; set; } = new();

    public List<ServiceConfiguration> Services { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];

    public List<string> FeatureFlags { get; set; } = [];
}

[GenerateShape]
public sealed partial class DatabaseConfiguration
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;

    public bool UseSsl { get; set; }
}

[GenerateShape]
public sealed partial class ServiceConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool Enabled { get; set; }

    public List<string> Endpoints { get; set; } = [];

    public Dictionary<string, string> Labels { get; set; } = [];
}
