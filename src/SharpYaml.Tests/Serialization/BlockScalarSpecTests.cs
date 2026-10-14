// Fixtures adapted from yaml/yaml-test-suite, revision da267a5c4782e7361e82889e76c0dc7df0e1e870.
// Source for each ID: https://github.com/yaml/yaml-test-suite/blob/da267a5c4782e7361e82889e76c0dc7df0e1e870/src/<ID>.yaml
// Display markers have been decoded to literal whitespace; expected JSON is from upstream.
//
// MIT License
// Copyright (c) 2016-2020 Ingy döt Net
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class BlockScalarSpecTests
{
    [TestMethod]
    [DataRow("TS54", ">\n ab\n cd\n \n ef\n\n\n gh\n", "\"ab cd\\nef\\n\\ngh\\n\"")]
    [DataRow("K527", ">-\n  trimmed\n  \n \n\n  as\n  space\n", "\"trimmed\\n\\n\\nas space\"")]
    [DataRow("MJS9", ">\n  foo \n \n  \t bar\n\n  baz\n", "\"foo \\n\\n\\t bar\\n\\nbaz\\n\"")]
    [DataRow("7T8X", ">\n\n folded\n line\n\n next\n line\n   * bullet\n\n   * list\n   * lines\n\n last\n line\n\n# Comment\n",
        "\"\\nfolded line\\nnext line\\n  * bullet\\n\\n  * list\\n  * lines\\n\\nlast line\\n\"")]
    [DataRow("F6MC", "---\na: >2\n   more indented\n  regular\nb: >2\n\n\n   more indented\n  regular\n",
        "{\"a\":\" more indented\\nregular\\n\",\"b\":\"\\n\\n more indented\\nregular\\n\"}")]
    [DataRow("D83L", "- |2-\n  explicit indent and chomp\n- |-2\n  chomp and explicit indent\n",
        "[\"explicit indent and chomp\",\"chomp and explicit indent\"]")]
    [DataRow("K858", "strip: >-\n\nclip: >\n\nkeep: |+\n\n", "{\"strip\":\"\",\"clip\":\"\",\"keep\":\"\\n\"}")]
    [DataRow("A6F9", "strip: |-\n  text\nclip: |\n  text\nkeep: |+\n  text\n", "{\"strip\":\"text\",\"clip\":\"text\\n\",\"keep\":\"text\\n\"}")]
    [DataRow("F8F9", " # Strip\n  # Comments:\nstrip: |-\n  # text\n  \n # Clip\n  # comments:\n\nclip: |\n  # text\n \n # Keep\n  # comments:\n\nkeep: |+\n  # text\n\n # Trail\n  # comments.\n",
        "{\"strip\":\"# text\",\"clip\":\"# text\\n\",\"keep\":\"# text\\n\\n\"}")]
    public void Parse_OfficialYamlTestSuiteFixture(string id, string yaml, string expectedJson)
    {
        var actual = JsonSerializer.SerializeToNode(YamlSerializer.Deserialize<object>(yaml));
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expectedJson), actual), $"Fixture {id}: {actual}");
    }
}
