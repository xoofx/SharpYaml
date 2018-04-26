﻿// Copyright (c) SharpYaml - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SharpYaml.Events;

namespace SharpYaml.Model
{
    public class YamlSequence : YamlContainer, IList<YamlElement> {
        private SequenceStart _sequenceStart;
        private readonly SequenceEnd _sequenceEnd;

        private readonly List<YamlElement> _contents;

        public YamlSequence() {
            _sequenceStart = new SequenceStart();
            _sequenceEnd = new SequenceEnd();
            _contents = new List<YamlElement>();
        }

        YamlSequence(SequenceStart sequenceStart, SequenceEnd sequenceEnd, List<YamlElement> contents, YamlNodeTracker tracker) {
            if (tracker == null)
                _contents = contents;
            else {
                _contents = new List<YamlElement>();

                Tracker = tracker;

                foreach (var item in contents)
                    Add(item);
            }

            SequenceStart = sequenceStart;
            
            this._sequenceEnd = sequenceEnd;
        }

        public SequenceStart SequenceStart {
            get => _sequenceStart;
            set {
                _sequenceStart = value;

                if (Tracker != null)
                    Tracker.OnSequenceStartChanged(this, _sequenceStart, value);
            }
        }

        public override string Anchor {
            get { return _sequenceStart.Anchor; }
            set {
                SequenceStart = new SequenceStart(value,
                    _sequenceStart.Tag,
                    _sequenceStart.IsImplicit,
                    _sequenceStart.Style,
                    _sequenceStart.Start,
                    _sequenceStart.End);
            }
        }

        public override string Tag {
            get { return _sequenceStart.Tag; }
            set {
                SequenceStart = new SequenceStart(_sequenceStart.Anchor,
                    value,
                    _sequenceStart.IsImplicit,
                    _sequenceStart.Style,
                    _sequenceStart.Start,
                    _sequenceStart.End);
            }
        }

        public override YamlStyle Style {
            get { return _sequenceStart.Style; }
            set {
                SequenceStart = new SequenceStart(_sequenceStart.Anchor,
                    _sequenceStart.Tag,
                    _sequenceStart.IsImplicit,
                    value,
                    _sequenceStart.Start,
                    _sequenceStart.End);
            }
        }

        public override bool IsCanonical { get { return _sequenceStart.IsCanonical; } }

        public override bool IsImplicit {
            get { return _sequenceStart.IsImplicit; }
            set {
                SequenceStart = new SequenceStart(_sequenceStart.Anchor,
                    _sequenceStart.Tag,
                    value,
                    _sequenceStart.Style,
                    _sequenceStart.Start,
                    _sequenceStart.End);
            }
        }

        public static YamlSequence Load(EventReader eventReader, YamlNodeTracker tracker = null) {
            var sequenceStart = eventReader.Allow<SequenceStart>();

            var contents = new List<YamlElement>();
            while (!eventReader.Accept<SequenceEnd>()) {
                var item = ReadElement(eventReader, tracker);
                if (item != null)
                    contents.Add(item);
            }

            var sequenceEnd = eventReader.Allow<SequenceEnd>();

            return new YamlSequence(sequenceStart, sequenceEnd, contents, tracker);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return _sequenceStart;

            foreach (var item in _contents) {
                foreach (var evnt in item.EnumerateEvents()) {
                    yield return evnt;
                }
            }

            yield return _sequenceEnd;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<YamlElement> GetEnumerator() {
            return _contents.GetEnumerator();
        }

        public void Add(YamlElement item) {
            _contents.Add(item);

            if (Tracker != null) {
                item.Tracker = Tracker;
                Tracker.OnSequenceAddElement(this, item, _contents.Count - 1);
            }
        }

        public override YamlNodeTracker Tracker {
            get { return base.Tracker; }
            internal set {
                base.Tracker = value;

                foreach (var item in _contents)
                    item.Tracker = value;
            }
        }

        public void Clear() {
            var copy = Tracker == null ? null : new List<YamlElement>(_contents);

            _contents.Clear();

            if (Tracker != null) {
                for (int i = copy.Count; i >= 0; i--)
                    Tracker.OnSequenceRemoveElement(this, copy[i], i);
            }
        }

        public bool Contains(YamlElement item) {
            return _contents.Contains(item);
        }

        public void CopyTo(YamlElement[] array, int arrayIndex) {
            _contents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlElement item) {
            var index = IndexOf(item);
            if (index >= 0) {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        public int Count { get { return _contents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlElement item) {
            return _contents.IndexOf(item);
        }

        public void Insert(int index, YamlElement item) {
            _contents.Insert(index, item);

            if (Tracker != null) {
                item.Tracker = Tracker;
                Tracker.OnSequenceAddElement(this, item, index);
            }
        }

        public void RemoveAt(int index) {
            var oldValue = _contents[index];

            _contents.RemoveAt(index);

            if (Tracker != null)
                Tracker.OnSequenceRemoveElement(this, oldValue, index);
        }

        public YamlElement this[int index] {
            get { return _contents[index]; }
            set {
                var oldValue = _contents[index];

                _contents[index] = value;

                if (Tracker != null) {
                    value.Tracker = Tracker;
                    Tracker.OnSequenceElementChanged(this, index, oldValue, value);
                }
            }
        }

        public override YamlNode DeepClone() {
            var sequenceStartCopy = new SequenceStart(_sequenceStart.Anchor, 
                _sequenceStart.Tag, 
                _sequenceStart.IsImplicit, 
                _sequenceStart.Style, 
                _sequenceStart.Start, 
                _sequenceStart.End);

            var sequenceEndCopy = new SequenceEnd(_sequenceEnd.Start, _sequenceEnd.End);

            return new YamlSequence(sequenceStartCopy, sequenceEndCopy, _contents.Select(c => (YamlElement) c.DeepClone()).ToList(), Tracker);
        }
    }
}