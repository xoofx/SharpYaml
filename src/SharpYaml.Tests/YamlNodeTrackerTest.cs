using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SharpYaml.Model;
using Path = SharpYaml.Model.Path;
using YamlStream = SharpYaml.Model.YamlStream;

namespace SharpYaml.Tests
{
    public class YamlNodeTrackerTest
    {
        [Test]
        public void DeserializeTest()
        {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

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
            YamlStream.Load(fileStream, tracker);

            Assert.AreEqual(9, childrenAdded);
        }

        [Test]
        public void ValueSetTest()
        {
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

            Assert.AreEqual(3, childrenAdded);

            ScalarValueChanged valueChanged = null;
            tracker.TrackerEvent += (sender, args) =>
            {
                if (args is ScalarValueChanged changed)
                    valueChanged = changed;
            };
            ((YamlValue)stream[0].Contents).Value = "a silly scalar";

            Assert.AreEqual("a scalar", valueChanged.OldValue);
            Assert.AreEqual("a silly scalar", valueChanged.NewValue);
            Assert.AreEqual(stream[0].Contents, valueChanged.Node);
            Assert.AreEqual(1, valueChanged.ParentPaths.Count);
            Assert.AreEqual(new Model.Path(stream, new[] { new ChildIndex(0, false), new ChildIndex(-1, false) }), valueChanged.ParentPaths[0]);
        }


        class SubscriberHandler
        {
            public int ACalls;
            public int BCalls;
            public int CCalls;

            public void A(TrackerEventArgs args)
            {
                ACalls++;
            }

            public void B(TrackerEventArgs args)
            {
                BCalls++;
            }

            public void C(TrackerEventArgs args)
            {
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
        public void SubscriberTest()
        {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test12.yaml");

            var childrenAdded = 0;

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var yaml = YamlStream.Load(fileStream, tracker);

            var mapping1 = (YamlMapping)((YamlSequence)yaml[0].Contents)[1];
            var mapping2 = (YamlMapping)((YamlSequence)yaml[0].Contents)[2];

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
        public void AddPairTest()
        {
            var tracker = new YamlNodeTracker();
            var stream = new YamlStream(tracker);
            stream.Add(new YamlDocument());
            stream[0].Contents = new YamlMapping();

            TrackerEventArgs receivedArgs = null;
            tracker.TrackerEvent += (sender, args) =>
            {
                receivedArgs = args;
            };

            ((YamlMapping)stream[0].Contents)["A"] = new YamlValue(5);

            Assert.IsTrue(receivedArgs is MappingPairAdded);
            Assert.AreEqual(TrackerEventType.MappingPairAdded, ((MappingPairAdded)receivedArgs).EventType);
            Assert.AreEqual(0, ((MappingPairAdded)receivedArgs).Index);
            Assert.AreEqual(new Path(stream, new[] { new ChildIndex(0, false), new ChildIndex(-1, false) }), ((MappingPairAdded)receivedArgs).ParentPaths[0]);
            Assert.AreEqual("A", ((MappingPairAdded)receivedArgs).Child.Key.ToString());
            Assert.AreEqual("5", ((MappingPairAdded)receivedArgs).Child.Value.ToString());
        }


        [Test]
        public void TrackerAssignmentTest()
        {
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

        [Test]
        public void UpdateMappingNextChildrenTest()
        {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream, tracker);
            var seq = (YamlSequence)((YamlMapping)stream[0].Contents)["a sequence"];

            var modifiedPath = new Path();
            tracker.TrackerEvent += (sender, args) => modifiedPath = args.ParentPaths[0];

            seq[0] = new YamlValue("New item");

            Assert.AreEqual(3, modifiedPath.Indices.Last().Index); // Index of the sequence starts at 3.

            // Deleting "a mapping" should update the index of "a sequence".
            ((YamlMapping)stream[0].Contents).Remove("a mapping");

            seq[0] = new YamlValue("New item 2");

            Assert.AreEqual(seq, modifiedPath.Resolve());
            Assert.AreEqual(2, modifiedPath.Indices.Last().Index); // Index of the sequence is now 2.

            ((YamlMapping)stream[0].Contents).Insert(1, new KeyValuePair<YamlElement, YamlElement>(new YamlValue("a mapping 1"), new YamlMapping()));
            ((YamlMapping)stream[0].Contents).Insert(1, new KeyValuePair<YamlElement, YamlElement>(new YamlValue("a mapping 2"), new YamlMapping()));

            seq[0] = new YamlValue("New item 3");

            Assert.AreEqual(seq, modifiedPath.Resolve());
            Assert.AreEqual(4, modifiedPath.Indices.Last().Index); // Index of the sequence is now 4.
        }


        [Test]
        public void UpdateSequenceNextChildrenTest()
        {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test10.yaml");

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream, tracker);
            var seq = (YamlSequence)((YamlSequence)stream[0].Contents)[2];

            var modifiedPath = new Path();
            tracker.TrackerEvent += (sender, args) => modifiedPath = args.ParentPaths[0];

            seq[0] = new YamlValue("New item");

            Assert.AreEqual(2, modifiedPath.Indices.Last().Index); // Index of the sequence starts at 2.

            // Deleting item 1 should update the index of item 2.
            ((YamlSequence)stream[0].Contents).RemoveAt(1);

            seq[0] = new YamlValue("New item 2");

            Assert.AreEqual(seq, modifiedPath.Resolve());
            Assert.AreEqual(1, modifiedPath.Indices.Last().Index); // Index of the sequence is now 1.

            ((YamlSequence)stream[0].Contents).Insert(1, new YamlValue("value 1"));
            ((YamlSequence)stream[0].Contents).Insert(1, new YamlValue("value 2"));

            seq[0] = new YamlValue("New item 3");

            Assert.AreEqual(seq, modifiedPath.Resolve());
            Assert.AreEqual(3, modifiedPath.Indices.Last().Index); // Index of the sequence is now 3.
        }


        [Test]
        public void UpdateStreamNextChildrenTest()
        {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test4.yaml");

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream, tracker);
            var val = (YamlValue)stream[2].Contents;

            var modifiedPath = new Path();
            tracker.TrackerEvent += (sender, args) =>
            {
                if (args.ParentPaths.Count > 0)
                    modifiedPath = args.ParentPaths[0];
            };

            val.Value = "Another value";

            Assert.AreEqual(2, modifiedPath.Indices[0].Index); // Index of the value starts at 2.

            // Deleting item 1 should update the index of item 2.
            stream.RemoveAt(1);

            val.Value = "A different value";

            Assert.AreEqual(val, modifiedPath.Resolve());
            Assert.AreEqual(1, modifiedPath.Indices[0].Index); // Index of the value is now 1.

            stream.Insert(1, new YamlDocument { Contents = new YamlValue("more values") });
            stream.Insert(1, new YamlDocument { Contents = new YamlValue("values for everybody") });

            val.Value = "Yet another value";

            Assert.AreEqual(val, modifiedPath.Resolve());
            Assert.AreEqual(3, modifiedPath.Indices[0].Index); // Index of the sequence is now 3.
        }

