using System;

namespace SharpYaml;

/// <summary>
/// Provides reflection-based type metadata for <see cref="YamlSerializer"/>.
/// </summary>
public sealed class ReflectionYamlTypeInfoResolver : IYamlTypeInfoResolver
{
    private sealed class ReflectionYamlTypeInfo : YamlTypeInfo
    {
        public ReflectionYamlTypeInfo(Type type, YamlSerializerOptions options) : base(type, options)
        {
        }

        public override string SerializeAsString(object? value)
        {
            return YamlSerializer.SerializeWithReflection(value, Type, Options);
        }

        public override object? DeserializeFromString(string yaml)
        {
            return YamlSerializer.DeserializeWithReflection(yaml, Type, Options);
        }
    }

    /// <summary>
    /// Gets a shared default reflection resolver instance.
    /// </summary>
    public static ReflectionYamlTypeInfoResolver Default { get; } = new();

    /// <inheritdoc />
    public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);
        return new ReflectionYamlTypeInfo(type, options);
    }
}

