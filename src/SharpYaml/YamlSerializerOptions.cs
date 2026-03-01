using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using SharpYaml.Serialization;

namespace SharpYaml;

/// <summary>
/// Configures the behavior of <see cref="YamlSerializer"/> operations.
/// </summary>
public sealed record YamlSerializerOptions
{
    private static readonly YamlConverter[] s_emptyConverters = [];
    private static readonly ReadOnlyCollection<YamlConverter> s_emptyConvertersReadOnly = Array.AsReadOnly(s_emptyConverters);

    /// <summary>
    /// Gets a default options instance.
    /// </summary>
    public static YamlSerializerOptions Default { get; } = new();

    private YamlConverter[] _converters = s_emptyConverters;
    private readonly ReadOnlyCollection<YamlConverter> _convertersReadOnly = s_emptyConvertersReadOnly;

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
        get => _convertersReadOnly;
        init
        {
            ArgumentGuard.ThrowIfNull(value);
            if (value.Count == 0)
            {
                _converters = s_emptyConverters;
                _convertersReadOnly = s_emptyConvertersReadOnly;
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
            _convertersReadOnly = Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// Gets or sets the policy used to convert CLR property names.
    /// </summary>
    public JsonNamingPolicy? PropertyNamingPolicy { get; init; }

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
    public JsonNamingPolicy? DictionaryKeyPolicy { get; init; }

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
        get;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Indent size must be at least 1.");
            }

            field = value;
        }
    } = 2;

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
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();

    /// <summary>
    /// Gets polymorphism options.
    /// </summary>
    /// <exception cref="ArgumentNullException">Value is <see langword="null"/>.</exception>
    public YamlPolymorphismOptions PolymorphismOptions
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();

    /// <summary>
    /// Gets or sets object reference handling behavior.
    /// </summary>
    public YamlReferenceHandling ReferenceHandling { get; init; }

    /// <summary>
    /// Gets or sets a metadata resolver used to retrieve <see cref="YamlTypeInfo"/> instances.
    /// </summary>
    public IYamlTypeInfoResolver? TypeInfoResolver { get; init; }
}
