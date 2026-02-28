using System;
using System.Collections.Generic;
using SharpYaml.Serialization;

namespace SharpYaml;

/// <summary>
/// Configures the behavior of <see cref="YamlSerializer"/> operations.
/// </summary>
public sealed class YamlSerializerOptions
{
    /// <summary>
    /// Gets a default options instance.
    /// </summary>
    public static YamlSerializerOptions Default { get; } = new();

    private YamlConverter[] _converters = [];
    private int _indentSize = 2;
    private YamlScalarStylePreferences _scalarStylePreferences = new();
    private YamlPolymorphismOptions _polymorphismOptions = new();

    /// <summary>
    /// Gets the custom converters.
    /// </summary>
    /// <remarks>
    /// Converters are evaluated in order and take precedence over built-in converters.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A converter entry is <see langword="null"/>.</exception>
    public IReadOnlyList<YamlConverter> Converters
    {
        get => _converters;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                _converters = [];
                return;
            }

            var copy = new YamlConverter[value.Count];
            for (var i = 0; i < value.Count; i++)
            {
                var converter = value[i];
                if (converter is null)
                {
                    throw new ArgumentException("Converters cannot contain null entries.", nameof(value));
                }

                copy[i] = converter;
            }

            _converters = copy;
        }
    }

    /// <summary>
    /// Gets or sets the policy used to convert CLR property names.
    /// </summary>
    public YamlNamingPolicy? PropertyNamingPolicy { get; init; }

    /// <summary>
    /// Gets or sets an optional name for the YAML source.
    /// </summary>
    /// <remarks>
    /// This value is used to annotate <see cref="YamlException"/> messages with a source name
    /// (for example, a file path) when reporting parse errors.
    /// </remarks>
    public string? SourceName { get; init; }

    /// <summary>
    /// Gets or sets the policy used to convert dictionary keys during serialization.
    /// </summary>
    public YamlNamingPolicy? DictionaryKeyPolicy { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether property name matching is case-insensitive.
    /// </summary>
    public bool PropertyNameCaseInsensitive { get; init; }

    /// <summary>
    /// Gets or sets the default ignore condition for null/default values.
    /// </summary>
    public YamlIgnoreCondition DefaultIgnoreCondition { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether output should be indented.
    /// </summary>
    public bool WriteIndented { get; init; } = true;

    /// <summary>
    /// Gets or sets the number of spaces to use when <see cref="WriteIndented"/> is enabled.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is less than 1.</exception>
    public int IndentSize
    {
        get => _indentSize;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Indent size must be at least 1.");
            }

            _indentSize = value;
        }
    }

    /// <summary>
    /// Gets or sets member ordering behavior for emitted mappings.
    /// </summary>
    public YamlMappingOrderPolicy MappingOrder { get; init; } = YamlMappingOrderPolicy.Declaration;

    /// <summary>
    /// Gets or sets the schema used for scalar resolution.
    /// </summary>
    public YamlSchemaKind Schema { get; init; } = YamlSchemaKind.Core;

    /// <summary>
    /// Gets or sets behavior when duplicate mapping keys are encountered while reading.
    /// </summary>
    public YamlDuplicateKeyHandling DuplicateKeyHandling { get; init; } = YamlDuplicateKeyHandling.Error;

    /// <summary>
    /// Gets or sets a value indicating whether unregistered runtime type names from YAML tags are allowed during deserialization.
    /// </summary>
    /// <remarks>
    /// Enabling this option allows tag-based type name activation and should only be used with trusted YAML input.
    /// </remarks>
    public bool UnsafeAllowDeserializeFromTagTypeName { get; init; }

    /// <summary>
    /// Gets scalar style preferences for serialization.
    /// </summary>
    /// <exception cref="ArgumentNullException">Value is <see langword="null"/>.</exception>
    public YamlScalarStylePreferences ScalarStylePreferences
    {
        get => _scalarStylePreferences;
        init => _scalarStylePreferences = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets polymorphism options.
    /// </summary>
    /// <exception cref="ArgumentNullException">Value is <see langword="null"/>.</exception>
    public YamlPolymorphismOptions PolymorphismOptions
    {
        get => _polymorphismOptions;
        init => _polymorphismOptions = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets object reference handling behavior.
    /// </summary>
    public YamlReferenceHandling ReferenceHandling { get; init; }

    /// <summary>
    /// Gets or sets a metadata resolver used to retrieve <see cref="YamlTypeInfo"/> instances.
    /// </summary>
    public IYamlTypeInfoResolver? TypeInfoResolver { get; init; }

    /// <summary>
    /// Gets a converter that can handle <paramref name="typeToConvert"/>.
    /// </summary>
    /// <param name="typeToConvert">The CLR type to resolve.</param>
    /// <returns>The converter for <paramref name="typeToConvert"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeToConvert"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">No converter can handle <paramref name="typeToConvert"/>.</exception>
    public YamlConverter GetConverter(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        if (TryGetCustomConverter(typeToConvert, out var converter) && converter is not null)
        {
            return converter;
        }

        throw new NotSupportedException($"No YAML converter is registered for '{typeToConvert}'.");
    }

    /// <summary>
    /// Attempts to resolve a custom converter for <paramref name="typeToConvert"/> from <see cref="Converters"/>.
    /// </summary>
    /// <param name="typeToConvert">The CLR type to resolve.</param>
    /// <param name="converter">When successful, receives the converter instance.</param>
    /// <returns><see langword="true"/> when a custom converter was resolved; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeToConvert"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A converter factory returned <see langword="null"/> or returned a converter that does not handle <paramref name="typeToConvert"/>.
    /// </exception>
    public bool TryGetCustomConverter(Type typeToConvert, out YamlConverter? converter)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        // Search user-provided converters first (same precedence rule as System.Text.Json).
        for (var i = 0; i < _converters.Length; i++)
        {
            var candidate = _converters[i];
            if (candidate is null)
            {
                continue;
            }

            if (candidate is YamlConverterFactory factory)
            {
                if (!factory.CanConvert(typeToConvert))
                {
                    continue;
                }

                var created = factory.CreateConverter(typeToConvert, this);
                if (created is null || !created.CanConvert(typeToConvert))
                {
                    throw new InvalidOperationException($"Converter factory '{factory.GetType()}' returned an invalid converter for '{typeToConvert}'.");
                }

                converter = created;
                return true;
            }

            if (candidate.CanConvert(typeToConvert))
            {
                converter = candidate;
                return true;
            }
        }

        converter = null;
        return false;
    }

    internal YamlSerializerOptions WithTypeInfoResolverIfMissing(IYamlTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        if (TypeInfoResolver is not null)
        {
            return this;
        }

        return new YamlSerializerOptions
        {
            Converters = Converters,
            PropertyNamingPolicy = PropertyNamingPolicy,
            SourceName = SourceName,
            DictionaryKeyPolicy = DictionaryKeyPolicy,
            PropertyNameCaseInsensitive = PropertyNameCaseInsensitive,
            DefaultIgnoreCondition = DefaultIgnoreCondition,
            WriteIndented = WriteIndented,
            IndentSize = IndentSize,
            MappingOrder = MappingOrder,
            Schema = Schema,
            DuplicateKeyHandling = DuplicateKeyHandling,
            UnsafeAllowDeserializeFromTagTypeName = UnsafeAllowDeserializeFromTagTypeName,
            ScalarStylePreferences = ScalarStylePreferences,
            PolymorphismOptions = PolymorphismOptions,
            ReferenceHandling = ReferenceHandling,
            TypeInfoResolver = resolver,
        };
    }
}
