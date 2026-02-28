using System;

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

    /// <summary>
    /// Gets or sets the policy used to convert CLR property names.
    /// </summary>
    public YamlNamingPolicy? PropertyNamingPolicy { get; set; }

    /// <summary>
    /// Gets or sets the policy used to convert dictionary keys during serialization.
    /// </summary>
    public YamlNamingPolicy? DictionaryKeyPolicy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether property name matching is case-insensitive.
    /// </summary>
    public bool PropertyNameCaseInsensitive { get; set; }

    /// <summary>
    /// Gets or sets the default ignore condition for null/default values.
    /// </summary>
    public YamlIgnoreCondition DefaultIgnoreCondition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether output should be indented.
    /// </summary>
    public bool WriteIndented { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of spaces to use when <see cref="WriteIndented"/> is enabled.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is less than 1.</exception>
    public int IndentSize
    {
        get => _indentSize;
        set
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
    public YamlMappingOrderPolicy MappingOrder { get; set; } = YamlMappingOrderPolicy.Declaration;

    /// <summary>
    /// Gets or sets the schema used for scalar resolution.
    /// </summary>
    public YamlSchemaKind Schema { get; set; } = YamlSchemaKind.Core;

    /// <summary>
    /// Gets or sets behavior when duplicate mapping keys are encountered while reading.
    /// </summary>
    public YamlDuplicateKeyHandling DuplicateKeyHandling { get; set; } = YamlDuplicateKeyHandling.Error;

    /// <summary>
    /// Gets or sets a value indicating whether unregistered runtime type names from YAML tags are allowed during deserialization.
    /// </summary>
    /// <remarks>
    /// Enabling this option allows tag-based type name activation and should only be used with trusted YAML input.
    /// </remarks>
    public bool UnsafeAllowDeserializeFromTagTypeName { get; set; }

    /// <summary>
    /// Gets scalar style preferences for serialization.
    /// </summary>
    public YamlScalarStylePreferences ScalarStylePreferences { get; } = new();

    /// <summary>
    /// Gets polymorphism options.
    /// </summary>
    public YamlPolymorphismOptions PolymorphismOptions { get; } = new();

    /// <summary>
    /// Gets or sets object reference handling behavior.
    /// </summary>
    public YamlReferenceHandling ReferenceHandling { get; set; }

    /// <summary>
    /// Gets or sets a metadata resolver used to retrieve <see cref="YamlTypeInfo"/> instances.
    /// </summary>
    public IYamlTypeInfoResolver? TypeInfoResolver { get; set; }

    private int _indentSize = 2;
}
