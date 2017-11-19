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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SharpYaml.Events;

namespace SharpYaml.Model
{
    public class YamlMapping : YamlContainer, IDictionary<YamlElement, YamlElement>, IList<KeyValuePair<YamlElement, YamlElement>> {
        private MappingStart _mappingStart;
        private readonly MappingEnd _mappingEnd;

        private readonly List<YamlElement> _keys;
        private readonly Dictionary<YamlElement, YamlElement> _contents;

        public YamlMapping() {
            _mappingStart = new MappingStart();
            _mappingEnd = new MappingEnd();
            _keys = new List<YamlElement>();
            _contents = new Dictionary<YamlElement, YamlElement>();
        }

        YamlMapping(MappingStart mappingStart, MappingEnd mappingEnd, List<YamlElement> keys, Dictionary<YamlElement, YamlElement> contents) {
            this._mappingStart = mappingStart;
            this._mappingEnd = mappingEnd;
            this._keys = keys;
            this._contents = contents;
        }

        public MappingStart MappingStart {
            get => _mappingStart;
            set => _mappingStart = value;
        }

        public override string Anchor {
            get { return _mappingStart.Anchor; }
            set {
                _mappingStart = new MappingStart(value,
                    _mappingStart.Tag,
                    _mappingStart.IsImplicit,
                    _mappingStart.Style,
                    _mappingStart.Start,
                    _mappingStart.End);
            }
        }

        public override string Tag {
            get { return _mappingStart.Tag; }
            set {
                _mappingStart = new MappingStart(_mappingStart.Anchor,
                    value,
                    _mappingStart.IsImplicit,
                    _mappingStart.Style,
                    _mappingStart.Start,
                    _mappingStart.End);
            }
        }

        public override YamlStyle Style {
            get { return _mappingStart.Style; }
            set {
                _mappingStart = new MappingStart(_mappingStart.Anchor,
                    _mappingStart.Tag,
                    _mappingStart.IsImplicit,
                    value,
                    _mappingStart.Start,
                    _mappingStart.End);
            }
        }

        public override bool IsCanonical { get { return _mappingStart.IsCanonical; } }

        public override bool IsImplicit {
            get { return _mappingStart.IsImplicit; }
            set {
                _mappingStart = new MappingStart(_mappingStart.Anchor,
                    _mappingStart.Tag,
                    value,
                    _mappingStart.Style,
                    _mappingStart.Start,
                    _mappingStart.End);
            }
        }

        public static YamlMapping Load(EventReader eventReader) {
            var mappingStart = eventReader.Allow<MappingStart>();

            List<YamlElement> keys = new List<YamlElement>();
            Dictionary<YamlElement, YamlElement> contents = new Dictionary<YamlElement, YamlElement>();
            while (!eventReader.Accept<MappingEnd>()) {
                var key = ReadElement(eventReader);
                var value = ReadElement(eventReader);

                if (value == null)
                    throw new Exception();

                keys.Add(key);
                contents[key] = value;
            }

            var mappingEnd = eventReader.Allow<MappingEnd>();

            return new YamlMapping(mappingStart, mappingEnd, keys, contents);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return _mappingStart;

            foreach (var key in _keys) {
                foreach (var evnt in key.EnumerateEvents())
                    yield return evnt;

                foreach (var evnt in _contents[key].EnumerateEvents())
                    yield return evnt;
            }

            yield return _mappingEnd;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<YamlElement, YamlElement>> GetEnumerator() {
            return _contents.GetEnumerator();
        }

        void ICollection<KeyValuePair<YamlElement, YamlElement>>.Add(KeyValuePair<YamlElement, YamlElement> item) {
            Add(item.Key, item.Value);
        }

        public void Clear() {
            _contents.Clear();
            _keys.Clear();
        }

        bool ICollection<KeyValuePair<YamlElement, YamlElement>>.Contains(KeyValuePair<YamlElement, YamlElement> item) {
            return _contents.ContainsKey(item.Key);
        }

        void ICollection<KeyValuePair<YamlElement, YamlElement>>.CopyTo(KeyValuePair<YamlElement, YamlElement>[] array, int arrayIndex) {
            ((ICollection<KeyValuePair<YamlElement, YamlElement>>)_contents).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<YamlElement, YamlElement>>.Remove(KeyValuePair<YamlElement, YamlElement> item) {
            return Remove(item.Key);
        }

        public int Count { get { return _contents.Count; } }
        public bool IsReadOnly { get { return false; } }

        public void Add(YamlElement key, YamlElement value) {
            _contents.Add(key, value);
            _keys.Add(key);
        }

        public bool ContainsKey(YamlElement key) {
            return _contents.ContainsKey(key);
        }

        public bool Remove(YamlElement key) {
            if (_contents.Remove(key)) {
                _keys.Remove(key);
                return true;
            }

            return false;
        }

        public bool TryGetValue(YamlElement key, out YamlElement value) {
            return _contents.TryGetValue(key, out value);
        }

        public YamlElement this[YamlElement key] {
            get {
                if (!_contents.ContainsKey(key))
                    return null;
                return _contents[key];
            }
            set {
                if (!_contents.ContainsKey(key))
                    _keys.Add(key);

                _contents[key] = value;
            }
        }

        public YamlElement this[string key] {
            get { return this[new YamlValue(key)]; }
            set { this[new YamlValue(key)] = value; }
        }

        public ICollection<YamlElement> Keys { get { return _keys; } }
        public ICollection<YamlElement> Values { get { return _contents.Values; } }

        public int IndexOf(KeyValuePair<YamlElement, YamlElement> item) {
            return _keys.IndexOf(item.Key);
        }

        public void Insert(int index, KeyValuePair<YamlElement, YamlElement> item) {
            if (_contents.ContainsKey(item.Key))
                throw new Exception("Key already present.");

            _keys.Insert(index, item.Key);
            _contents[item.Key] = item.Value;
        }

        public void RemoveAt(int index) {
            var key = _keys[index];
            _keys.RemoveAt(index);
            _contents.Remove(key);
        }

        public KeyValuePair<YamlElement, YamlElement> this[int index] {
            get { return new KeyValuePair<YamlElement, YamlElement>(_keys[index], _contents[_keys[index]]); }
            set {
                if (_keys[index] != value.Key && _contents.ContainsKey(value.Key))
                    throw new Exception("Key already present at a different index.");

                if (_keys[index] != value.Key) {
                    _contents.Remove(_keys[index]);
                }

                _keys[index] = value.Key;
                _contents[value.Key] = value.Value;
            }
        }

        public override YamlNode DeepClone() {
            var mappingStartCopy = new MappingStart(_mappingStart.Anchor, 
                _mappingStart.Tag,
                _mappingStart.IsImplicit,
                _mappingStart.Style,
                _mappingStart.Start,
                _mappingStart.End);

            var mappingEndCopy = new MappingEnd(_mappingEnd.Start, _mappingEnd.End);

            return new YamlMapping(mappingStartCopy,
                mappingEndCopy,
                _keys.Select(k => (YamlElement) k.DeepClone()).ToList(),
                _contents.ToDictionary(kv => (YamlElement) kv.Key.DeepClone(), kv => (YamlElement) kv.Value.DeepClone()));
        }
    }
}