using System.Globalization;

namespace SharpYaml.Benchmarks;

internal static class BenchmarkDataFactory
{
    public static BenchmarkDocument CreateDocument(int serviceCount, int endpointCountPerService)
    {
        var services = new List<ServiceConfiguration>(serviceCount);
        for (var i = 0; i < serviceCount; i++)
        {
            services.Add(new ServiceConfiguration
            {
                Name = $"service-{i:D4}",
                Owner = $"team-{i % 8:D2}",
                Port = 5000 + i,
                Enabled = i % 4 != 0,
                Endpoints = CreateEndpoints(i, endpointCountPerService),
                Labels = new Dictionary<string, string>
                {
                    ["region"] = i % 2 == 0 ? "us-east-1" : "us-west-2",
                    ["tier"] = i % 3 == 0 ? "critical" : "standard",
                    ["serviceGroup"] = $"group-{i % 12:D2}",
                },
            });
        }

        return new BenchmarkDocument
        {
            Environment = "production",
            Version = 42,
            Database = new DatabaseConfiguration
            {
                Host = "primary.database.internal",
                Port = 5432,
                User = "service_account",
                Password = "benchmark-password",
                Schema = "public",
                UseSsl = true,
            },
            Services = services,
            Metadata = new Dictionary<string, string>
            {
                ["owner"] = "platform-team",
                ["commit"] = "9f14b8a8",
                ["build"] = "2026.02.28.1",
                ["region"] = "us-east-1",
                ["deployedAt"] = "2026-02-28T00:00:00Z",
            },
            FeatureFlags = CreateFeatureFlags(128),
        };
    }

    public static Dictionary<string, object?> CreateGenericDocument(int serviceCount, int endpointCountPerService)
    {
        var services = new List<object?>(serviceCount);
        for (var i = 0; i < serviceCount; i++)
        {
            var labels = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["region"] = i % 2 == 0 ? "us-east-1" : "us-west-2",
                ["tier"] = i % 3 == 0 ? "critical" : "standard",
                ["serviceGroup"] = $"group-{i % 12:D2}",
            };

            services.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = $"service-{i:D4}",
                ["owner"] = $"team-{i % 8:D2}",
                ["port"] = 5000 + i,
                ["enabled"] = i % 4 != 0,
                ["endpoints"] = CreateEndpoints(i, endpointCountPerService),
                ["labels"] = labels,
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["environment"] = "production",
            ["version"] = 42,
            ["database"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["host"] = "primary.database.internal",
                ["port"] = 5432,
                ["user"] = "service_account",
                ["password"] = "benchmark-password",
                ["schema"] = "public",
                ["useSsl"] = true,
            },
            ["services"] = services,
            ["metadata"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["owner"] = "platform-team",
                ["commit"] = "9f14b8a8",
                ["build"] = "2026.02.28.1",
                ["region"] = "us-east-1",
                ["deployedAt"] = "2026-02-28T00:00:00Z",
            },
            ["featureFlags"] = CreateFeatureFlags(128),
        };
    }

    public static List<string> CreateStringList(int count)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add($"string-value-{i.ToString("D5", CultureInfo.InvariantCulture)}");
        }

        return list;
    }

    private static List<string> CreateEndpoints(int serviceIndex, int endpointCountPerService)
    {
        var endpoints = new List<string>(endpointCountPerService);
        for (var i = 0; i < endpointCountPerService; i++)
        {
            endpoints.Add($"/api/v{(i % 3) + 1}/service-{serviceIndex:D4}/resource-{i:D2}");
        }

        return endpoints;
    }

    private static List<string> CreateFeatureFlags(int count)
    {
        var flags = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            flags.Add($"feature_flag_{i:D3}");
        }

        return flags;
    }
}
