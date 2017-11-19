// Copyright (c) SharpYaml - Alexandre Mutel
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

        YamlSequence(SequenceStart sequenceStart, SequenceEnd sequenceEnd, List<YamlElement> contents) {
            this._sequenceStart = sequenceStart;
            this._sequenceEnd = sequenceEnd;
            this._contents = contents;
        }

        public SequenceStart SequenceStart {
            get => _sequenceStart;
            set => _sequenceStart = value;
        }

        public override string Anchor {
            get { return _sequenceStart.Anchor; }
            set {
                _sequenceStart = new SequenceStart(value,
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
                _sequenceStart = new SequenceStart(_sequenceStart.Anchor,
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
                _sequenceStart = new SequenceStart(_sequenceStart.Anchor,
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
                _sequenceStart = new SequenceStart(_sequenceStart.Anchor,
                    _sequenceStart.Tag,
                    value,
                    _sequenceStart.Style,
                    _sequenceStart.Start,
                    _sequenceStart.End);
            }
        }

        public static YamlSequence Load(EventReader eventReader) {
            var sequenceStart = eventReader.Allow<SequenceStart>();

            var contents = new List<YamlElement>();
            while (!eventReader.Accept<SequenceEnd>()) {
                var item = ReadElement(eventReader);
                if (item != null)
                    contents.Add(item);
            }

            var sequenceEnd = eventReader.Allow<SequenceEnd>();

            return new YamlSequence(sequenceStart, sequenceEnd, contents);
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
        }

        public void Clear() {
            _contents.Clear();
        }

        public bool Contains(YamlElement item) {
            return _contents.Contains(item);
        }

        public void CopyTo(YamlElement[] array, int arrayIndex) {
            _contents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlElement item) {
            return _contents.Remove(item);
        }

        public int Count { get { return _contents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlElement item) {
            return _contents.IndexOf(item);
        }

        public void Insert(int index, YamlElement item) {
            _contents.Insert(index, item);
        }

        public void RemoveAt(int index) {
            _contents.RemoveAt(index);
        }

        public YamlElement this[int index] {
            get { return _contents[index]; }
            set { _contents[index] = value; }
        }

        public override YamlNode DeepClone() {
            var sequenceStartCopy = new SequenceStart(_sequenceStart.Anchor, 
                _sequenceStart.Tag, 
                _sequenceStart.IsImplicit, 
                _sequenceStart.Style, 
                _sequenceStart.Start, 
                _sequenceStart.End);

            var sequenceEndCopy = new SequenceEnd(_sequenceEnd.Start, _sequenceEnd.End);

            return new YamlSequence(sequenceStartCopy, sequenceEndCopy, _contents.Select(c => (YamlElement) c.DeepClone()).ToList());
        }
    }
}