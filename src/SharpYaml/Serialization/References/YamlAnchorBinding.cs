// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using SharpYaml.Events;

namespace SharpYaml.Serialization.References;

/// <summary>
/// An anchored node: the range of its recorded parsing events, which an alias to it replays.
/// </summary>
internal sealed class YamlAnchorBinding
{
    public YamlAnchorBinding(string name, List<YamlRecordedEvent> events, int start)
    {
        Name = name;
        Events = events;
        Start = start;
        End = -1;
    }

    /// <summary>Gets the anchor name, without the <c>&amp;</c> prefix.</summary>
    public string Name { get; }

    /// <summary>Gets the recording of the reader that parsed the node; other anchored nodes share it.</summary>
    public List<YamlRecordedEvent> Events { get; }

    /// <summary>Gets the index of the first event of the node in <see cref="Events"/>.</summary>
    public int Start { get; }

    /// <summary>Gets or sets the index after the last event of the node, or -1 while the node is being parsed.</summary>
    public int End { get; set; }

    /// <summary>Gets a value indicating whether all the events of the node are recorded.</summary>
    public bool IsComplete => End >= 0;
}

/// <summary>
/// A parsing event recorded inside an anchored node.
/// </summary>
internal readonly struct YamlRecordedEvent
{
    public YamlRecordedEvent(ParsingEvent parsingEvent, YamlAnchorBinding? binding)
    {
        Event = parsingEvent;
        Binding = binding;
    }

    /// <summary>Gets the event as the parser produced it, anchors included.</summary>
    public ParsingEvent Event { get; }

    /// <summary>
    /// Gets the node the event defines when it is anchored, or the node an alias event refers to: the binding of the
    /// name where the alias appears, which later anchors of the same name do not change.
    /// </summary>
    public YamlAnchorBinding? Binding { get; }
}
