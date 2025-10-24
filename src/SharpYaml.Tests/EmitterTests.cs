﻿// Copyright (c) 2015 SharpYaml - Alexandre Mutel
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
// 
// -------------------------------------------------------------------------------
// SharpYaml is a fork of YamlDotNet https://github.com/aaubry/YamlDotNet
// published with the following license:
// -------------------------------------------------------------------------------
// 
// Copyright (c) 2008, 2009, 2010, 2011, 2012 Antoine Aubry
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SharpYaml.Events;
using SharpYaml.Serialization;

namespace SharpYaml.Tests
{
    public class EmitterTests : YamlTest
    {
        public EmitterTests()
        {
#if NETCOREAPP
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        }
        [Test]
        public void EmitExample1()
        {
            ParseAndEmit("test1.yaml");
        }

        [Test]
        public void EmitExample2()
        {
            ParseAndEmit("test2.yaml");
        }

        [Test]
        public void EmitExample3()
        {
            ParseAndEmit("test3.yaml");
        }

        [Test]
        public void EmitExample4()
        {
            ParseAndEmit("test4.yaml");
        }

        [Test]
        public void EmitExample5()
        {
            ParseAndEmit("test5.yaml");
        }

        [Test]
        public void EmitExample6()
        {
            ParseAndEmit("test6.yaml");
        }

        [Test]
        public void EmitExample7()
        {
            ParseAndEmit("test7.yaml");
        }

        [Test]
        public void EmitExample8()
        {
            ParseAndEmit("test8.yaml");
        }

        [Test]
        public void EmitExample9()
        {
            ParseAndEmit("test9.yaml");
        }

        [Test]
        public void EmitExample10()
        {
            ParseAndEmit("test10.yaml");
        }

        [Test]
        public void EmitExample11()
        {
            ParseAndEmit("test11.yaml");
        }

        [Test]
        public void EmitExample12()
        {
            ParseAndEmit("test12.yaml");
        }

        [Test]
        public void EmitExample13()
        {
            ParseAndEmit("test13.yaml");
        }

        [Test]
        public void EmitExample14()
        {
            ParseAndEmit("test14.yaml");
        }

        [Test]
        public void EmitUnicode()
        {
            var encoding = Encoding.GetEncoding(28595); // Cyrillic
            var stream = new MemoryStream();
            var input = "Гранит дзень";
            using (var writer = new StreamWriter(stream, encoding))
            {
                var emitter = new Emitter(writer);
                emitter.Emit(new StreamStart());
                emitter.Emit(new DocumentStart(null, null, true));
                emitter.Emit(new Scalar(input, ScalarStyle.SingleQuoted));
                emitter.Emit(new DocumentEnd(true));
            }
            var result = encoding.GetString(stream.ToArray()).Trim();
            Assert.AreEqual("'" + input + "'", result);
        }

        [Test]
        public void EmitUnicodeEscapes()
        {
            var encoding = new UTF8Encoding(false);
            var stream = new MemoryStream();
            var input = "Test\U00010905Yo♥";
            using (var writer = new StreamWriter(stream, encoding))
            {
                var emitter = new Emitter(writer);
                emitter.Emit(new StreamStart());
                emitter.Emit(new DocumentStart(null, null, true));
                emitter.Emit(new Scalar(input, ScalarStyle.DoubleQuoted));
                emitter.Emit(new DocumentEnd(true));
            }
            var result = encoding.GetString(stream.ToArray()).Trim();
            Assert.AreEqual("\"Test\\xD802\\xDD05Yo♥\"", result);
        }

        private void ParseAndEmit(string name)
        {
            var testText = YamlFile(name).ReadToEnd();

            var output = new StringWriter();
            var parser = Parser.CreateParser(new StringReader(testText));
            IEmitter emitter = new Emitter(output, 2, int.MaxValue, false);
            Dump.WriteLine("= Parse and emit yaml file [" + name + "] =");
            while (parser.MoveNext())
            {
                Dump.WriteLine(parser.Current);
                emitter.Emit(parser.Current);
            }
            Dump.WriteLine();

            Dump.WriteLine("= Original =");
            Dump.WriteLine(testText);
            Dump.WriteLine();

            Dump.WriteLine("= Result =");
            Dump.WriteLine(output);
            Dump.WriteLine();

            // Todo: figure out how (if?) to assert
        }

        private string EmitScalar(Scalar scalar)
        {
            return Emit(
                new SequenceStart(null, null, false, YamlStyle.Block),
                scalar,
                new SequenceEnd()
                );
        }

