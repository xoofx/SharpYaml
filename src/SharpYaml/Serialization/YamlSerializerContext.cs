using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Base type for source-generated YAML serializer contexts.
/// </summary>
public abstract partial class YamlSerializerContext : IYamlTypeInfoResolver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlSerializerContext"/> class.
    /// </summary>
    /// <param name="options">The options used by this context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    protected YamlSerializerContext(YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    /// <summary>
    /// Gets the options associated with this context.
    /// </summary>
    public YamlSerializerOptions Options { get; }

    /// <inheritdoc />
    public abstract YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options);
}

