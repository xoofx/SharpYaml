// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using SharpYaml.Events;

namespace SharpYaml.Serialization.References;

/// <summary>
/// The anchors of the YAML read by one <see cref="YamlReader"/>: records the parsing events of anchored nodes and
/// replays them in place of the aliases that refer to them.
/// </summary>
/// <remarks>
/// <para>
/// An alias is replayed as a copy of its anchored node, with the anchors of the copy removed, so converters never see
/// an alias by default. Under <see cref="YamlReferenceHandling.Preserve"/>, an alias whose anchored node registered an
/// object is reported as an alias instead, for <see cref="YamlReader.TryReadAlias(out object?)"/> to resolve; so is an
/// anchored node inside a copy, when its object is registered.
/// </para>
/// <para>
/// Nested aliases are recorded as aliases, not as their expansion, so the recording grows with the document and not
/// with the expansion. Every replayed node counts against <see cref="YamlSerializerOptions.MaxAliasExpansion"/>.
/// Anchors are scoped to their document.
/// </para>
/// </remarks>
internal sealed class YamlAnchorTable
{
    private readonly YamlAliasScope _scope;
    private readonly bool _isFragment;
    private Dictionary<string, YamlAnchorBinding>? _bindings;
    private List<YamlRecordedEvent>? _events;
    private OpenNode[] _open = [];
    private int _openCount;
    private int _openDepth;
    private ReplayFrame[] _frames = [];
    private int _frameCount;

    /// <summary>Creates the table of a reader over a whole YAML stream.</summary>
    public YamlAnchorTable(YamlAliasScope scope)
    {
        _scope = scope;
    }

    private YamlAnchorTable(YamlAliasScope scope, Dictionary<string, YamlAnchorBinding>? bindings)
    {
        _scope = scope;
        _bindings = bindings;
        _isFragment = true;
    }

    public YamlAliasScope Scope => _scope;

    /// <summary>Gets a value indicating whether the last event returned by <see cref="Read"/> comes from a replayed alias.</summary>
    public bool IsReplayed { get; private set; }

