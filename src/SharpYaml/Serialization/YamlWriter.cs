using System;
using System.IO;

namespace SharpYaml.Serialization;

/// <summary>
/// Writes YAML tokens for use by <see cref="YamlConverter"/> implementations.
/// </summary>
public sealed class YamlWriter
{
    internal YamlWriter(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        Writer = writer;
    }

    internal TextWriter Writer { get; }
}

