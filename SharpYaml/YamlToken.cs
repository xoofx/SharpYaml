using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpYaml.Events;
using SharpYaml.Schemas;
using SharpYaml.Serialization;
using SharpYaml.Serialization.Serializers;
using SharpYaml.Tokens;
using DocumentEnd = SharpYaml.Events.DocumentEnd;
using DocumentStart = SharpYaml.Events.DocumentStart;
using Scalar = SharpYaml.Events.Scalar;
using StreamEnd = SharpYaml.Events.StreamEnd;
using StreamStart = SharpYaml.Events.StreamStart;

namespace SharpYaml.YamlToken {
    public abstract class YamlToken {
        protected static YamlToken ReadToken(EventReader eventReader) {
            if (eventReader.Accept<MappingStart>())
                return YamlMapping.Load(eventReader);

            if (eventReader.Accept<SequenceStart>())
                return YamlSequence.Load(eventReader);

            if (eventReader.Accept<Scalar>())
                return YamlValue.Load(eventReader);

            return null;
        }

        public abstract IEnumerable<ParsingEvent> EnumerateEvents();

        public void WriteTo(TextWriter writer, bool suppressDocumentTags = false) {
            WriteTo(new Emitter(writer), suppressDocumentTags);
        }

        public void WriteTo(IEmitter emitter, bool suppressDocumentTags = false) {
            foreach (var evnt in EnumerateEvents()) {
                if (suppressDocumentTags) {
                    var document = evnt as DocumentStart;
                    if (document != null) {
                        document.Tags.Clear();
                    }
                }

                emitter.Emit(evnt);
            }
        }

        public T ToObject<T>(SerializerSettings settings = null) {
            var s = new Serializer(settings);

            var context = new SerializerContext(s, null) { Reader = new EventReader(new MemoryParser(EnumerateEvents())) };
            return (T)context.ReadYaml(null, typeof(T));
        }

        class MemoryEmitter : IEmitter {
            public List<ParsingEvent> Events = new List<ParsingEvent>();

            public void Emit(ParsingEvent evnt) {
                Events.Add(evnt);
            }
        }

        public static YamlToken FromObject(object value, SerializerSettings settings = null, Type expectedType = null) {
            var s = new Serializer(settings);

            var emitter = new MemoryEmitter();
            var context = new SerializerContext(s, null) { Writer = new WriterEventEmitter(emitter) };
            context.WriteYaml(value, expectedType);

            return ReadToken(new EventReader(new MemoryParser(emitter.Events)));
        }

        public abstract YamlToken DeepClone();
    }

    public abstract class YamlContainer : YamlToken {

    }

    public class YamlStream : YamlContainer, IList<YamlDocument> {
        private StreamStart streamStart;
        private StreamEnd streamEnd;

        private List<YamlDocument> documents;

        public YamlStream() {
            streamStart = new StreamStart();
            streamEnd = new StreamEnd();
            documents = new List<YamlDocument>();
        }

