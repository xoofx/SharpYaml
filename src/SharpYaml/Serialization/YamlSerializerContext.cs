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
    protected YamlSerializerContext()
        : this(new YamlSerializerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlSerializerContext"/> class.
    /// </summary>
    /// <param name="options">The options used by this context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> specifies a <see cref="YamlSerializerOptions.TypeInfoResolver"/> that is not this context.
    /// </exception>
    protected YamlSerializerContext(YamlSerializerOptions options)
    {
        ArgumentGuard.ThrowIfNull(options);

        if (options.TypeInfoResolver is null)
        {
            Options = options with { TypeInfoResolver = this };
            return;
        }

        if (!ReferenceEquals(options.TypeInfoResolver, this))
        {
            throw new ArgumentException(
                $"The provided {nameof(YamlSerializerOptions)} instance is associated with a different {nameof(YamlSerializerOptions.TypeInfoResolver)}. " +
                $"A {nameof(YamlSerializerContext)} must use an options instance whose {nameof(YamlSerializerOptions.TypeInfoResolver)} is the context itself.",
                nameof(options));
        }

        Options = options;
    }

    /// <summary>
    /// Gets the options associated with this context.
    /// </summary>
    public YamlSerializerOptions Options { get; }

    /// <summary>
    /// Gets typed metadata for a CLR type handled by this context.
    /// </summary>
    /// <typeparam name="T">The CLR type to resolve.</typeparam>
    /// <returns>The resolved metadata.</returns>
    /// <exception cref="InvalidOperationException">No metadata is available for <typeparamref name="T"/>.</exception>
    public YamlTypeInfo<T> GetTypeInfo<T>()
    {
        var typeInfo = GetTypeInfo(typeof(T), Options) as YamlTypeInfo<T>;
        if (typeInfo is null)
        {
            throw new InvalidOperationException($"No generated metadata is available for type '{typeof(T)}' on context '{GetType()}'.");
        }

        return typeInfo;
    }

    /// <inheritdoc />
    public abstract YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options);
}
