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
    /// <summary>Represents the Yaml Stream.</summary>
    public class YamlStream : YamlNode, IList<YamlDocument>
    {
        private readonly List<YamlDocument> _documents;

        /// <summary>Initializes a new instance of this type.</summary>
        public YamlStream(YamlNodeTracker? tracker = null)
        {
            StreamStart = new StreamStart();
            StreamEnd = new StreamEnd();
            _documents = new List<YamlDocument>();
            Tracker = tracker;
        }

        YamlStream(StreamStart streamStart, StreamEnd streamEnd, List<YamlDocument> documents, YamlNodeTracker? tracker = null)
        {
            this.StreamStart = streamStart;
            this.StreamEnd = streamEnd;

            if (tracker == null)
                _documents = documents;
            else
            {
                _documents = new List<YamlDocument>();

                Tracker = tracker;

                foreach (var document in documents)
                    Add(document);
            }
        }

        internal StreamStart StreamStart { get; }
        internal StreamEnd StreamEnd { get; }

        /// <summary>Loads data.</summary>
        public static YamlStream Load(TextReader stream, YamlNodeTracker? tracker = null)
        {
            return Load(new EventReader(Parser.CreateParser(stream)), tracker);
        }

        /// <summary>Loads data.</summary>
        public static YamlStream Load(EventReader eventReader, YamlNodeTracker? tracker = null)
        {
            var streamStart = eventReader.Allow<StreamStart>();

            var documents = new List<YamlDocument>();
            while (!eventReader.Accept<StreamEnd>())
                documents.Add(YamlDocument.Load(eventReader, tracker));

            var streamEnd = eventReader.Allow<StreamEnd>();

            return new YamlStream(streamStart, streamEnd, documents, tracker);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Gets enumerator.</summary>
        public IEnumerator<YamlDocument> GetEnumerator()
        {
            return _documents.GetEnumerator();
        }

        /// <summary>Adds an item.</summary>
        public void Add(YamlDocument item)
        {
            _documents.Add(item);

            if (Tracker != null)
            {
                item.Tracker = Tracker;
                Tracker.OnStreamAddDocument(this, item, _documents.Count - 1, null);
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

                for (var index = 0; index < _documents.Count; index++)
                {
                    var item = _documents[index];
                    item.Tracker = value;
                    Tracker.OnStreamAddDocument(this, item, index, null);
                }
            }
        }

        /// <summary>Removes all elements from the collection.</summary>
        public void Clear()
        {
            var copy = Tracker == null ? null : new List<YamlDocument>(_documents);

            _documents.Clear();

            if (Tracker != null)
            {
                for (int i = copy.Count; i >= 0; i--)
                    Tracker.OnStreamRemoveDocument(this, copy[i], i, null);
            }
        }

        /// <summary>Determines whether a value exists.</summary>
        public bool Contains(YamlDocument item)
        {
            return _documents.Contains(item);
        }

        /// <summary>Copies the elements to an array starting at the specified index.</summary>
        public void CopyTo(YamlDocument[] array, int arrayIndex)
        {
            _documents.CopyTo(array, arrayIndex);
        }

        /// <summary>Removes an item.</summary>
        public bool Remove(YamlDocument item)
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
        public int Count { get { return _documents.Count; } }

        /// <summary>Gets a value indicating whether is Read Only.</summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>Gets the zero-based index of the specified item.</summary>
        public int IndexOf(YamlDocument item)
        {
            return _documents.IndexOf(item);
        }

        /// <summary>Inserts an item at the specified index.</summary>
        public void Insert(int index, YamlDocument item)
        {
            _documents.Insert(index, item);

            if (Tracker != null)
            {
                item.Tracker = Tracker;

                ICollection<YamlDocument>? nextDocuments = null;
                if (index < _documents.Count - 1)
                    nextDocuments = _documents.Skip(index + 1).ToArray();

                Tracker.OnStreamAddDocument(this, item, index, nextDocuments);
            }
        }

        /// <summary>Removes at.</summary>
        public void RemoveAt(int index)
        {
            var oldValue = _documents[index];

            _documents.RemoveAt(index);

            if (Tracker != null)
            {
                IEnumerable<YamlDocument>? nextDocuments = null;
                if (index < _documents.Count)
                    nextDocuments = _documents.Skip(index);

                Tracker.OnStreamRemoveDocument(this, oldValue, index, nextDocuments);
            }
        }

        /// <summary>Gets or sets an element at the specified index.</summary>
        public YamlDocument this[int index]
        {
            get { return _documents[index]; }
            set
            {
                var oldValue = _documents[index];

                _documents[index] = value;

                if (Tracker != null)
                {
                    value.Tracker = Tracker;
                    Tracker.OnStreamDocumentChanged(this, index, oldValue, value);
                }
            }
        }

        /// <summary>Creates a deep clone of the current value.</summary>
        public override YamlNode DeepClone(YamlNodeTracker? tracker = null)
        {
            var documentsClone = new List<YamlDocument>(_documents.Count);
            for (var i = 0; i < _documents.Count; i++)
                documentsClone.Add((YamlDocument)_documents[i].DeepClone());

            return new YamlStream(StreamStart, StreamEnd, documentsClone, tracker);
        }
    }
}
