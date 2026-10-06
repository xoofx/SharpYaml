// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization;

/// <summary>
/// Per-deserialization state for unmapped member reporting. Created only when a callback or finalizer is configured,
/// and shared between a reader and the readers it creates for buffered (polymorphic) nodes.
/// </summary>
internal sealed class YamlUnmappedMemberSession
{
    private readonly Action<YamlUnmappedMember>? _callback;
    private readonly Action<IReadOnlyList<YamlUnmappedMember>>? _finalizer;
    private List<YamlUnmappedMember>? _members;

    private YamlUnmappedMemberSession(Action<YamlUnmappedMember>? callback, Action<IReadOnlyList<YamlUnmappedMember>>? finalizer)
    {
        _callback = callback;
        _finalizer = finalizer;
    }

    /// <summary>Anchors defined by the nodes reported so far, so that later reported nodes can alias them.</summary>
    public Dictionary<string, Model.YamlElement> Anchors { get; } = new();

    /// <summary>The path of the node most recently buffered by the reader; used to seed the path of the re-parse.</summary>
    public string? BufferedPath { get; set; }

    /// <summary>The discriminator property name of the node most recently buffered, which is not an unmapped member of the derived type.</summary>
    public string? BufferedDiscriminator { get; set; }

    public static YamlUnmappedMemberSession? Create(YamlSerializerOptions options)
    {
        var callback = options.UnmappedMemberCallback;
        var finalizer = options.UnmappedMembersFinalizer;
        return callback is null && finalizer is null ? null : new YamlUnmappedMemberSession(callback, finalizer);
    }

    public void Report(YamlUnmappedMember member)
    {
        _callback?.Invoke(member);
        if (_finalizer is not null)
        {
            (_members ??= new List<YamlUnmappedMember>()).Add(member);
        }
    }

    public void Complete()
    {
        var members = _members;
        if (_finalizer is null || members is null || members.Count == 0)
        {
            return;
        }

        _members = null;
        _finalizer(members);
    }
}
