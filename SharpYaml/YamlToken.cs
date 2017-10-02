using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        protected static YamlElement ReadElement(EventReader eventReader) {
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
            var events = EnumerateEvents().ToList();

            // Emitter will throw an exception if we attempt to use it without
            // starting StremStart and DocumentStart events.
            if (!(events[0] is StreamStart))
                events.Insert(0, new StreamStart());

            if (!(events[1] is DocumentStart))
                events.Insert(1, new DocumentStart());

            foreach (var evnt in events) {
                if (suppressDocumentTags) {
                    var document = evnt as DocumentStart;
                    if (document != null && document.Tags != null) {
                        document.Tags.Clear();
                    }
                }

                emitter.Emit(evnt);
            }
        }

        public override string ToString() {
            var sb = new StringBuilder();
            WriteTo(new StringWriter(sb), true);
            return sb.ToString().Trim();
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

        public static YamlElement FromObject(object value, SerializerSettings settings = null, Type expectedType = null) {
            var s = new Serializer(settings);

            var emitter = new MemoryEmitter();
            var context = new SerializerContext(s, null) { Writer = new WriterEventEmitter(emitter) };
            context.WriteYaml(value, expectedType);

            return ReadElement(new EventReader(new MemoryParser(emitter.Events)));
        }

        public abstract YamlToken DeepClone();
    }

    public abstract class YamlElement : YamlToken {
        public abstract string Anchor { get; set; }
        public abstract string Tag { get; set; }
        public abstract bool IsCanonical { get; }
    }

    public abstract class YamlContainer : YamlElement {
        public abstract YamlStyle Style { get; set; }
        public abstract bool IsImplicit { get; set; }
    }

    public class YamlStream : YamlToken, IList<YamlDocument> {
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

    public class YamlDocument : YamlToken {
        private DocumentStart documentStart;
        private DocumentEnd documentEnd;

        private YamlElement contents;

        public YamlDocument() {
            documentStart = new DocumentStart(null, new TagDirectiveCollection(), true);
            documentEnd = new DocumentEnd(true);
        }

        YamlDocument(DocumentStart documentStart, DocumentEnd documentEnd, YamlElement contents) {
            this.documentStart = documentStart;
            this.documentEnd = documentEnd;
            this.contents = contents;
        }

        public static YamlDocument Load(EventReader eventReader) {
            var documentStart = eventReader.Allow<DocumentStart>();

            var contents = ReadElement(eventReader);

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

        public DocumentStart DocumentStart {
            get => documentStart;
            set => documentStart = value;
        }

        public DocumentEnd DocumentEnd {
            get => documentEnd;
            set => documentEnd = value;
        }

        public YamlElement Contents {
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

            return new YamlDocument(documentStartCopy, documentEndCopy, (YamlElement) Contents?.DeepClone());
        }
    }

    public class YamlSequence : YamlContainer, IList<YamlElement> {
        private SequenceStart sequenceStart;
        private SequenceEnd sequenceEnd;

        private List<YamlElement> contents;

        public YamlSequence() {
            sequenceStart = new SequenceStart();
            sequenceEnd = new SequenceEnd();
            contents = new List<YamlElement>();
        }

        YamlSequence(SequenceStart sequenceStart, SequenceEnd sequenceEnd, List<YamlElement> contents) {
            this.sequenceStart = sequenceStart;
            this.sequenceEnd = sequenceEnd;
            this.contents = contents;
        }

        public SequenceStart SequenceStart {
            get => sequenceStart;
            set => sequenceStart = value;
        }

        public override string Anchor {
            get { return sequenceStart.Anchor; }
            set {
                sequenceStart = new SequenceStart(value,
                                                 sequenceStart.Tag,
                                                 sequenceStart.IsImplicit,
                                                 sequenceStart.Style,
                                                 sequenceStart.Start,
                                                 sequenceStart.End);
            }
        }

        public override string Tag {
            get { return sequenceStart.Tag; }
            set {
                sequenceStart = new SequenceStart(sequenceStart.Anchor,
                                                  value,
                                                  sequenceStart.IsImplicit,
                                                  sequenceStart.Style,
                                                  sequenceStart.Start,
                                                  sequenceStart.End);
            }
        }

        public override YamlStyle Style {
            get { return sequenceStart.Style; }
            set {
                sequenceStart = new SequenceStart(sequenceStart.Anchor,
                                                  sequenceStart.Tag,
                                                  sequenceStart.IsImplicit,
                                                  value,
                                                  sequenceStart.Start,
                                                  sequenceStart.End);
            }
        }

        public override bool IsCanonical { get { return sequenceStart.IsCanonical; } }

        public override bool IsImplicit {
            get { return sequenceStart.IsImplicit; }
            set {
                sequenceStart = new SequenceStart(sequenceStart.Anchor,
                                                  sequenceStart.Tag,
                                                  value,
                                                  sequenceStart.Style,
                                                  sequenceStart.Start,
                                                  sequenceStart.End);
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

        public IEnumerator<YamlElement> GetEnumerator() {
            return contents.GetEnumerator();
        }

        public void Add(YamlElement item) {
            contents.Add(item);
        }

        public void Clear() {
            contents.Clear();
        }

        public bool Contains(YamlElement item) {
            return contents.Contains(item);
        }

        public void CopyTo(YamlElement[] array, int arrayIndex) {
            contents.CopyTo(array, arrayIndex);
        }

        public bool Remove(YamlElement item) {
            return contents.Remove(item);
        }

        public int Count { get { return contents.Count; } }

        public bool IsReadOnly { get { return false; } }

        public int IndexOf(YamlElement item) {
            return contents.IndexOf(item);
        }

        public void Insert(int index, YamlElement item) {
            contents.Insert(index, item);
        }

        public void RemoveAt(int index) {
            contents.RemoveAt(index);
        }

        public YamlElement this[int index] {
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

            return new YamlSequence(sequenceStartCopy, sequenceEndCopy, contents.Select(c => (YamlElement) c.DeepClone()).ToList());
        }
    }

    public class YamlMapping : YamlContainer, IDictionary<YamlElement, YamlElement>, IList<KeyValuePair<YamlElement, YamlElement>> {
        private MappingStart mappingStart;
        private MappingEnd mappingEnd;

        private List<YamlElement> keys;
        private Dictionary<YamlElement, YamlElement> contents;

        public YamlMapping() {
            mappingStart = new MappingStart();
            mappingEnd = new MappingEnd();
            keys = new List<YamlElement>();
            contents = new Dictionary<YamlElement, YamlElement>();
        }

        YamlMapping(MappingStart mappingStart, MappingEnd mappingEnd, List<YamlElement> keys, Dictionary<YamlElement, YamlElement> contents) {
            this.mappingStart = mappingStart;
            this.mappingEnd = mappingEnd;
            this.keys = keys;
            this.contents = contents;
        }

        public MappingStart MappingStart {
            get => mappingStart;
            set => mappingStart = value;
        }

        public override string Anchor {
            get { return mappingStart.Anchor; }
            set {
                mappingStart = new MappingStart(value,
                                                mappingStart.Tag,
                                                mappingStart.IsImplicit,
                                                mappingStart.Style,
                                                mappingStart.Start,
                                                mappingStart.End);
            }
        }

        public override string Tag {
            get { return mappingStart.Tag; }
            set {
                mappingStart = new MappingStart(mappingStart.Anchor,
                                                value,
                                                mappingStart.IsImplicit,
                                                mappingStart.Style,
                                                mappingStart.Start,
                                                mappingStart.End);
            }
        }

        public override YamlStyle Style {
            get { return mappingStart.Style; }
            set {
                mappingStart = new MappingStart(mappingStart.Anchor,
                                                mappingStart.Tag,
                                                mappingStart.IsImplicit,
                                                value,
                                                mappingStart.Start,
                                                mappingStart.End);
            }
        }

        public override bool IsCanonical { get { return mappingStart.IsCanonical; } }

        public override bool IsImplicit {
            get { return mappingStart.IsImplicit; }
            set {
                mappingStart = new MappingStart(mappingStart.Anchor,
                                                mappingStart.Tag,
                                                value,
                                                mappingStart.Style,
                                                mappingStart.Start,
                                                mappingStart.End);
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

        public IEnumerator<KeyValuePair<YamlElement, YamlElement>> GetEnumerator() {
            return contents.GetEnumerator();
        }

        void ICollection<KeyValuePair<YamlElement, YamlElement>>.Add(KeyValuePair<YamlElement, YamlElement> item) {
            Add(item.Key, item.Value);
        }

        public void Clear() {
            contents.Clear();
            keys.Clear();
        }

        bool ICollection<KeyValuePair<YamlElement, YamlElement>>.Contains(KeyValuePair<YamlElement, YamlElement> item) {
            return contents.ContainsKey(item.Key);
        }

        void ICollection<KeyValuePair<YamlElement, YamlElement>>.CopyTo(KeyValuePair<YamlElement, YamlElement>[] array, int arrayIndex) {
            ((ICollection<KeyValuePair<YamlElement, YamlElement>>)contents).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<YamlElement, YamlElement>>.Remove(KeyValuePair<YamlElement, YamlElement> item) {
            return Remove(item.Key);
        }

        public int Count { get { return contents.Count; } }
        public bool IsReadOnly { get { return false; } }

        public void Add(YamlElement key, YamlElement value) {
            contents.Add(key, value);
            keys.Add(key);
        }

        public bool ContainsKey(YamlElement key) {
            return contents.ContainsKey(key);
        }

        public bool Remove(YamlElement key) {
            if (contents.Remove(key)) {
                keys.Remove(key);
                return true;
            }

            return false;
        }

        public bool TryGetValue(YamlElement key, out YamlElement value) {
            return contents.TryGetValue(key, out value);
        }

        public YamlElement this[YamlElement key] {
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

        public YamlElement this[string key] {
            get { return this[new YamlValue(key)]; }
            set { this[new YamlValue(key)] = value; }
        }

        public ICollection<YamlElement> Keys { get { return keys; } }
        public ICollection<YamlElement> Values { get { return contents.Values; } }

        public int IndexOf(KeyValuePair<YamlElement, YamlElement> item) {
            return keys.IndexOf(item.Key);
        }

        public void Insert(int index, KeyValuePair<YamlElement, YamlElement> item) {
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

        public KeyValuePair<YamlElement, YamlElement> this[int index] {
            get { return new KeyValuePair<YamlElement, YamlElement>(keys[index], contents[keys[index]]); }
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
                                   keys.Select(k => (YamlElement) k.DeepClone()).ToList(),
                                   contents.ToDictionary(kv => (YamlElement) kv.Key.DeepClone(), kv => (YamlElement) kv.Value.DeepClone()));
        }
    }

    public class YamlValue : YamlElement {
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

        public override string Anchor {
            get { return scalar.Anchor; }
            set {
                scalar = new Scalar(value,
                                    scalar.Tag,
                                    scalar.Value,
                                    scalar.Style,
                                    scalar.IsPlainImplicit,
                                    scalar.IsQuotedImplicit,
                                    scalar.Start,
                                    scalar.End);
            }
        }

        public override string Tag {
            get { return scalar.Tag; }
            set {
                scalar = new Scalar(scalar.Anchor,
                                    value,
                                    scalar.Value,
                                    scalar.Style,
                                    scalar.IsPlainImplicit,
                                    scalar.IsQuotedImplicit,
                                    scalar.Start,
                                    scalar.End);
            }
        }

        public ScalarStyle Style {
            get { return scalar.Style; }
            set {
                scalar = new Scalar(scalar.Anchor,
                                    scalar.Tag,
                                    scalar.Value,
                                    value,
                                    scalar.IsPlainImplicit,
                                    scalar.IsQuotedImplicit,
                                    scalar.Start,
                                    scalar.End);
            }
        }

        public override bool IsCanonical { get { return scalar.IsCanonical; } }

        public bool IsPlainImplicit {
            get { return scalar.IsPlainImplicit; }
            set {
                scalar = new Scalar(scalar.Anchor,
                                    scalar.Tag,
                                    scalar.Value,
                                    scalar.Style,
                                    value,
                                    scalar.IsQuotedImplicit,
                                    scalar.Start,
                                    scalar.End);
            }
        }

        public bool IsQuotedImplicit {
            get { return scalar.IsQuotedImplicit; }
            set {
                scalar = new Scalar(scalar.Anchor,
                    scalar.Tag,
                    scalar.Value,
                    scalar.Style,
                    scalar.IsPlainImplicit,
                    value,
                    scalar.Start,
                    scalar.End);
            }
        }
    }
}
