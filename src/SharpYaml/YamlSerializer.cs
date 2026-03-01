using System;
using System.IO;
using System.Text;
using SharpYaml.Serialization;

namespace SharpYaml;

/// <summary>
/// Serializes and deserializes YAML payloads, following a <c>System.Text.Json</c>-style API shape.
/// </summary>
public static class YamlSerializer
{
    private const string ReflectionSwitchName = "SharpYaml.YamlSerializer.IsReflectionEnabledByDefault";
    private static readonly bool ReflectionEnabledByDefault = AppContext.TryGetSwitch(ReflectionSwitchName, out var enabledBySwitch) ? enabledBySwitch : true;
    [ThreadStatic]
    private static StringBuilder? s_cachedStringBuilder;

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
    /// Serializes a value into YAML text using generated metadata from a serializer context.
    /// </summary>
    /// <typeparam name="T">The CLR type to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>A YAML payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <typeparamref name="T"/> in <paramref name="context"/>.</exception>
    public static string Serialize<T>(T value, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Serialize(value, typeof(T), context);
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
        return SerializeCore(typeInfo, value);
    }

    /// <summary>
    /// Serializes a value into YAML text using generated metadata from a serializer context.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="inputType">The declared input type.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>A YAML payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inputType"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <paramref name="inputType"/> in <paramref name="context"/>.</exception>
    public static string Serialize(object? value, Type inputType, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(inputType);
        ArgumentNullException.ThrowIfNull(context);

        var typeInfo = ResolveTypeInfo(context, inputType);
        return SerializeCore(typeInfo, value);
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
    /// Serializes a value to a writer using generated metadata from a serializer context.
    /// </summary>
    /// <typeparam name="T">The CLR type to serialize.</typeparam>
    /// <param name="writer">The destination writer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <typeparamref name="T"/> in <paramref name="context"/>.</exception>
    public static void Serialize<T>(TextWriter writer, T value, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Serialize((object?)value, typeof(T), context));
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
    /// Serializes a value to a writer using generated metadata from a serializer context.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="inputType">The declared input type.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/>, <paramref name="inputType"/>, or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <paramref name="inputType"/> in <paramref name="context"/>.</exception>
    public static void Serialize(TextWriter writer, object? value, Type inputType, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(Serialize(value, inputType, context));
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
    /// Deserializes a YAML payload from text using generated metadata from a serializer context.
    /// </summary>
    /// <typeparam name="T">The destination CLR type.</typeparam>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <typeparamref name="T"/> in <paramref name="context"/>.</exception>
    public static T? Deserialize<T>(string yaml, YamlSerializerContext context)
    {
        return (T?)Deserialize(yaml, typeof(T), context);
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
        return DeserializeCore(typeInfo, yaml);
    }

    /// <summary>
    /// Deserializes a YAML payload into an explicit destination type using generated metadata from a serializer context.
    /// </summary>
    /// <param name="yaml">The YAML payload.</param>
    /// <param name="returnType">The destination CLR type.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="yaml"/>, <paramref name="returnType"/>, or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <paramref name="returnType"/> in <paramref name="context"/>.</exception>
    public static object? Deserialize(string yaml, Type returnType, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(returnType);
        ArgumentNullException.ThrowIfNull(context);

        var typeInfo = ResolveTypeInfo(context, returnType);
        return DeserializeCore(typeInfo, yaml);
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
    /// Deserializes YAML from a text reader using generated metadata from a serializer context.
    /// </summary>
    /// <typeparam name="T">The destination CLR type.</typeparam>
    /// <param name="reader">The source reader.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <typeparamref name="T"/> in <paramref name="context"/>.</exception>
    public static T? Deserialize<T>(TextReader reader, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Deserialize<T>(reader.ReadToEnd(), context);
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
    /// Deserializes YAML from a text reader using an explicit destination type and generated metadata from a serializer context.
    /// </summary>
    /// <param name="reader">The source reader.</param>
    /// <param name="returnType">The destination CLR type.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/>, <paramref name="returnType"/>, or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <paramref name="returnType"/> in <paramref name="context"/>.</exception>
    public static object? Deserialize(TextReader reader, Type returnType, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Deserialize(reader.ReadToEnd(), returnType, context);
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
    /// Deserializes YAML from a span of characters using generated metadata from a serializer context.
    /// </summary>
    /// <typeparam name="T">The destination CLR type.</typeparam>
    /// <param name="yaml">The YAML payload as a span.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <typeparamref name="T"/> in <paramref name="context"/>.</exception>
    public static T? Deserialize<T>(ReadOnlySpan<char> yaml, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Deserialize<T>(yaml.ToString(), context);
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
    /// Deserializes YAML from a span of characters using an explicit destination type and generated metadata from a serializer context.
    /// </summary>
    /// <param name="yaml">The YAML payload as a span.</param>
    /// <param name="returnType">The destination CLR type.</param>
    /// <param name="context">The source-generated serializer context.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="returnType"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No generated metadata is available for <paramref name="returnType"/> in <paramref name="context"/>.</exception>
    public static object? Deserialize(ReadOnlySpan<char> yaml, Type returnType, YamlSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        ArgumentNullException.ThrowIfNull(context);
        return Deserialize(yaml.ToString(), returnType, context);
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
        return SerializeCore(typeInfo, value);
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
        return DeserializeCore(typeInfo, yaml);
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
        return DeserializeCore(typeInfo, yaml.ToString());
    }

    private static string SerializeCore(YamlTypeInfo typeInfo, object? value)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var stringBuilder = AcquireStringBuilder(minimumCapacity: 1024);
        var writer = new YamlWriter(stringBuilder, typeInfo.Options);
        typeInfo.Write(writer, value);
        if (stringBuilder.Length == 0 || stringBuilder[stringBuilder.Length - 1] != '\n')
        {
            stringBuilder.Append('\n');
        }

        return GetStringAndReleaseBuilder(stringBuilder);
    }

    private static string SerializeCore<T>(YamlTypeInfo<T> typeInfo, T value)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var stringBuilder = AcquireStringBuilder(minimumCapacity: 1024);
        var writer = new YamlWriter(stringBuilder, typeInfo.Options);
        typeInfo.Write(writer, value);
        if (stringBuilder.Length == 0 || stringBuilder[stringBuilder.Length - 1] != '\n')
        {
            stringBuilder.Append('\n');
        }

        return GetStringAndReleaseBuilder(stringBuilder);
    }

    private static object? DeserializeCore(YamlTypeInfo typeInfo, string yaml)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(yaml);

        var reader = YamlReader.Create(yaml, typeInfo.Options);
        if (!reader.Read())
        {
            return null;
        }

        return typeInfo.ReadAsObject(reader);
    }

    private static T? DeserializeCore<T>(YamlTypeInfo<T> typeInfo, string yaml)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(yaml);

        var reader = YamlReader.Create(yaml, typeInfo.Options);
        if (!reader.Read())
        {
            return default;
        }

        return typeInfo.Read(reader);
    }

    private static StringBuilder AcquireStringBuilder(int minimumCapacity)
    {
        var cached = s_cachedStringBuilder;
        if (cached is not null)
        {
            s_cachedStringBuilder = null;
            cached.Clear();
            if (cached.Capacity < minimumCapacity)
            {
                cached.EnsureCapacity(minimumCapacity);
            }

            return cached;
        }

        return new StringBuilder(minimumCapacity);
    }

    private static string GetStringAndReleaseBuilder(StringBuilder builder)
    {
        var result = builder.ToString();
        if (builder.Capacity <= 1024 * 1024)
        {
            builder.Clear();
            s_cachedStringBuilder = builder;
        }

        return result;
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

    private static YamlTypeInfo ResolveTypeInfo(YamlSerializerContext context, Type requestedType)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requestedType);

        var typeInfo = context.GetTypeInfo(requestedType, context.Options);
        if (typeInfo is not null)
        {
            return typeInfo;
        }

        throw new InvalidOperationException($"No generated metadata is available for '{requestedType}' on context '{context.GetType()}'.");
    }

    private static YamlTypeInfo ResolveTypeInfo(YamlSerializerOptions options, Type requestedType)
    {
        if (options.TypeInfoResolver is YamlSerializerContext context && !ReferenceEquals(options, context.Options))
        {
            throw new InvalidOperationException(
                $"The provided {nameof(YamlSerializerOptions)} instance does not match the options associated with the source-generated context '{context.GetType()}'. " +
                $"Use the overloads that accept a {nameof(YamlSerializerContext)} directly, or pass '{context.GetType()}.{nameof(YamlSerializerContext.Options)}' as the options instance.");
        }

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
