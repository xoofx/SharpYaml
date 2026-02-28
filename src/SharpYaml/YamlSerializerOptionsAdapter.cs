using System;
using SharpYaml.Schemas;
using SharpYaml.Serialization;
using SharpYaml.Serialization.Descriptors;

namespace SharpYaml;

internal static class YamlSerializerOptionsAdapter
{
    private sealed class PolicyNamingConvention : IMemberNamingConvention
    {
        private readonly YamlNamingPolicy _policy;
        private readonly StringComparer _comparer;

        public PolicyNamingConvention(YamlNamingPolicy policy, bool caseInsensitive)
        {
            _policy = policy;
            _comparer = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        }

        public StringComparer Comparer => _comparer;

        public string Convert(string name)
        {
            return _policy.ConvertName(name);
        }
    }

    public static SerializerSettings ToLegacySettings(YamlSerializerOptions options)
    {
        var schema = options.Schema switch
        {
            YamlSchemaKind.Core => CoreSchema.Instance,
            YamlSchemaKind.Json => new JsonSchema(),
            YamlSchemaKind.Failsafe => new FailsafeSchema(),
            YamlSchemaKind.Extended => new ExtendedSchema(),
            _ => CoreSchema.Instance,
        };

        var settings = new SerializerSettings(schema)
        {
            PreferredIndent = options.IndentSize,
            EmitDefaultValues = options.DefaultIgnoreCondition == YamlIgnoreCondition.Never,
            IgnoreNulls = options.DefaultIgnoreCondition is YamlIgnoreCondition.WhenWritingNull or YamlIgnoreCondition.WhenWritingDefault,
            EmitAlias = options.ReferenceHandling == YamlReferenceHandling.Preserve,
            ResetAlias = options.ReferenceHandling == YamlReferenceHandling.Preserve,
            ComparerForKeySorting = options.MappingOrder == YamlMappingOrderPolicy.Sorted ? new DefaultKeyComparer() : null!,
            SortKeyForMapping = options.MappingOrder == YamlMappingOrderPolicy.Sorted,
        };

        if (options.PropertyNamingPolicy is not null)
        {
            settings.NamingConvention = new PolicyNamingConvention(options.PropertyNamingPolicy, options.PropertyNameCaseInsensitive);
        }
        else if (options.PropertyNameCaseInsensitive)
        {
            settings.NamingConvention = new CaseInsensitiveDefaultNamingConvention();
        }

        return settings;
    }
}
