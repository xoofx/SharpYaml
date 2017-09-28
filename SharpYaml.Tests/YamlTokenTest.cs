using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SharpYaml.Events;
using SharpYaml.Serialization;
using SharpYaml.YamlToken;
using YamlStream = SharpYaml.YamlToken.YamlStream;

namespace SharpYaml.Tests {
    public class YamlTokenTest {
        [Test]
        public void ReadYamlReference() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.YamlReferenceCard.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            var collectionIndicators = ((YamlMapping)((YamlMapping)stream[0].Contents)[new YamlValue("Collection indicators")]);

            var firstCollectionIndicator = collectionIndicators.Keys.First();

            Assert.AreEqual("? ", firstCollectionIndicator.ToObject<string>());

            var firstCollectionIndicatorValue = collectionIndicators[firstCollectionIndicator];

            collectionIndicators[0] = new KeyValuePair<YamlElement, YamlElement>(
                new YamlValue(":-)"),
                firstCollectionIndicatorValue
            );

            var serialized = new StringBuilder();

            stream.WriteTo(new StringWriter(serialized));

            stream = YamlStream.Load(new StringReader(serialized.ToString()));

            collectionIndicators = ((YamlMapping)((YamlMapping)stream[0].Contents)[new YamlValue("Collection indicators")]);

            firstCollectionIndicator = collectionIndicators.Keys.First();

            Assert.AreEqual(":-)", firstCollectionIndicator.ToObject<string>());
        }

        [Test]
        public void YamlValue() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test6.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            var value = ((YamlValue)stream[0].Contents);

            Assert.AreEqual(3.14f, value.ToObject<float>());

            stream[0].Contents = new YamlValue(double.PositiveInfinity);

            var serialized = new StringBuilder();

            stream.WriteTo(new StringWriter(serialized));

            stream = YamlStream.Load(new StringReader(serialized.ToString()));

            value = ((YamlValue)stream[0].Contents);

            Assert.AreEqual(float.PositiveInfinity, value.ToObject<float>());
        }

        [Test]
        public void FromObject() {
            var stream = new YamlStream();
            var document = new YamlToken.YamlDocument();
            stream.Add(document);

            var sequence = (YamlSequence)YamlToken.YamlToken.FromObject(new[] { "item 4", "item 5", "item 6" }, new SerializerSettings { EmitAlias = false }, typeof(string[]));

            sequence.SequenceStart = new SequenceStart(sequence.SequenceStart.Anchor, sequence.SequenceStart.Tag, true, YamlStyle.Flow);

            stream[0].Contents = sequence;

            var serialized = new StringBuilder();

            stream.WriteTo(new StringWriter(serialized), true);

            Assert.AreEqual("[item 4, item 5, item 6]", serialized.ToString().Trim());
        }

        [Test]
        public void DeepClone() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            var serialized = new StringBuilder();
            stream.WriteTo(new StringWriter(serialized), true);

            var clone = (YamlStream)stream.DeepClone();

            ((YamlMapping)((YamlMapping)clone[0].Contents)[2].Value)[new YamlValue("key 2")] = new YamlValue("value 3");

            var serialized2 = new StringBuilder();
            stream.WriteTo(new StringWriter(serialized2), true);

            Assert.AreEqual(serialized.ToString(), serialized2.ToString());

            var serialized3 = new StringBuilder();
            clone.WriteTo(new StringWriter(serialized3), true);

            Assert.AreNotEqual(serialized.ToString(), serialized3.ToString());
        }

        [Test]
        public void MappingStringKey() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            Assert.AreEqual("value 2", ((YamlMapping)((YamlMapping)stream[0].Contents)[2].Value)["key 2"].ToObject<string>());

            ((YamlMapping)((YamlMapping)stream[0].Contents)[2].Value)["key 3"] = new YamlValue("value 3");

            Assert.AreEqual("key 3", ((YamlMapping)((YamlMapping)stream[0].Contents)[2].Value)[2].Key.ToObject<string>());
            Assert.AreEqual("value 3", ((YamlMapping)((YamlMapping)stream[0].Contents)[2].Value)[2].Value.ToObject<string>());
        }


        [Test]
        public void AllowMissingKeyLookup() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test11.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            Assert.IsNull(((YamlMapping)stream[0].Contents)["Bla"]);
        }


        [Test]
        public void ToStringTest() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test8.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            Assert.AreEqual("[item 1, item 2, item 3]", stream.ToString());
            Assert.AreEqual("[item 1, item 2, item 3]", stream[0].Contents.ToString());
            Assert.AreEqual("item 1", ((YamlSequence)stream[0].Contents)[0].ToString());
        }

        [Test]
        public void StyleTest() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.test10.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            var seq = (YamlSequence)(YamlContainer)stream[0].Contents;
            Assert.AreEqual(YamlStyle.Block, seq.Style);
            Assert.AreEqual(YamlStyle.Block, ((YamlContainer)seq[2]).Style);
            Assert.AreEqual(YamlStyle.Block, ((YamlContainer)seq[3]).Style);

            seq.Style = YamlStyle.Flow;
            ((YamlContainer)seq[2]).Style = YamlStyle.Flow;
            ((YamlContainer)seq[3]).Style = YamlStyle.Flow;

            var serialized = new StringBuilder();
            stream.WriteTo(new StringWriter(serialized), true);
            Assert.AreEqual(1, serialized.ToString().Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries).Length);
        }

        [Test]
        public void TagTest() {
            var file = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SharpYaml.Tests.files.dictionaryExplicit.yaml");

            var fileStream = new StreamReader(file);
            var stream = YamlStream.Load(fileStream);

            var dict = stream[0].Contents.ToObject<object>();

            Assert.AreEqual(typeof(Dictionary<string, int>), dict.GetType());
            Assert.AreEqual("!System.Collections.Generic.Dictionary`2[System.String,System.Int32],mscorlib", stream[0].Contents.Tag);

            stream[0].Contents.Tag = "!System.Collections.Generic.Dictionary`2[System.String,System.Double],mscorlib";

            var dict2 = stream[0].Contents.ToObject<object>();

            Assert.AreEqual(typeof(Dictionary<string, double>), dict2.GetType());
        }
    }
}
