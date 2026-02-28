using System;

namespace SharpYaml;

/// <summary>
/// Represents metadata and operations for a serializable type.
/// </summary>
public abstract class YamlTypeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlTypeInfo"/> class.
    /// </summary>
    /// <param name="type">The represented CLR type.</param>
    /// <param name="options">The options associated with the metadata.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="type"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    protected YamlTypeInfo(Type type, YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);

        Type = type;
        Options = options;
    }

    /// <summary>
    /// Gets the CLR type represented by this metadata.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the options associated with this metadata instance.
    /// </summary>
    public YamlSerializerOptions Options { get; }

    /// <summary>
    /// Serializes a value as YAML text.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The YAML payload.</returns>
    public abstract string SerializeAsString(object? value);

    /// <summary>
    /// Deserializes YAML text into a value.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
    public abstract object? DeserializeFromString(string yaml);
}

/// <summary>
/// Represents metadata and operations for a specific serializable type.
/// </summary>
/// <typeparam name="T">The represented CLR type.</typeparam>
public abstract class YamlTypeInfo<T> : YamlTypeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlTypeInfo{T}"/> class.
    /// </summary>
    /// <param name="options">The options associated with the metadata.</param>
    protected YamlTypeInfo(YamlSerializerOptions options) : base(typeof(T), options)
    {
    }

    /// <summary>
    /// Serializes a value as YAML text.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The YAML payload.</returns>
    public abstract string Serialize(T value);

    /// <summary>
    /// Deserializes YAML text into a value.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
    public abstract T? Deserialize(string yaml);

    /// <inheritdoc />
    public override string SerializeAsString(object? value)
    {
        return Serialize((T)value!);
    }

    /// <inheritdoc />
    public override object? DeserializeFromString(string yaml)
    {
        return Deserialize(yaml);
    }
}

