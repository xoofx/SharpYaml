// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Events;

/// <summary>
/// Contains the behavior that is common between node events.
/// </summary>
public abstract class NodeEvent : ParsingEvent
{
    internal static bool IsValidAnchor(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsHighSurrogate(character) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
                continue;
            }
            // YAML 1.2.2 ns-anchor-char excludes whitespace and flow indicators.
            if (character is ' ' or '\t' or '\r' or '\n' or ',' or '[' or ']' or '{' or '}' || !Emitter.IsPrintable(character))
            {
                return false;
            }
        }
        return value.Length != 0;
    }

    /// <summary>
    /// Gets the anchor.
    /// </summary>
    /// <value></value>
    public string? Anchor { get; }

    /// <summary>
    /// Gets the tag.
    /// </summary>
    /// <value></value>
    public string? Tag { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is canonical.
    /// </summary>
    /// <value></value>
    public abstract bool IsCanonical { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeEvent"/> class.
    /// </summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="start">The start position of the event.</param>
    /// <param name="end">The end position of the event.</param>
    protected NodeEvent(string? anchor, string? tag, Mark start, Mark end)
        : base(start, end)
    {
        if (anchor != null)
        {
            if (anchor.Length == 0)
            {
                throw new ArgumentException("Anchor value must not be empty.", "anchor");
            }

            if (!IsValidAnchor(anchor))
            {
                throw new ArgumentException("Anchor value must contain non-space characters and no flow indicators.", "anchor");
            }
        }

        if (tag != null && tag.Length == 0)
        {
            throw new ArgumentException("Tag value must not be empty.", "tag");
        }

        this.Anchor = anchor;
        this.Tag = tag;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeEvent"/> class.
    /// </summary>
    protected NodeEvent(string? anchor, string? tag)
        : this(anchor, tag, Mark.Empty, Mark.Empty)
    {
    }
}