        private string Emit(params ParsingEvent[] events)
        {
            var buffer = new StringWriter();
            var emitter = new Emitter(buffer);
            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart(null, null, true));

            foreach (var evt in events)
            {
                emitter.Emit(evt);
            }

            emitter.Emit(new DocumentEnd(true));
            emitter.Emit(new StreamEnd());

            return buffer.ToString();
        }

        [Test]
        [TestCase("LF hello\nworld")]
        [TestCase("CRLF hello\r\nworld")]
        public void FoldedStyleDoesNotLooseCharacters(string text)
        {
            var yaml = EmitScalar(new Scalar(null, null, text, ScalarStyle.Folded, true, false));
            Dump.WriteLine(yaml);
            Assert.True(yaml.Contains("world"));
        }

        // We are disabling this and want to keep the \n in the output. It is better to have folded > ? 
        //[Test]
        //public void FoldedStyleIsSelectedWhenNewLinesAreFoundInLiteral()
        //{
        //    var yaml = EmitScalar(new Scalar(null, null, "hello\nworld", ScalarStyle.Any, true, false));
        //    Dump.WriteLine(yaml);
        //    Assert.True(yaml.Contains(">"));
        //}

        [Test]
        public void FoldedStyleDoesNotGenerateExtraLineBreaks()
        {
            var yaml = EmitScalar(new Scalar(null, null, "hello\nworld", ScalarStyle.Folded, true, false));
            Dump.WriteLine(yaml);

            // Todo: Why involve the rep. model when testing the Emitter? Can we match using a regex?
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            var sequence = (YamlSequenceNode)stream.Documents[0].RootNode;
            var scalar = (YamlScalarNode)sequence.Children[0];

            Assert.AreEqual("hello\nworld", scalar.Value);
        }

        [Test]
        public void FoldedStyleDoesNotCollapseLineBreaks()
        {
            var yaml = EmitScalar(new Scalar(null, null, ">+\n", ScalarStyle.Folded, true, false));
            Dump.WriteLine("${0}$", yaml);

            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            var sequence = (YamlSequenceNode)stream.Documents[0].RootNode;
            var scalar = (YamlScalarNode)sequence.Children[0];

            Assert.AreEqual(">+\n", scalar.Value);
        }

        [Test]
        public void FoldedStylePreservesNewLines()
        {
            var input = "id: 0\nPayload:\n  X: 5\n  Y: 6\n";

            var yaml = Emit(
                new MappingStart(),
                new Scalar("Payload"),
                new Scalar(null, null, input, ScalarStyle.Folded, true, false),
                new MappingEnd()
                );
            Dump.WriteLine(yaml);

            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));

            var mapping = (YamlMappingNode)stream.Documents[0].RootNode;
            var value = (YamlScalarNode)mapping.Children.First().Value;

            var output = value.Value;
            Dump.WriteLine(output);
            Assert.AreEqual(input, output);
        }

        [Test]
        public void FoldedScalarWithMultipleWordsPreservesLineBreaks()
        {
            // Test case for the reported issue where "a folded\nscalar" becomes "a folded scalar"
            var input = "a folded\nscalar";
            
            // First, test direct parsing and emission with specific folded style
            var yaml = EmitScalar(new Scalar(null, null, input, ScalarStyle.Folded, true, false));
            Dump.WriteLine("Emitted YAML:");
            Dump.WriteLine(yaml);
            
            // Parse it back and verify the content is preserved
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            var sequence = (YamlSequenceNode)stream.Documents[0].RootNode;
            var scalar = (YamlScalarNode)sequence.Children[0];
            
            Dump.WriteLine($"Original: '{input}'");
            Dump.WriteLine($"Round-trip result: '{scalar.Value}'");
            
            Assert.AreEqual(input, scalar.Value, "Folded scalar should preserve the line break between 'folded' and 'scalar'");
            
            // Also test the round-trip through full parse/emit cycle
            var testYaml = ">-\n  a folded\n  scalar";
            var parser = Parser.CreateParser(new StringReader(testYaml));
            var output = new StringWriter();
            var emitter = new Emitter(output);
            
            while (parser.MoveNext())
            {
                emitter.Emit(parser.Current);
            }
            
            var roundTripYaml = output.ToString().Trim();
            Dump.WriteLine("Round-trip YAML:");
            Dump.WriteLine(roundTripYaml);
            
            // Parse the round-trip result
            var stream2 = new YamlStream();
            stream2.Load(new StringReader(roundTripYaml));
            var scalar2 = (YamlScalarNode)stream2.Documents[0].RootNode;
            
            Assert.AreEqual(input, scalar2.Value, "Round-trip parsing should preserve original content");
        }
    }
}
