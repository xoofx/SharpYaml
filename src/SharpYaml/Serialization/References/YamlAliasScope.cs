// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using SharpYaml.Events;

namespace SharpYaml.Serialization.References;

/// <summary>
/// The alias state of one deserialization, shared by its reader and by the readers created for its buffered nodes:
/// the objects of anchored nodes under <see cref="YamlReferenceHandling.Preserve"/>, and the budget of nodes that
/// aliases can still expand to.
/// </summary>
internal sealed class YamlAliasScope
{
    private readonly int _maxExpansion;
    private int _remainingExpansion;

    public YamlAliasScope(YamlSerializerOptions options)
    {
        References = options.ReferenceHandling != YamlReferenceHandling.None ? new YamlReferenceReader() : null;
        _maxExpansion = options.EffectiveMaxAliasExpansion;
        _remainingExpansion = _maxExpansion;
        MaxDepth = options.EffectiveMaxDepth;
        SourceName = options.SourceName;
    }

    /// <summary>Gets the objects of anchored nodes, or <see langword="null"/> when references are not preserved.</summary>
    public YamlReferenceReader? References { get; }

    public int MaxDepth { get; }

    public string? SourceName { get; }

    /// <summary>Counts one node produced by an alias, and throws once the budget is spent.</summary>
    /// <exception cref="YamlException">Aliases expanded to more nodes than <see cref="YamlSerializerOptions.MaxAliasExpansion"/>.</exception>
    public void ConsumeExpansion(ParsingEvent node)
    {
        if (--_remainingExpansion < 0)
        {
            throw new YamlException(
                SourceName,
                node.Start,
                node.End,
                $"Aliases expand to more than {_maxExpansion} nodes, the limit set by YamlSerializerOptions.{nameof(YamlSerializerOptions.MaxAliasExpansion)}.");
        }
    }
}