        [Test]
        public void UpdateSubscriberNextChildrenTest()
        {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream, tracker);
            var rootMapping = (YamlMapping)stream[0].Contents;
            var val = (YamlValue)rootMapping["a simple key"];
            var map = (YamlMapping)rootMapping["a mapping"];
            var seq = (YamlSequence)rootMapping["a sequence"];

            var subscriber = new SubscriberHandler();
            tracker.Subscribe(subscriber, tracker.GetPaths(seq)[0], "A");
            tracker.Subscribe(subscriber, tracker.GetPaths(map)[0], "B");
            tracker.Subscribe(subscriber, tracker.GetPaths(val)[0], "C");

            seq[0] = new YamlValue("New item");
            map["bla"] = new YamlValue("New item");
            val.Tag = "bla";

            Assert.AreEqual(1, subscriber.ACalls);
            Assert.AreEqual(1, subscriber.BCalls);
            Assert.AreEqual(1, subscriber.CCalls);

            rootMapping.Remove("a simple key");

            seq[0] = new YamlValue("New item 2");

            // Make sure we're still subscribed.
            Assert.AreEqual(2, subscriber.ACalls);
            Assert.AreEqual(1, subscriber.BCalls);

            map["bla"] = new YamlValue("New item 2");

            // Make sure we're still subscribed.
            Assert.AreEqual(2, subscriber.ACalls);
            Assert.AreEqual(2, subscriber.BCalls);

            // C should get unsubscribed and not get any seq or map changes.
            Assert.AreEqual(1, subscriber.CCalls);

            rootMapping.Insert(0, new KeyValuePair<YamlElement, YamlElement>(new YamlValue("a mapping 1"), new YamlMapping()));

            seq[0] = new YamlValue("New item 3");

            // Make sure we're still subscribed.
            Assert.AreEqual(3, subscriber.ACalls);
            Assert.AreEqual(2, subscriber.BCalls);

            map["bla"] = new YamlValue("New item 3");

            // Make sure we're still subscribed.
            Assert.AreEqual(3, subscriber.ACalls);
            Assert.AreEqual(3, subscriber.BCalls);
            Assert.AreEqual(1, subscriber.CCalls);
        }


        [Test]
        public void UpdateSubscriberNextChildrenTest2()
        {
            // As the indices of the parents change, the children also need to get re-subscribed.
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var tracker = new YamlNodeTracker();

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream, tracker);
            var rootMapping = (YamlMapping)stream[0].Contents;
            var map = (YamlMapping)rootMapping["a mapping"];
            var seq = (YamlSequence)rootMapping["a sequence"];

            var subscriber = new SubscriberHandler();
            tracker.Subscribe(subscriber, tracker.GetPaths(map["key 1"])[0], "A");
            tracker.Subscribe(subscriber, tracker.GetPaths(seq[0])[0], "B");

            map["key 1"].Tag = "bla";
            seq[0].Tag = "bla";

            Assert.AreEqual(1, subscriber.ACalls);
            Assert.AreEqual(1, subscriber.BCalls);

            rootMapping.Remove(map);

            seq[0].Tag = "bla2";

            // Make sure B is still subscribed and A isn't.
            Assert.AreEqual(1, subscriber.ACalls);
            Assert.AreEqual(2, subscriber.BCalls);

            rootMapping.Insert(0, new KeyValuePair<YamlElement, YamlElement>(new YamlValue("a mapping 1"), new YamlMapping()));

            seq[0].Tag = "bla3";

            Assert.AreEqual(1, subscriber.ACalls);
            Assert.AreEqual(3, subscriber.BCalls);
        }
    }
}
