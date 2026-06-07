// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using SharpYaml.Events;
using SharpYaml.Model;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlModelNodeConverter : YamlConverter
{
    public static readonly YamlModelNodeConverter Instance = new();

    private YamlModelNodeConverter()
    {
    }

    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentGuard.ThrowIfNull(typeToConvert);
        return typeof(YamlNode).IsAssignableFrom(typeToConvert);
    }

    public override object? Read(YamlReader reader, Type typeToConvert)
    {
        ArgumentGuard.ThrowIfNull(reader);
        ArgumentGuard.ThrowIfNull(typeToConvert);

        var sourceName = reader.SourceName;
        var start = reader.Start;
        var end = reader.End;

        var node = ReadNode(reader, new Dictionary<string, YamlElement>());
        if (node is null)
        {
            return null;
        }

        if (!typeToConvert.IsInstanceOfType(node))
        {
            throw new YamlException(sourceName, start, end, $"Cannot deserialize YAML node '{node.GetType()}' into '{typeToConvert}'.");
        }

        return node;
    }

    public override void Write(YamlWriter writer, object? value)
    {
        ArgumentGuard.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is not YamlNode node)
        {
            throw new YamlException(Mark.Empty, Mark.Empty, $"Expected a '{typeof(YamlNode)}' instance but found '{value.GetType()}'.");
        }

        WriteNode(writer, node);
    }

    private static YamlElement? ReadNode(YamlReader reader, Dictionary<string, YamlElement> anchors)
    {
        switch (reader.CurrentEvent)
        {
            case MappingStart mappingStart:
                return ReadMapping(reader, mappingStart, anchors);

            case SequenceStart sequenceStart:
                return ReadSequence(reader, sequenceStart, anchors);

            case Scalar scalar:
                reader.Read();
                var value = new YamlValue(scalar, tracker: null);
                RegisterAnchor(value, anchors);
                return value;

            case AnchorAlias alias:
                reader.Read();
                if (!anchors.TryGetValue(alias.Value, out var anchored))
                {
                    throw new YamlException(
                        alias.Start,
                        alias.End,
                        FormattableString.Invariant($"Found an alias '*{alias.Value}' referencing an unknown anchor."));
                }

                // The model API does not currently preserve aliases as a distinct node type.
                // Materialize a copy so that writing the model back out does not emit duplicate anchors.
                var clone = (YamlElement)anchored.DeepClone(tracker: null);
                clone.Anchor = null;
                return clone;

            default:
                return null;
        }
    }

    private static YamlMapping ReadMapping(YamlReader reader, MappingStart mappingStart, Dictionary<string, YamlElement> anchors)
    {
        reader.Read();

        var keys = new List<YamlElement>();
        var contents = new Dictionary<YamlElement, YamlElement?>();
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType == YamlTokenType.None)
            {
                throw new YamlException("Unexpected end of mapping while loading YAML model.");
            }

            var key = ReadNode(reader, anchors);
            var value = ReadNode(reader, anchors);

            if (key is null || value is null)
            {
                throw new YamlException("Unexpected end of mapping while loading YAML model.");
            }

            keys.Add(key);
            contents[key] = value;
        }

        var mappingEnd = reader.CurrentEvent as MappingEnd
            ?? throw new YamlException(reader.SourceName, reader.Start, reader.End, "Expected the end of a YAML mapping.");
        reader.Read();

        var mapping = new YamlMapping(mappingStart, mappingEnd, keys, contents, tracker: null);
        RegisterAnchor(mapping, anchors);
        return mapping;
    }

    private static YamlSequence ReadSequence(YamlReader reader, SequenceStart sequenceStart, Dictionary<string, YamlElement> anchors)
    {
        reader.Read();

        var contents = new List<YamlElement>();
        while (reader.TokenType != YamlTokenType.EndSequence)
        {
            if (reader.TokenType == YamlTokenType.None)
            {
                throw new YamlException("Unexpected end of sequence while loading YAML model.");
            }

            var item = ReadNode(reader, anchors);
            if (item is not null)
            {
                contents.Add(item);
            }
        }

        var sequenceEnd = reader.CurrentEvent as SequenceEnd
            ?? throw new YamlException(reader.SourceName, reader.Start, reader.End, "Expected the end of a YAML sequence.");
        reader.Read();

        var sequence = new YamlSequence(sequenceStart, sequenceEnd, contents, tracker: null);
        RegisterAnchor(sequence, anchors);
        return sequence;
    }

    private static void RegisterAnchor(YamlElement element, Dictionary<string, YamlElement> anchors)
    {
        var anchor = element.Anchor;
        if (!string.IsNullOrEmpty(anchor))
        {
            anchors[anchor] = element;
        }
    }

    private static void WriteNode(YamlWriter writer, YamlNode node)
    {
        if (node is YamlElement element)
        {
            if (element.Anchor is not null)
            {
                writer.WriteAnchor(element.Anchor);
            }

            if (element.Tag is not null)
            {
                writer.WriteTag(element.Tag);
            }
        }

        switch (node)
        {
            case YamlValue scalar:
                writer.WriteScalar(scalar.Value);
                return;

            case YamlSequence sequence:
                writer.WriteStartSequence();
                for (var i = 0; i < sequence.Count; i++)
                {
                    WriteNode(writer, sequence[i]);
                }
                writer.WriteEndSequence();
                return;

            case YamlMapping mapping:
                writer.WriteStartMapping();
                for (var i = 0; i < mapping.Count; i++)
                {
                    var pair = ((IList<KeyValuePair<YamlElement, YamlElement?>>)mapping)[i];
                    if (pair.Key is not YamlValue keyValue)
                    {
                        throw new YamlException(Mark.Empty, Mark.Empty, "Only scalar mapping keys are supported when serializing a YamlMapping.");
                    }

                    writer.WritePropertyName(keyValue.Value);

                    if (pair.Value is null)
                    {
                        writer.WriteNullValue();
                        continue;
                    }

                    WriteNode(writer, pair.Value);
                }
                writer.WriteEndMapping();
                return;

            default:
                throw new YamlException(Mark.Empty, Mark.Empty, $"Unsupported YAML node type '{node.GetType()}'.");
        }
    }
}
