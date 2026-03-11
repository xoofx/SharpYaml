// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
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

        var buffered = YamlReader.BufferCurrentNodeToString(reader);
        var parser = Parser.CreateParser(new StringReader(buffered), reader.Options.EffectiveMaxDepth, sourceName);
        var stream = YamlStream.Load(new EventReader(parser));
        if (stream.Count == 0 || stream[0].Contents is null)
        {
            return null;
        }

        var node = stream[0].Contents;
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