    /// <summary>Gets the anchored node of the alias last returned by <see cref="Read"/>.</summary>
    public YamlAnchorBinding? CurrentAliasTarget { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the alias last returned by <see cref="Read"/> has no registered object yet: the
    /// caller decides with <see cref="ResolvePendingAlias"/> whether it is reported or replayed.
    /// </summary>
    public bool IsPendingAlias { get; private set; }

    /// <summary>
    /// Captures the anchors bound now, for a reader created later over YAML buffered from this point
    /// (<see cref="CreateFragment"/>): its aliases then refer to the nodes they referred to here, even when the buffered
    /// YAML anchors the same name again further on.
    /// </summary>
    public Dictionary<string, YamlAnchorBinding>? CaptureBindings()
        => _bindings is { Count: > 0 } ? new Dictionary<string, YamlAnchorBinding>(_bindings, StringComparer.Ordinal) : null;

    /// <summary>Creates the table of a reader over YAML buffered from this reader, which shares its scope.</summary>
    /// <param name="bindings">The anchors captured by <see cref="CaptureBindings"/>, owned by the new table.</param>
    public YamlAnchorTable CreateFragment(Dictionary<string, YamlAnchorBinding>? bindings) => new(_scope, bindings);

    /// <summary>
    /// Reads the next node event: from the alias being replayed, else from <paramref name="parser"/>, skipping stream and
    /// document events. Returns <see langword="null"/> at the end of the stream.
    /// </summary>
    /// <param name="parser">The parser of the reader.</param>
    /// <param name="passAliasesThrough">
    /// <see langword="true"/> to return the aliases of the parser as they are, as when buffering a node to text that
    /// another reader parses again; the aliases of a replayed node are still expanded.
    /// </param>
    /// <exception cref="YamlException">An alias is unknown or recursive, or aliases exceeded their expansion budget.</exception>
    /// <param name="skipAliasExpansion">Returns even replayed aliases without expansion while skipping a value.</param>
    public ParsingEvent? Read(IParser parser, bool passAliasesThrough, bool skipAliasExpansion = false)
    {
        while (true)
        {
            ParsingEvent? next;
            YamlAnchorBinding? binding;
            bool isFirstOfFrame = false;
            int frameIndex = _frameCount - 1;
            if (frameIndex >= 0)
            {
                ref var frame = ref _frames[frameIndex];
                if (frame.Position == frame.Binding.End)
                {
                    _frameCount--;
                    _frames[frameIndex] = default;
                    continue;
                }

                isFirstOfFrame = frame.Position == frame.Binding.Start;
                var recorded = frame.Binding.Events[frame.Position++];
                next = recorded.Event;
                binding = recorded.Binding;
                IsReplayed = true;
            }
            else
            {
                if (!parser.MoveNext())
                {
                    IsReplayed = false;
                    return null;
                }

                next = parser.Current;
                if (next is null)
                {
                    continue;
                }

                switch (next.Kind)
                {
                    case EventType.YAML_STREAM_START_EVENT:
                    case EventType.YAML_STREAM_END_EVENT:
                        continue;

                    case EventType.YAML_DOCUMENT_START_EVENT:
                    case EventType.YAML_DOCUMENT_END_EVENT:
                        if (!_isFragment)
                        {
                            ResetDocument();
                        }

                        continue;
                }

                // An event the parser reuses has no anchor and is not an alias: it only matters to a recording.
                binding = next.IsReused && _openCount == 0 ? null : Record(next, passAliasesThrough);
                IsReplayed = false;
            }

            CurrentAliasTarget = null;
            IsPendingAlias = false;
            if (next.Kind == EventType.YAML_ALIAS_EVENT)
            {
                var alias = (AnchorAlias)next;
                if (skipAliasExpansion || (!IsReplayed && passAliasesThrough))
                {
                    CurrentAliasTarget = binding;
                    return alias;
                }

                // Record resolved the binding of every alias it recorded.
                CurrentAliasTarget = binding!;
                // Resolve only when a converter observes the token; Skip can discard it without replaying it.
                IsPendingAlias = true;
                return alias;
            }

            if (!IsReplayed)
            {
                return next;
            }

            if (!skipAliasExpansion && next is not (MappingEnd or SequenceEnd))
            {
                _scope.ConsumeExpansion(next);
            }

            if (next is not NodeEvent { Anchor: { Length: > 0 } anchor } anchored)
            {
                return next;
            }

            // A nested anchored node whose object is registered keeps its identity: report an alias and skip its events.
            if (!isFirstOfFrame && binding is not null && IsRegistered(binding))
            {
                _frames[frameIndex].Position = binding.End;
                CurrentAliasTarget = binding;
                return new AnchorAlias(anchor, anchored.Start, anchored.End);
            }

            return WithoutAnchor(anchored);
        }
    }

    /// <summary>
    /// Decides what the pending alias last returned by <see cref="Read"/> stands for: returns <see langword="true"/>
    /// when the object of its anchored node is now registered, so that the alias is reported, and otherwise replays the
    /// node, whose first event the next <see cref="Read"/> returns.
    /// </summary>
    /// <param name="alias">The alias event.</param>
    /// <exception cref="YamlException">The alias refers to a node that contains it and has no registered object.</exception>
    public bool ResolvePendingAlias(AnchorAlias alias)
    {
        var target = CurrentAliasTarget!;
        IsPendingAlias = false;
        if (IsRegistered(target))
        {
            if (IsReplayed)
            {
                _scope.ConsumeExpansion(alias);
            }

            return true;
        }

        CurrentAliasTarget = null;
        Expand(target, alias);
        return false;
    }

    /// <summary>
    /// Replays the anchored node of the alias last returned by <see cref="Read"/>, for a caller that cannot use the
    /// object registered for it: the next <see cref="Read"/> returns the first event of the node.
    /// </summary>
    /// <param name="alias">The alias event.</param>
    /// <exception cref="YamlException">The alias is recursive or unknown.</exception>
    public void ExpandCurrentAlias(AnchorAlias alias)
    {
        var target = CurrentAliasTarget ?? Resolve(alias);
        CurrentAliasTarget = null;
        Expand(target, alias);
    }

    private YamlAnchorBinding Resolve(AnchorAlias alias)
    {
        if (_bindings is not null && _bindings.TryGetValue(alias.Value, out var binding))
        {
            return binding;
        }

        throw new YamlException(_scope.SourceName, alias.Start, alias.End, $"Found an alias '*{alias.Value}' referencing an unknown anchor.");
    }

    /// <summary>Gets whether the object of <paramref name="binding"/> is registered: the name is still bound to that node and its converter registered an object.</summary>
    private bool IsRegistered(YamlAnchorBinding binding)
        => _scope.References is { } references
            && _bindings is not null
            && _bindings.TryGetValue(binding.Name, out var current)
            && ReferenceEquals(current, binding)
            && references.Contains(binding.Name);

    private void Expand(YamlAnchorBinding target, AnchorAlias alias)
    {
        if (!target.IsComplete || IsReplaying(target))
        {
            var reason = _scope.References is null
                ? "Set YamlSerializerOptions.ReferenceHandling to YamlReferenceHandling.Preserve to deserialize recursive references."
                : "Its object is not available before the node is fully read, as for a type created by a constructor or an immutable collection.";
            throw new YamlException(_scope.SourceName, alias.Start, alias.End, $"The alias '*{alias.Value}' refers to a node that contains it. {reason}");
        }

        if (_frameCount == _frames.Length)
        {
            Array.Resize(ref _frames, Math.Max(4, _frames.Length * 2));
        }

        _frames[_frameCount++] = new ReplayFrame(target);
    }

    private bool IsReplaying(YamlAnchorBinding binding)
    {
        for (var i = 0; i < _frameCount; i++)
        {
            if (ReferenceEquals(_frames[i].Binding, binding))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records an event of the parser: binds the anchor of an anchored node, resolves the binding of an alias and keeps
    /// the events of the anchored nodes being parsed. Returns the binding the event defines or refers to.
    /// </summary>
    private YamlAnchorBinding? Record(ParsingEvent parsingEvent, bool passAliasesThrough)
    {
        YamlAnchorBinding? binding = null;
        if (parsingEvent is AnchorAlias alias)
        {
            binding = Resolve(alias);
        }
        else if (parsingEvent is NodeEvent { Anchor: { Length: > 0 } anchor })
        {
            // An anchored event is never a reused one: see IReusedEventSource.
            var events = _events ??= new List<YamlRecordedEvent>();
            binding = new YamlAnchorBinding(anchor, events, events.Count);
            events.Add(new YamlRecordedEvent(parsingEvent, binding));
            (_bindings ??= new Dictionary<string, YamlAnchorBinding>(StringComparer.Ordinal))[anchor] = binding;

            // The name now denotes this node: forget the object of the previous one before the new one registers its own.
            // A node buffered to text is parsed again by another reader, which does so.
            if (!passAliasesThrough)
            {
                _scope.References?.Remove(anchor);
            }

            if (parsingEvent is Scalar)
            {
                binding.End = events.Count;
            }
            else
            {
                PushOpen(binding);
            }

            return binding;
        }

        if (_openCount == 0)
        {
            return binding;
        }

        // The recording outlives the event when the parser reuses it.
        parsingEvent = parsingEvent.Detach();
        _events!.Add(new YamlRecordedEvent(parsingEvent, binding));
        if (parsingEvent is MappingStart or SequenceStart)
        {
            _openDepth++;
        }
        else if (parsingEvent is MappingEnd or SequenceEnd)
        {
            ref var top = ref _open[_openCount - 1];
            if (top.Depth == _openDepth)
            {
                top.Binding.End = _events.Count;
                top = default;
                _openCount--;
            }

            _openDepth--;
        }

        return binding;
    }

    private void PushOpen(YamlAnchorBinding binding)
    {
        if (_openCount == _open.Length)
        {
            Array.Resize(ref _open, Math.Max(4, _open.Length * 2));
        }

        _openDepth++;
        _open[_openCount++] = new OpenNode(binding, _openDepth);
    }

    private void ResetDocument()
    {
        _bindings?.Clear();
        _events = null;
        Array.Clear(_open, 0, _openCount);
        _openCount = 0;
        _openDepth = 0;
        _scope.References?.Clear();
    }

    private static NodeEvent WithoutAnchor(NodeEvent anchored) => anchored switch
    {
        Scalar scalar => new Scalar(null, scalar.Tag, scalar.Value, scalar.Style, scalar.IsPlainImplicit, scalar.IsQuotedImplicit, scalar.Start, scalar.End),
        MappingStart mapping => new MappingStart(null, mapping.Tag, mapping.IsImplicit, mapping.Style, mapping.Start, mapping.End),
        SequenceStart sequence => new SequenceStart(null, sequence.Tag, sequence.IsImplicit, sequence.Style, sequence.Start, sequence.End),
        _ => anchored,
    };

    private struct OpenNode
    {
        public OpenNode(YamlAnchorBinding binding, int depth)
        {
            Binding = binding;
            Depth = depth;
        }

        public YamlAnchorBinding Binding;
        public int Depth;
    }

    private struct ReplayFrame
    {
        public ReplayFrame(YamlAnchorBinding binding)
        {
            Binding = binding;
            Position = binding.Start;
        }

        public YamlAnchorBinding Binding;
        public int Position;
    }
}
