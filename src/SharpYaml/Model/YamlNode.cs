// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpYaml.Events;
using DocumentStart = SharpYaml.Events.DocumentStart;
using Scalar = SharpYaml.Events.Scalar;
using StreamStart = SharpYaml.Events.StreamStart;

namespace SharpYaml.Model;

/// <summary>Represents the Yaml Node.</summary>
public abstract class YamlNode
{
    /// <summary>Gets or sets tracker.</summary>
    public virtual YamlNodeTracker? Tracker { get; internal set; }

    /// <summary>Reads the next YAML element from the event stream.</summary>
    protected static YamlElement? ReadElement(EventReader eventReader, YamlNodeTracker? tracker = null)
    {
        return ReadElement(eventReader, tracker, anchors: null);
    }

    internal static YamlElement? ReadElement(EventReader eventReader, YamlNodeTracker? tracker, Dictionary<string, YamlElement>? anchors)
    {
        if (eventReader.Accept<MappingStart>())
        {
            var mapping = YamlMapping.Load(eventReader, tracker, anchors);
            RegisterAnchor(mapping, anchors);
            return mapping;
        }

        if (eventReader.Accept<SequenceStart>())
        {
            var sequence = YamlSequence.Load(eventReader, tracker, anchors);
            RegisterAnchor(sequence, anchors);
            return sequence;
        }

        if (eventReader.Accept<Scalar>())
        {
            var value = YamlValue.Load(eventReader, tracker);
            RegisterAnchor(value, anchors);
            return value;
        }

        if (eventReader.Accept<AnchorAlias>())
        {
            var alias = eventReader.Allow<AnchorAlias>();

            if (anchors == null || !anchors.TryGetValue(alias.Value, out var anchored))
            {
                throw new YamlException(alias.Start, alias.End, FormattableString.Invariant($"Found an alias '*{alias.Value}' referencing an unknown anchor."));
            }

            // The model API does not currently preserve aliases as a distinct node type.
            // We materialize a copy so that writing the model back out does not emit duplicate anchors.
            var clone = (YamlElement)anchored.DeepClone(tracker: null);
            clone.Anchor = null;
            return clone;
        }

        return null;
    }

    private static void RegisterAnchor(YamlElement element, Dictionary<string, YamlElement>? anchors)
    {
        if (anchors == null)
        {
            return;
        }

        var anchor = element.Anchor;
        if (!string.IsNullOrEmpty(anchor))
        {
            anchors[anchor] = element;
        }
    }

    /// <summary>Enumerates parsing events for this YAML node.</summary>
    public IEnumerable<ParsingEvent> EnumerateEvents()
    {
        return new YamlNodeEventEnumerator(this);
    }

    /// <summary>Writes to.</summary>
    public void WriteTo(TextWriter writer, bool suppressDocumentTags = false)
    {
        WriteTo(new Emitter(writer), suppressDocumentTags);
    }

    /// <summary>Writes to.</summary>
    public void WriteTo(IEmitter emitter, bool suppressDocumentTags = false)
    {
        var events = EnumerateEvents().ToList();

        // Emitter will throw an exception if we attempt to use it without
        // starting StremStart and DocumentStart events.
        if (events[0] is not StreamStart)
            events.Insert(0, new StreamStart());

        if (events[1] is not DocumentStart)
            events.Insert(1, new DocumentStart());

        foreach (var evnt in events)
        {
            if (suppressDocumentTags)
            {
                if (evnt is DocumentStart document && document.Tags != null)
                {
                    document.Tags.Clear();
                }
            }

            emitter.Emit(evnt);
        }
    }

    /// <summary>Returns a string representation of the current instance.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        WriteTo(new StringWriter(sb), true);
        return sb.ToString().Trim();
    }

    /// <summary>Converts this node to an instance of <typeparamref name="T"/>.</summary>
    public T? ToObject<T>(YamlSerializerOptions? options = null)
    {
        return (T?)ToObject(typeof(T), options);
    }

    /// <summary>Converts this YAML node to an object.</summary>
    public object? ToObject(Type type, YamlSerializerOptions? options = null)
    {
        ArgumentGuard.ThrowIfNull(type);
        return YamlSerializer.Deserialize(ToString(), type, options);
    }

    /// <summary>Creates a YAML element from an object.</summary>
    public static YamlElement FromObject(object value, YamlSerializerOptions? options = null, Type? expectedType = null)
    {
        ArgumentGuard.ThrowIfNull(value);
        var yaml = expectedType is null
            ? YamlSerializer.Serialize(value, options)
            : YamlSerializer.Serialize(value, expectedType, options);
        var stream = YamlStream.Load(new StringReader(yaml));
        if (stream.Count == 0 || stream[0].Contents is null)
        {
            throw new YamlException("Unable to materialize a YAML element from the serialized object graph.");
        }

        return stream[0].Contents;
    }

    /// <summary>Creates a deep clone of the current value.</summary>
    public abstract YamlNode DeepClone(YamlNodeTracker? tracker = null);
}