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

            collectionIndicators[0] = new KeyValuePair<YamlToken.YamlToken, YamlToken.YamlToken>(
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

            var clone = (YamlStream) stream.DeepClone();

            ((YamlMapping) ((YamlMapping) clone[0].Contents)[2].Value)[new YamlValue("key 2")] = new YamlValue("value 3");

            var serialized2 = new StringBuilder();
            stream.WriteTo(new StringWriter(serialized2), true);

            Assert.AreEqual(serialized.ToString(), serialized2.ToString());

            var serialized3 = new StringBuilder();
            clone.WriteTo(new StringWriter(serialized3), true);

            Assert.AreNotEqual(serialized.ToString(), serialized3.ToString());
        }
    }
}