        YamlStream(StreamStart streamStart, StreamEnd streamEnd, List<YamlDocument> documents) {
            this.streamStart = streamStart;
            this.streamEnd = streamEnd;
            this.documents = documents;
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
            yield return streamStart;

            foreach (var document in documents) {
                foreach (var evnt in document.EnumerateEvents()) {
                    yield return evnt;
                }
            }


            yield return streamEnd;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<YamlDocument> GetEnumerator() {
            return documents.GetEnumerator();
        }

        public void Add(YamlDocument item) {
            documents.Add(item);
        }

        public void Clear() {
            documents.Clear();
        }

        public bool Contains(YamlDocument item) {
            return documents.Contains(item);
        }

        public void CopyTo(YamlDocument[] array, int arrayIndex) {
            documents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlDocument item) {
            return documents.Remove(item);
        }

        public int Count { get { return documents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlDocument item) {
            return documents.IndexOf(item);
        }

        public void Insert(int index, YamlDocument item) {
            documents.Insert(index, item);
        }

        public void RemoveAt(int index) {
            documents.RemoveAt(index);
        }

        public YamlDocument this[int index] {
            get { return documents[index]; }
            set { documents[index] = value; }
        }

        public override YamlToken DeepClone() {
            return new YamlStream(new StreamStart(streamStart.Start, streamStart.End),
                                  new StreamEnd(streamEnd.Start, streamEnd.End),
                                  documents.Select(d => (YamlDocument)d.DeepClone()).ToList());
        }
    }

    public class YamlDocument : YamlContainer {
        private DocumentStart documentStart;
        private DocumentEnd documentEnd;

        private YamlToken contents;

        public YamlDocument() {
            documentStart = new DocumentStart(null, new TagDirectiveCollection(), true);
            documentEnd = new DocumentEnd(true);
        }

        YamlDocument(DocumentStart documentStart, DocumentEnd documentEnd, YamlToken contents) {
            this.documentStart = documentStart;
            this.documentEnd = documentEnd;
            this.contents = contents;
        }

        public static YamlDocument Load(EventReader eventReader) {
            var documentStart = eventReader.Allow<DocumentStart>();

            YamlToken contents = null;
            if (eventReader.Accept<MappingStart>())
                contents = YamlMapping.Load(eventReader);
            else if (eventReader.Accept<SequenceStart>())
                contents = YamlSequence.Load(eventReader);
            else if (eventReader.Accept<Scalar>())
                contents = YamlValue.Load(eventReader);

            var documentEnd = eventReader.Allow<DocumentEnd>();

            return new YamlDocument(documentStart, documentEnd, contents);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return documentStart;

            foreach (var evnt in contents.EnumerateEvents()) {
                yield return evnt;
            }

            yield return documentEnd;
        }

        public DocumentStart DocumentStart1 {
            get => documentStart;
            set => documentStart = value;
        }

        public DocumentEnd DocumentEnd1 {
            get => documentEnd;
            set => documentEnd = value;
        }

        public YamlToken Contents {
            get { return contents; }
            set { contents = value; }
        }

        public override YamlToken DeepClone() {
            var documentVersionCopy = documentStart.Version == null
                ? null
                : new VersionDirective(documentStart.Version.Version, documentStart.Version.Start, documentStart.Version.End);

            var documentTagsCopy = documentStart.Tags == null ? null : new TagDirectiveCollection(documentStart.Tags);

            var documentStartCopy = new DocumentStart(documentVersionCopy, documentTagsCopy, documentStart.IsImplicit,
                                documentStart.Start, documentStart.End);

            var documentEndCopy = new DocumentEnd(documentEnd.IsImplicit, documentEnd.Start, documentEnd.End);

            return new YamlDocument(documentStartCopy, documentEndCopy, Contents?.DeepClone());
        }
    }

    public class YamlSequence : YamlContainer, IList<YamlToken> {
        private SequenceStart sequenceStart;
        private SequenceEnd sequenceEnd;

        private List<YamlToken> contents;

        public YamlSequence() {
            sequenceStart = new SequenceStart();
            sequenceEnd = new SequenceEnd();
            contents = new List<YamlToken>();
        }

        YamlSequence(SequenceStart sequenceStart, SequenceEnd sequenceEnd, List<YamlToken> contents) {
            this.sequenceStart = sequenceStart;
            this.sequenceEnd = sequenceEnd;
            this.contents = contents;
        }

        public SequenceStart SequenceStart {
            get => sequenceStart;
            set => sequenceStart = value;
        }

        public static YamlSequence Load(EventReader eventReader) {
            var sequenceStart = eventReader.Allow<SequenceStart>();

            var contents = new List<YamlToken>();
            while (!eventReader.Accept<SequenceEnd>()) {
                var item = ReadToken(eventReader);
                if (item != null)
                    contents.Add(item);
            }

            var sequenceEnd = eventReader.Allow<SequenceEnd>();

            return new YamlSequence(sequenceStart, sequenceEnd, contents);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return sequenceStart;

            foreach (var item in contents) {
                foreach (var evnt in item.EnumerateEvents()) {
                    yield return evnt;
                }
            }

            yield return sequenceEnd;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<YamlToken> GetEnumerator() {
            return contents.GetEnumerator();
        }

        public void Add(YamlToken item) {
            contents.Add(item);
        }

        public void Clear() {
            contents.Clear();
        }

        public bool Contains(YamlToken item) {
            return contents.Contains(item);
        }

        public void CopyTo(YamlToken[] array, int arrayIndex) {
            contents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlToken item) {
            return contents.Remove(item);
        }

        public int Count { get { return contents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlToken item) {
            return contents.IndexOf(item);
        }

        public void Insert(int index, YamlToken item) {
            contents.Insert(index, item);
        }

        public void RemoveAt(int index) {
            contents.RemoveAt(index);
        }

        public YamlToken this[int index] {
            get { return contents[index]; }
            set { contents[index] = value; }
        }

        public override YamlToken DeepClone() {
            var sequenceStartCopy = new SequenceStart(sequenceStart.Anchor, 
                                                      sequenceStart.Tag, 
                                                      sequenceStart.IsImplicit, 
                                                      sequenceStart.Style, 
                                                      sequenceStart.Start, 
                                                      sequenceStart.End);

            var sequenceEndCopy = new SequenceEnd(sequenceEnd.Start, sequenceEnd.End);

            return new YamlSequence(sequenceStartCopy, sequenceEndCopy, contents.Select(c => c.DeepClone()).ToList());
        }
    }

    public class YamlMapping : YamlContainer, IDictionary<YamlToken, YamlToken>, IList<KeyValuePair<YamlToken, YamlToken>> {
        private MappingStart mappingStart;
        private MappingEnd mappingEnd;

        private List<YamlToken> keys;
        private Dictionary<YamlToken, YamlToken> contents;

        public YamlMapping() {
            mappingStart = new MappingStart();
            mappingEnd = new MappingEnd();
            keys = new List<YamlToken>();
            contents = new Dictionary<YamlToken, YamlToken>();
        }

        YamlMapping(MappingStart mappingStart, MappingEnd mappingEnd, List<YamlToken> keys, Dictionary<YamlToken, YamlToken> contents) {
            this.mappingStart = mappingStart;
            this.mappingEnd = mappingEnd;
            this.keys = keys;
            this.contents = contents;
        }

        public MappingStart MappingStart {
            get => mappingStart;
            set => mappingStart = value;
        }

        public static YamlMapping Load(EventReader eventReader) {
            var mappingStart = eventReader.Allow<MappingStart>();

            List<YamlToken> keys = new List<YamlToken>();
            Dictionary<YamlToken, YamlToken> contents = new Dictionary<YamlToken, YamlToken>();
            while (!eventReader.Accept<MappingEnd>()) {
                var key = ReadToken(eventReader);
                var value = ReadToken(eventReader);

                if (value == null)
                    throw new Exception();

                keys.Add(key);
                contents[key] = value;
            }

            var mappingEnd = eventReader.Allow<MappingEnd>();

            return new YamlMapping(mappingStart, mappingEnd, keys, contents);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return mappingStart;

            foreach (var key in keys) {
                foreach (var evnt in key.EnumerateEvents())
                    yield return evnt;

                foreach (var evnt in contents[key].EnumerateEvents())
                    yield return evnt;
            }

            yield return mappingEnd;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<YamlToken, YamlToken>> GetEnumerator() {
            return contents.GetEnumerator();
        }

        void ICollection<KeyValuePair<YamlToken, YamlToken>>.Add(KeyValuePair<YamlToken, YamlToken> item) {
            Add(item.Key, item.Value);
        }

        public void Clear() {
            contents.Clear();
            keys.Clear();
        }

        bool ICollection<KeyValuePair<YamlToken, YamlToken>>.Contains(KeyValuePair<YamlToken, YamlToken> item) {
            return contents.ContainsKey(item.Key);
        }

        void ICollection<KeyValuePair<YamlToken, YamlToken>>.CopyTo(KeyValuePair<YamlToken, YamlToken>[] array, int arrayIndex) {
            ((ICollection<KeyValuePair<YamlToken, YamlToken>>)contents).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<YamlToken, YamlToken>>.Remove(KeyValuePair<YamlToken, YamlToken> item) {
            return Remove(item.Key);
        }

        public int Count { get { return contents.Count; } }
        public bool IsReadOnly { get { return false; } }

        public void Add(YamlToken key, YamlToken value) {
            contents.Add(key, value);
            keys.Add(key);
        }

        public bool ContainsKey(YamlToken key) {
            return contents.ContainsKey(key);
        }

        public bool Remove(YamlToken key) {
            if (contents.Remove(key)) {
                keys.Remove(key);
                return true;
            }

            return false;
        }

        public bool TryGetValue(YamlToken key, out YamlToken value) {
            return contents.TryGetValue(key, out value);
        }

        public YamlToken this[YamlToken key] {
            get {
                if (!contents.ContainsKey(key))
                    return null;
                return contents[key];
            }
            set {
                if (!contents.ContainsKey(key))
                    keys.Add(key);

                contents[key] = value;
            }
        }

        public YamlToken this[string key] {
            get { return this[new YamlValue(key)]; }
            set { this[new YamlValue(key)] = value; }
        }

        public ICollection<YamlToken> Keys { get { return keys; } }
        public ICollection<YamlToken> Values { get { return contents.Values; } }

        public int IndexOf(KeyValuePair<YamlToken, YamlToken> item) {
            return keys.IndexOf(item.Key);
        }

        public void Insert(int index, KeyValuePair<YamlToken, YamlToken> item) {
            if (contents.ContainsKey(item.Key))
                throw new Exception("Key already present.");

            keys.Insert(index, item.Key);
            contents[item.Key] = item.Value;
        }

        public void RemoveAt(int index) {
            var key = keys[index];
            keys.RemoveAt(index);
            contents.Remove(key);
        }

        public KeyValuePair<YamlToken, YamlToken> this[int index] {
            get { return new KeyValuePair<YamlToken, YamlToken>(keys[index], contents[keys[index]]); }
            set {
                if (keys[index] != value.Key && contents.ContainsKey(value.Key))
                    throw new Exception("Key already present at a different index.");

                if (keys[index] != value.Key) {
                    contents.Remove(keys[index]);
                }

                keys[index] = value.Key;
                contents[value.Key] = value.Value;
            }
        }

        public override YamlToken DeepClone() {
            var mappingStartCopy = new MappingStart(mappingStart.Anchor, 
                                                    mappingStart.Tag,
                                                    mappingStart.IsImplicit,
                                                    mappingStart.Style,
                                                    mappingStart.Start,
                                                    mappingStart.End);

            var mappingEndCopy = new MappingEnd(mappingEnd.Start, mappingEnd.End);

            return new YamlMapping(mappingStartCopy,
                                   mappingEndCopy,
                                   keys.Select(k => k.DeepClone()).ToList(),
                                   contents.ToDictionary(kv => kv.Key.DeepClone(), kv => kv.Value.DeepClone()));
        }
    }

    public class YamlValue : YamlToken {
        private Scalar scalar;

        YamlValue(Scalar scalar) {
            this.scalar = scalar;
        }

        public YamlValue(object value, IYamlSchema schema = null) {
            var valueString = PrimitiveSerializer.ConvertValue(value);
            if (schema == null)
                schema = new CoreSchema();

            scalar = new Scalar(schema.GetDefaultTag(value.GetType()), valueString);
        }

        public static YamlValue Load(EventReader eventReader) {
            var scalar = eventReader.Allow<Scalar>();

            return new YamlValue(scalar);
        }

        public override IEnumerable<ParsingEvent> EnumerateEvents() {
            yield return scalar;
        }

        protected bool Equals(YamlValue other) {
            return Equals(scalar.Value, other.scalar.Value);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((YamlValue)obj);
        }

        public override int GetHashCode() {
            return (scalar != null ? scalar.Value.GetHashCode() : 0);
        }

        public override YamlToken DeepClone() {
            return new YamlValue(new Scalar(scalar.Anchor,
                                            scalar.Tag,
                                            scalar.Value,
                                            scalar.Style,
                                            scalar.IsPlainImplicit,
                                            scalar.IsQuotedImplicit,
                                            scalar.Start,
                                            scalar.End));
        }
    }
}
