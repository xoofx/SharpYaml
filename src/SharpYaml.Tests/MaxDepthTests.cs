using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SharpYaml.Model;
using SharpYaml.Serialization;
using ModelYamlNode = SharpYaml.Model.YamlNode;
using ModelYamlStream = SharpYaml.Model.YamlStream;

namespace SharpYaml.Tests
{
    public class MaxDepthTests
    {
        private const int DefaultMaxDepth = 64;

        [Test]
        public void Parser_DefaultMaxDepth_ThrowsForDeeplyNestedFlowSequence()
        {
            var yaml = CreateDeepFlowSequenceYaml(DefaultMaxDepth + 1);
            var parser = Parser.CreateParser(new StringReader(yaml));

            var exception = Assert.Throws<YamlException>(() => Drain(parser));
            StringAssert.Contains("maximum nesting depth", exception.Message);
        }

        [Test]
        public void Parser_CustomMaxDepth_AllowsConfiguredDepth()
        {
            var depth = DefaultMaxDepth + 1;
            var yaml = CreateDeepFlowSequenceYaml(depth);
            var parser = Parser.CreateParser(new StringReader(yaml), depth);

            var events = Drain(parser);

            Assert.IsTrue(events.Count > 0);
        }

        [Test]
        public void Parser_MaxDepth_CannotBeNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Parser.CreateParser(new StringReader("0"), -1));
        }

        [Test]
        public void ModelYamlStream_Load_DefaultMaxDepth_ThrowsForDeeplyNestedFlowMapping()
        {
            var yaml = CreateDeepFlowMappingYaml(DefaultMaxDepth + 1);

            var exception = Assert.Throws<YamlException>(() => ModelYamlStream.Load(new StringReader(yaml)));
            StringAssert.Contains("maximum nesting depth", exception.Message);
        }

        [Test]
        public void Serializer_Deserialize_DefaultMaxDepth_ThrowsForDeeplyNestedFlowSequence()
        {
            var yaml = CreateDeepFlowSequenceYaml(DefaultMaxDepth + 1);
            var serializer = new Serializer();

            var exception = Assert.Throws<YamlException>(() => serializer.Deserialize(yaml));
            StringAssert.Contains("maximum nesting depth", exception.Message);
        }

        [Test]
        public void Serializer_Deserialize_CustomMaxDepth_AllowsDeeperSequence()
        {
            var depth = DefaultMaxDepth + 8;
            var yaml = CreateDeepFlowSequenceYaml(depth);
            var serializer = new Serializer(new SerializerSettings { MaxDepth = depth });

            var result = serializer.Deserialize(yaml);

            Assert.IsNotNull(result);
        }

        [Test]
        public void SerializerSettings_MaxDepth_CannotBeNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SerializerSettings { MaxDepth = -1 });
        }

        [Test]
        public void Serializer_Serialize_DefaultMaxDepth_ThrowsForDeeplyNestedObjectGraph()
        {
            var value = CreateNestedList(DefaultMaxDepth + 1);
            var serializer = new Serializer();

            var exception = Assert.Throws<YamlException>(() => serializer.Serialize(value));
            StringAssert.Contains("maximum nesting depth", exception.Message);
        }

        [Test]
        public void Serializer_Serialize_CustomMaxDepth_AllowsDeeperObjectGraph()
        {
            var depth = DefaultMaxDepth + 8;
            var value = CreateNestedList(depth);
            var serializer = new Serializer(new SerializerSettings { MaxDepth = depth });

            var yaml = serializer.Serialize(value);

            Assert.IsFalse(string.IsNullOrEmpty(yaml));
        }

        [Test]
        public void YamlNode_FromObject_UsesConfiguredMaxDepth()
        {
            var depth = DefaultMaxDepth + 8;
            var value = CreateNestedList(depth);
            var settings = new SerializerSettings { MaxDepth = depth };

            var node = ModelYamlNode.FromObject(value, settings);

            Assert.IsInstanceOf<YamlSequence>(node);
        }

        private static List<Events.ParsingEvent> Drain(IParser parser)
        {
            var events = new List<Events.ParsingEvent>();
            while (parser.MoveNext())
            {
                if (parser.Current != null)
                {
                    events.Add(parser.Current);
                }
            }

            return events;
        }

        private static string CreateDeepFlowSequenceYaml(int depth)
        {
            return new string('[', depth) + "0" + new string(']', depth);
        }

        private static string CreateDeepFlowMappingYaml(int depth)
        {
            var builder = new StringBuilder(depth * 6 + 1);
            for (var i = 0; i < depth; i++)
            {
                builder.Append("{a: ");
            }

            builder.Append('0');

            for (var i = 0; i < depth; i++)
            {
                builder.Append('}');
            }

            return builder.ToString();
        }

        private static object CreateNestedList(int depth)
        {
            object current = 0;
            for (var i = 0; i < depth; i++)
            {
                current = new List<object> { current };
            }

            return current;
        }
    }
}
