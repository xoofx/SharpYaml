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
using System.IO;
using System.Linq;
using SharpYaml.Events;

namespace SharpYaml.Model
{
    public class YamlStream : YamlNode, IList<YamlDocument> {
        private readonly StreamStart _streamStart;
        private readonly StreamEnd _streamEnd;

        private readonly List<YamlDocument> _documents;

        public YamlStream() {
            _streamStart = new StreamStart();
            _streamEnd = new StreamEnd();
            _documents = new List<YamlDocument>();
        }

        YamlStream(StreamStart streamStart, StreamEnd streamEnd, List<YamlDocument> documents) {
            this._streamStart = streamStart;
            this._streamEnd = streamEnd;
            this._documents = documents;
        }

        public static YamlStream Load(TextReader stream) {
            return Load(new EventReader(new Parser(stream)));
        }

        public static YamlStream Load(EventReader eventReader) {
            var streamStart = eventReader.Allow<StreamStart>();

            var documents = new List<YamlDocument>();
            while (!eventReader.Accept<StreamEnd>())
                documents.Add(YamlDocument.Load(eventReader));

            var streamEnd = eventReader.Allow<StreamEnd>();

            return new YamlStream(streamStart, streamEnd, documents);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return _streamStart;

            foreach (var document in _documents) {
                foreach (var evnt in document.EnumerateEvents()) {
                    yield return evnt;
                }
            }


            yield return _streamEnd;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<YamlDocument> GetEnumerator() {
            return _documents.GetEnumerator();
        }

        public void Add(YamlDocument item) {
            _documents.Add(item);
        }

        public void Clear() {
            _documents.Clear();
        }

        public bool Contains(YamlDocument item) {
            return _documents.Contains(item);
        }

        public void CopyTo(YamlDocument[] array, int arrayIndex) {
            _documents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlDocument item) {
            return _documents.Remove(item);
        }

        public int Count { get { return _documents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlDocument item) {
            return _documents.IndexOf(item);
        }

        public void Insert(int index, YamlDocument item) {
            _documents.Insert(index, item);
        }

        public void RemoveAt(int index) {
            _documents.RemoveAt(index);
        }

        public YamlDocument this[int index] {
            get { return _documents[index]; }
            set { _documents[index] = value; }
        }

        public override YamlNode DeepClone() {
            return new YamlStream(new StreamStart(_streamStart.Start, _streamStart.End),
                new StreamEnd(_streamEnd.Start, _streamEnd.End),
                _documents.Select(d => (YamlDocument)d.DeepClone()).ToList());
        }
    }
}