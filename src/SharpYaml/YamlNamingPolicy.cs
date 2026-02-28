using System;
using System.Text;

namespace SharpYaml;

/// <summary>
/// Provides a mechanism to convert CLR member names to YAML member names.
/// </summary>
public abstract class YamlNamingPolicy
{
    private sealed class CamelCaseYamlNamingPolicy : YamlNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            if (!char.IsUpper(name[0]))
            {
                return name;
            }

            var builder = new StringBuilder(name);
            builder[0] = char.ToLowerInvariant(builder[0]);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Gets a naming policy that converts the first character to camel-case.
    /// </summary>
    public static YamlNamingPolicy CamelCase { get; } = new CamelCaseYamlNamingPolicy();

    /// <summary>
    /// Converts a member name into a serialized YAML name.
    /// </summary>
    /// <param name="name">The CLR member name.</param>
    /// <returns>The serialized YAML member name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public abstract string ConvertName(string name);
}

