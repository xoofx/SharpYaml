using System;
using System.IO;

namespace SharpYaml;

/// <summary>
/// Serializes and deserializes YAML payloads, following a <c>System.Text.Json</c>-style API shape.
/// </summary>
public static class YamlSerializer
{
    private const string ReflectionSwitchName = "SharpYaml.YamlSerializer.IsReflectionEnabledByDefault";
    private static readonly bool ReflectionEnabledByDefault = AppContext.TryGetSwitch(ReflectionSwitchName, out var enabledBySwitch) ? enabledBySwitch : true;

    /// <summary>
    /// Gets a value indicating whether reflection-based serialization is enabled by default.
    /// </summary>
    public static bool IsReflectionEnabledByDefault => ReflectionEnabledByDefault;

    /// <summary>
    /// Serializes a value into YAML text.
    /// </summary>
    /// <typeparam name="T">The CLR type to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>A YAML payload.</returns>
    public static string Serialize<T>(T value, YamlSerializerOptions? options = null)
    {
        return Serialize((object?)value, typeof(T), options);
    }

    /// <summary>
    /// Serializes a value into YAML text using an explicit input type.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="inputType">The declared input type.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>A YAML payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inputType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Reflection is disabled and no metadata is available from <see cref="YamlSerializerOptions.TypeInfoResolver"/>.
    /// </exception>
    public static string Serialize(object? value, Type inputType, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(inputType);

        var effectiveOptions = options ?? YamlSerializerOptions.Default;
        var typeInfo = ResolveTypeInfo(effectiveOptions, inputType);
        return typeInfo.SerializeAsString(value);
    }

    /// <summary>
    /// Serializes a value to a writer.
    /// </summary>
    /// <typeparam name="T">The CLR type to serialize.</typeparam>
    /// <param name="writer">The destination writer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public static void Serialize<T>(TextWriter writer, T value, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Serialize((object?)value, typeof(T), options));
    }

    /// <summary>
    /// Serializes a value to a writer using an explicit input type.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="inputType">The declared input type.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="inputType"/> is <see langword="null"/>.</exception>
    public static void Serialize(TextWriter writer, object? value, Type inputType, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Serialize(value, inputType, options));
    }

    /// <summary>
    /// Deserializes a YAML payload from text.
    /// </summary>
    /// <typeparam name="T">The destination CLR type.</typeparam>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> is <see langword="null"/>.</exception>
    public static T? Deserialize<T>(string yaml, YamlSerializerOptions? options = null)
    {
        return (T?)Deserialize(yaml, typeof(T), options);
    }

    /// <summary>
    /// Deserializes a YAML payload into an explicit destination type.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="returnType">The destination CLR type.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> or <paramref name="returnType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Reflection is disabled and no metadata is available from <see cref="YamlSerializerOptions.TypeInfoResolver"/>.
    /// </exception>
    public static object? Deserialize(string yaml, Type returnType, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(returnType);

        var effectiveOptions = options ?? YamlSerializerOptions.Default;
        var typeInfo = ResolveTypeInfo(effectiveOptions, returnType);
        return typeInfo.DeserializeFromString(yaml);
    }

    /// <summary>
    /// Deserializes YAML from a text reader.
    /// </summary>
    /// <typeparam name="T">The destination CLR type.</typeparam>
    /// <param name="reader">The source reader.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static T? Deserialize<T>(TextReader reader, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Deserialize<T>(reader.ReadToEnd(), options);
    }

    /// <summary>
    /// Deserializes YAML from a text reader using an explicit destination type.
    /// </summary>
    /// <param name="reader">The source reader.</param>
    /// <param name="returnType">The destination CLR type.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="returnType"/> is <see langword="null"/>.</exception>
    public static object? Deserialize(TextReader reader, Type returnType, YamlSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Deserialize(reader.ReadToEnd(), returnType, options);
    }

    /// <summary>
    /// Deserializes YAML from a span of characters.
    /// </summary>
    /// <typeparam name="T">The destination CLR type.</typeparam>
    /// <param name="yaml">The YAML payload as a span.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>The deserialized value.</returns>
    public static T? Deserialize<T>(ReadOnlySpan<char> yaml, YamlSerializerOptions? options = null)
    {
        return Deserialize<T>(yaml.ToString(), options);
    }

    /// <summary>
    /// Deserializes YAML from a span of characters using an explicit destination type.
    /// </summary>
    /// <param name="yaml">The YAML payload as a span.</param>
    /// <param name="returnType">The destination CLR type.</param>
    /// <param name="options">The serializer options. If <see langword="null"/>, <see cref="YamlSerializerOptions.Default"/> is used.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="returnType"/> is <see langword="null"/>.</exception>
    public static object? Deserialize(ReadOnlySpan<char> yaml, Type returnType, YamlSerializerOptions? options = null)
    {
        return Deserialize(yaml.ToString(), returnType, options);
    }

    /// <summary>
    /// Serializes a value using explicit type metadata.
    /// </summary>
    /// <typeparam name="T">The represented CLR type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="typeInfo">The metadata used for serialization.</param>
    /// <returns>A YAML payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> is <see langword="null"/>.</exception>
    public static string Serialize<T>(T value, YamlTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return typeInfo.Serialize(value);
    }

    /// <summary>
    /// Deserializes a payload using explicit type metadata.
    /// </summary>
    /// <typeparam name="T">The represented CLR type.</typeparam>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="typeInfo">The metadata used for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> or <paramref name="typeInfo"/> is <see langword="null"/>.</exception>
    public static T? Deserialize<T>(string yaml, YamlTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return typeInfo.Deserialize(yaml);
    }

    /// <summary>
    /// Deserializes a payload using explicit type metadata from a character span.
    /// </summary>
    /// <typeparam name="T">The represented CLR type.</typeparam>
    /// <param name="yaml">The YAML payload as a span.</param>
    /// <param name="typeInfo">The metadata used for deserialization.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> is <see langword="null"/>.</exception>
    public static T? Deserialize<T>(ReadOnlySpan<char> yaml, YamlTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return typeInfo.Deserialize(yaml.ToString());
    }

    private static void EnsureReflectionAvailable(YamlSerializerOptions options, Type requestedType)
    {
        if (IsReflectionEnabledByDefault)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Reflection serialization is disabled and no metadata was found for '{requestedType}'. " +
            $"Provide metadata via {nameof(YamlSerializerOptions)}.{nameof(YamlSerializerOptions.TypeInfoResolver)} or enable the '{ReflectionSwitchName}' AppContext switch.");
    }

    private static YamlTypeInfo ResolveTypeInfo(YamlSerializerOptions options, Type requestedType)
    {
        var typeInfo = options.TypeInfoResolver?.GetTypeInfo(requestedType, options);
        if (typeInfo is not null)
        {
            return typeInfo;
        }

        if (!IsReflectionEnabledByDefault)
        {
            EnsureReflectionAvailable(options, requestedType);
        }

        typeInfo = ReflectionYamlTypeInfoResolver.Default.GetTypeInfo(requestedType, options);
        if (typeInfo is null)
        {
            throw new InvalidOperationException($"No metadata is available for '{requestedType}'.");
        }

        return typeInfo;
    }
}
