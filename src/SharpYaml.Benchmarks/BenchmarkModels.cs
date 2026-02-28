namespace SharpYaml.Benchmarks;

public sealed class BenchmarkDocument
{
    public string Environment { get; set; } = string.Empty;

    public int Version { get; set; }

    public DatabaseConfiguration Database { get; set; } = new();

    public List<ServiceConfiguration> Services { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];

    public List<string> FeatureFlags { get; set; } = [];
}

public sealed class DatabaseConfiguration
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;

    public bool UseSsl { get; set; }
}

public sealed class ServiceConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool Enabled { get; set; }

    public List<string> Endpoints { get; set; } = [];

    public Dictionary<string, string> Labels { get; set; } = [];
}
