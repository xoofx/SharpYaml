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

        public YamlStream(YamlNodeTracker tracker = null) {
            _streamStart = new StreamStart();
            _streamEnd = new StreamEnd();
            _documents = new List<YamlDocument>();
            Tracker = tracker;
        }

        YamlStream(StreamStart streamStart, StreamEnd streamEnd, List<YamlDocument> documents, YamlNodeTracker tracker = null) {
            this._streamStart = streamStart;
            this._streamEnd = streamEnd;

            if (tracker == null)
                _documents = documents;
            else {
                _documents = new List<YamlDocument>();

                Tracker = tracker;

                foreach (var document in documents)
                    Add(document);
            }
        }

        public static YamlStream Load(TextReader stream, YamlNodeTracker tracker = null) {
            return Load(new EventReader(new Parser(stream)), tracker);
        }

        public static YamlStream Load(EventReader eventReader, YamlNodeTracker tracker = null) {
            var streamStart = eventReader.Allow<StreamStart>();

            var documents = new List<YamlDocument>();
            while (!eventReader.Accept<StreamEnd>())
                documents.Add(YamlDocument.Load(eventReader, tracker));

            var streamEnd = eventReader.Allow<StreamEnd>();

            return new YamlStream(streamStart, streamEnd, documents, tracker);
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

            if (Tracker != null) {
                item.Tracker = Tracker;
                Tracker.OnStreamAddDocument(this, item, _documents.Count - 1);
            }
        }
        
        public override YamlNodeTracker Tracker {
            get { return base.Tracker; }
            internal set {
                if (Tracker == value)
                    return;

                base.Tracker = value;

                for (var index = 0; index < _documents.Count; index++) {
                    var item = _documents[index];
                    item.Tracker = value;
                    Tracker.OnStreamAddDocument(this, item, index);
                }
            }
        }

        public void Clear() {
            var copy = Tracker == null ? null : new List<YamlDocument>(_documents);

            _documents.Clear();

            if (Tracker != null) {
                for (int i = copy.Count; i >= 0; i--)
                    Tracker.OnStreamRemoveDocument(this, copy[i], i);
            }
        }

        public bool Contains(YamlDocument item) {
            return _documents.Contains(item);
        }

        public void CopyTo(YamlDocument[] array, int arrayIndex) {
            _documents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlDocument item) {
            var index = IndexOf(item);
            if (index >= 0) {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        public int Count { get { return _documents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlDocument item) {
            return _documents.IndexOf(item);
        }

        public void Insert(int index, YamlDocument item) {
            _documents.Insert(index, item);

            if (Tracker != null) {
                item.Tracker = Tracker;
                Tracker.OnStreamAddDocument(this, item, index);
            }
        }

        public void RemoveAt(int index) {
            var oldValue = _documents[index];

            _documents.RemoveAt(index);

            if (Tracker != null)
                Tracker.OnStreamRemoveDocument(this, oldValue, index);
        }

        public YamlDocument this[int index] {
            get { return _documents[index]; }
            set {
                var oldValue = _documents[index];

                _documents[index] = value;

                if (Tracker != null) {
                    value.Tracker = Tracker;
                    Tracker.OnStreamDocumentChanged(this, index, oldValue, value);
                }
            }
        }

        public override YamlNode DeepClone() {
            return new YamlStream(new StreamStart(_streamStart.Start, _streamStart.End),
                new StreamEnd(_streamEnd.Start, _streamEnd.End),
                _documents.Select(d => (YamlDocument)d.DeepClone()).ToList());
        }
    }
}