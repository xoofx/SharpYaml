using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SharpYaml.Model;
using YamlStream = SharpYaml.Model.YamlStream;

namespace SharpYaml.Tests {
    public class YamlNodeTrackerTest {
        [Test]
        public void DeserializeTest() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var childrenAdded = 0;

            var tracker = new YamlNodeTracker();

            tracker.TrackerEvent += (sender, args) => {
                if (args.EventType == TrackerEventType.MappingPairAdded ||
                    args.EventType == TrackerEventType.SequenceElementAdded ||
                    args.EventType == TrackerEventType.StreamDocumentAdded)
                    childrenAdded++;
            };

            var fileStream = new StreamReader(file);
            YamlStream.Load(fileStream, tracker);

            Assert.AreEqual(9, childrenAdded);
        }

        [Test]
        public void ValueSetTest() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test4.yaml");

            var childrenAdded = 0;

            var tracker = new YamlNodeTracker();

            tracker.TrackerEvent += (sender, args) => {
                if (args.EventType == TrackerEventType.MappingPairAdded ||
                    args.EventType == TrackerEventType.SequenceElementAdded ||
                    args.EventType == TrackerEventType.StreamDocumentAdded)
                    childrenAdded++;
            };

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream, tracker);

            Assert.AreEqual(3, childrenAdded);

            ScalarValueChanged valueChanged = null;
            tracker.TrackerEvent += (sender, args) => {
                if (args is ScalarValueChanged)
                    valueChanged = (ScalarValueChanged) args;
            };
            ((YamlValue) stream[0].Contents).Value = "a silly scalar";

            Assert.AreEqual("a scalar", valueChanged.OldValue);
            Assert.AreEqual("a silly scalar", valueChanged.NewValue);
            Assert.AreEqual(stream[0].Contents, valueChanged.Node);
            Assert.AreEqual(1, valueChanged.ParentPaths.Count);
            Assert.AreEqual(new Model.Path(stream, new []{ new ChildIndex(0, false), new ChildIndex(-1, false) }),  valueChanged.ParentPaths[0]);
        }


        class SubscriberHandler {
            public int ACalls;
            public int BCalls;
            public int CCalls;

            public void A(TrackerEventArgs args) {
                ACalls++;
            }

            public void B(TrackerEventArgs args) {
                BCalls++;
            }

            public void C(TrackerEventArgs args) {
                CCalls++;
            }
        }

        [Test]
        public void DisposeTest()
        {
            System.WeakReference GetWeakRef()
            {
                // In .NET versions higher than 3.5, the parents dictionary is replaced with
                // ConditionalWeakTable, allowing tracked YAML nodes to be freed properly.
                var file = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("SharpYaml.Tests.files.test4.yaml");

                var childrenAdded = 0;

                var tracker = new YamlNodeTracker();

                tracker.TrackerEvent += (sender, args) =>
                {
                    if (args.EventType == TrackerEventType.MappingPairAdded ||
                        args.EventType == TrackerEventType.SequenceElementAdded ||
                        args.EventType == TrackerEventType.StreamDocumentAdded)
                        childrenAdded++;
                };

                var fileStream = new StreamReader(file);
                var stream = YamlStream.Load(fileStream, tracker);

                return new System.WeakReference(stream);

            }

            var weakRef = GetWeakRef();
            System.GC.Collect();
            System.GC.WaitForFullGCComplete();
            System.GC.WaitForPendingFinalizers();
            Assert.IsFalse(weakRef.IsAlive);
        }

        [Test]
        public void SubscriberTest() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test12.yaml");

            var childrenAdded = 0;

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var yaml = YamlStream.Load(fileStream, tracker);

            var mapping1 = (YamlMapping) ((YamlSequence) yaml[0].Contents)[1];
            var mapping2 = (YamlMapping) ((YamlSequence)yaml[0].Contents)[2];

            var handler = new SubscriberHandler();

            tracker.Subscribe(handler, null, "A");
            tracker.Subscribe(handler, tracker.GetPaths(yaml[0].Contents)[0], "B");
            tracker.Subscribe(handler, tracker.GetPaths(mapping1)[0], "C");
            
            mapping1["key 1"] = new YamlValue("Bla");

            Assert.AreEqual(1, handler.ACalls);
            Assert.AreEqual(1, handler.BCalls);
            Assert.AreEqual(1, handler.CCalls);

            mapping2[0] = new KeyValuePair<YamlElement, YamlElement>(new YamlValue("K"), new YamlValue("V"));

            Assert.AreEqual(2, handler.ACalls);
            Assert.AreEqual(2, handler.BCalls);
            Assert.AreEqual(1, handler.CCalls);

            ((YamlSequence)yaml[0].Contents).Add(new YamlValue("5"));

            Assert.AreEqual(3, handler.ACalls);
            Assert.AreEqual(3, handler.BCalls);
            Assert.AreEqual(1, handler.CCalls);
        }

        [Test]
        public void AddPairTest() {
            var tracker = new YamlNodeTracker();
            var stream = new YamlStream(tracker);
            stream.Add(new YamlDocument());
            stream[0].Contents = new YamlMapping();

            TrackerEventArgs receivedArgs = null;
            tracker.TrackerEvent += (sender, args) => {
                receivedArgs = args;
            };

            ((YamlMapping) stream[0].Contents)["A"] = new YamlValue(5);

            Assert.IsTrue(receivedArgs is MappingPairAdded);
            Assert.AreEqual(TrackerEventType.MappingPairAdded, ((MappingPairAdded) receivedArgs).EventType);
            Assert.AreEqual(0, ((MappingPairAdded)receivedArgs).Index);
            Assert.AreEqual(new Model.Path(stream, new [] { new ChildIndex(0, false), new ChildIndex(-1, false) }), ((MappingPairAdded)receivedArgs).ParentPaths[0]);
            Assert.AreEqual("A", ((MappingPairAdded)receivedArgs).Child.Key.ToString());
            Assert.AreEqual("5", ((MappingPairAdded) receivedArgs).Child.Value.ToString());
        }


        [Test]
        public void TrackerAssignmentTest() {
            var tracker = new YamlNodeTracker();
            var stream = new YamlStream(tracker);

            var document = new YamlDocument();
            var sequence = new YamlSequence();
            document.Contents = sequence;

            var mapping = new YamlMapping();
            sequence.Add(mapping);

            var key = new YamlValue("key");
            var value = new YamlValue("value");

            var eventList = new List<TrackerEventArgs>();

            tracker.TrackerEvent += (sender, args) => eventList.Add(args);

            mapping[key] = value;

            Assert.IsNull(document.Tracker);
            Assert.IsNull(sequence.Tracker);
            Assert.IsNull(mapping.Tracker);
            Assert.IsNull(key.Tracker);
            Assert.IsNull(value.Tracker);

            stream.Add(document);

            Assert.AreEqual(tracker, document.Tracker);
            Assert.AreEqual(tracker, sequence.Tracker);
            Assert.AreEqual(tracker, mapping.Tracker);
            Assert.AreEqual(tracker, key.Tracker);
            Assert.AreEqual(tracker, value.Tracker);

            Assert.AreEqual(4, eventList.Count);
            Assert.IsTrue(eventList[0] is MappingPairAdded);
            Assert.IsTrue(eventList[1] is SequenceElementAdded);
            Assert.IsTrue(eventList[2] is DocumentContentsChanged);
            Assert.IsTrue(eventList[3] is StreamDocumentAdded);

            eventList.Clear();

            var key2 = new YamlValue("key2");
            var value2 = new YamlValue("value2");
            mapping[key2] = value2;

            Assert.AreEqual(1, eventList.Count);
            Assert.IsTrue(eventList[0] is MappingPairAdded);
        }
    }
}