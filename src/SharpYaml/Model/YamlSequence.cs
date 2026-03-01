// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SharpYaml.Events;

namespace SharpYaml.Model;

/// <summary>Represents the Yaml Sequence.</summary>
public class YamlSequence : YamlContainer, IList<YamlElement>
{
    private SequenceStart _sequenceStart;
    private readonly List<YamlElement> _contents;

    /// <summary>Initializes a new instance of this type.</summary>
    public YamlSequence()
    {
        _sequenceStart = new SequenceStart();
        SequenceEnd = new SequenceEnd();
        _contents = new List<YamlElement>();
    }

    YamlSequence(SequenceStart sequenceStart, SequenceEnd sequenceEnd, List<YamlElement> contents, YamlNodeTracker? tracker)
    {
        if (tracker == null)
            _contents = contents;
        else
        {
            _contents = new List<YamlElement>();

            Tracker = tracker;

            foreach (var item in contents)
                Add(item);
        }

        SequenceStart = sequenceStart;

        this.SequenceEnd = sequenceEnd;
    }

    /// <summary>Gets sequence Start.</summary>
    public SequenceStart SequenceStart
    {
        get => _sequenceStart;
        set
        {
            _sequenceStart = value;

            if (Tracker != null)
                Tracker.OnSequenceStartChanged(this, _sequenceStart, value);
        }
    }

    internal SequenceEnd SequenceEnd { get; }

    /// <summary>Gets anchor.</summary>
    public override string? Anchor
    {
        get { return _sequenceStart.Anchor; }
        set
        {
            SequenceStart = new SequenceStart(value,
                _sequenceStart.Tag,
                _sequenceStart.IsImplicit,
                _sequenceStart.Style,
                _sequenceStart.Start,
                _sequenceStart.End);
        }
    }

    /// <summary>Gets tag.</summary>
    public override string? Tag
    {
        get { return _sequenceStart.Tag; }
        set
        {
            SequenceStart = new SequenceStart(_sequenceStart.Anchor,
                value,
                string.IsNullOrEmpty(value),
                _sequenceStart.Style,
                _sequenceStart.Start,
                _sequenceStart.End);
        }
    }

    /// <summary>Gets style.</summary>
    public override YamlStyle Style
    {
        get { return _sequenceStart.Style; }
        set
        {
            SequenceStart = new SequenceStart(_sequenceStart.Anchor,
                _sequenceStart.Tag,
                _sequenceStart.IsImplicit,
                value,
                _sequenceStart.Start,
                _sequenceStart.End);
        }
    }

    /// <summary>Gets a value indicating whether is Canonical.</summary>
    public override bool IsCanonical { get { return _sequenceStart.IsCanonical; } }

    /// <summary>Gets is Implicit.</summary>
    public override bool IsImplicit
    {
        get { return _sequenceStart.IsImplicit; }
        set
        {
            SequenceStart = new SequenceStart(_sequenceStart.Anchor,
                _sequenceStart.Tag,
                value,
                _sequenceStart.Style,
                _sequenceStart.Start,
                _sequenceStart.End);
        }
    }

    /// <summary>Loads data.</summary>
    public static YamlSequence Load(EventReader eventReader, YamlNodeTracker? tracker = null)
    {
        return Load(eventReader, tracker, anchors: null);
    }

    internal static YamlSequence Load(EventReader eventReader, YamlNodeTracker? tracker, Dictionary<string, YamlElement>? anchors)
    {
        var sequenceStart = eventReader.Allow<SequenceStart>();

        var contents = new List<YamlElement>();
        while (!eventReader.Accept<SequenceEnd>())
        {
            var item = ReadElement(eventReader, tracker, anchors);
            if (item != null)
                contents.Add(item);
        }

        var sequenceEnd = eventReader.Allow<SequenceEnd>();

        return new YamlSequence(sequenceStart, sequenceEnd, contents, tracker);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>Gets enumerator.</summary>
    public IEnumerator<YamlElement> GetEnumerator()
    {
        return _contents.GetEnumerator();
    }

    /// <summary>Adds an item.</summary>
    public void Add(YamlElement item)
    {
        _contents.Add(item);

        if (Tracker != null)
        {
            item.Tracker = Tracker;
            Tracker.OnSequenceAddElement(this, item, _contents.Count - 1, null);
        }
    }

    /// <summary>Gets tracker.</summary>
    public override YamlNodeTracker? Tracker
    {
        get { return base.Tracker; }
        internal set
        {
            if (Tracker == value)
                return;

            base.Tracker = value;

            for (var index = 0; index < _contents.Count; index++)
            {
                var item = _contents[index];
                item.Tracker = value;
                Tracker.OnSequenceAddElement(this, item, index, null);
            }
        }
    }

    /// <summary>Removes all elements from the collection.</summary>
    public void Clear()
    {
        var copy = Tracker == null ? null : new List<YamlElement>(_contents);

        _contents.Clear();

        if (Tracker != null)
        {
            for (int i = copy.Count - 1; i >= 0; i--)
                Tracker.OnSequenceRemoveElement(this, copy[i], i, null);
        }
    }

    /// <summary>Determines whether a value exists.</summary>
    public bool Contains(YamlElement item)
    {
        return _contents.Contains(item);
    }

    /// <summary>Copies the elements to an array starting at the specified index.</summary>
    public void CopyTo(YamlElement[] array, int arrayIndex)
    {
        _contents.CopyTo(array, arrayIndex);
    }

    /// <summary>Removes an item.</summary>
    public bool Remove(YamlElement item)
    {
        var index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }

        return false;
    }

    /// <summary>Gets count.</summary>
    public int Count { get { return _contents.Count; } }

    /// <summary>Gets a value indicating whether is Read Only.</summary>
    public bool IsReadOnly { get { return false; } }

    /// <summary>Gets the zero-based index of the specified item.</summary>
    public int IndexOf(YamlElement item)
    {
        return _contents.IndexOf(item);
    }

    /// <summary>Inserts an item at the specified index.</summary>
    public void Insert(int index, YamlElement item)
    {
        _contents.Insert(index, item);

        if (Tracker != null)
        {
            item.Tracker = Tracker;

            ICollection<YamlElement>? nextChildren = null;
            if (index < _contents.Count - 1)
                nextChildren = _contents.Skip(index + 1).ToArray();

            Tracker.OnSequenceAddElement(this, item, index, nextChildren);
        }
    }

    /// <summary>Removes at.</summary>
    public void RemoveAt(int index)
    {
        var oldValue = _contents[index];

        _contents.RemoveAt(index);

        if (Tracker != null)
        {
            IEnumerable<YamlElement>? nextChildren = null;
            if (index < _contents.Count)
                nextChildren = _contents.Skip(index);

            Tracker.OnSequenceRemoveElement(this, oldValue, index, nextChildren);
        }
    }

    /// <summary>Gets or sets an element at the specified index.</summary>
    public YamlElement this[int index]
    {
        get { return _contents[index]; }
        set
        {
            var oldValue = _contents[index];

            _contents[index] = value;

            if (Tracker != null)
            {
                value.Tracker = Tracker;
                Tracker.OnSequenceElementChanged(this, index, oldValue, value);
            }
        }
    }

    /// <summary>Creates a deep clone of the current value.</summary>
    public override YamlNode DeepClone(YamlNodeTracker? tracker = null)
    {
        var contentsClone = new List<YamlElement>(_contents.Count);
        for (var i = 0; i < _contents.Count; i++)
            contentsClone.Add((YamlElement)_contents[i].DeepClone());

        return new YamlSequence(_sequenceStart, SequenceEnd, contentsClone, tracker);
    }
}