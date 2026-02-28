using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Events;

namespace SharpYaml.Tests;

[TestClass]
public sealed class EventReaderTests
{
    [TestMethod]
    public void PeekAllowExpectAndSkip_WorkAsExpected()
    {
        const string yaml = "root:\n  list:\n    - a\n    - b\n  value: 3\n";
        var parser = Parser.CreateParser(new StringReader(yaml));
        var reader = new EventReader(parser);

        Assert.IsNotNull(reader.Peek<StreamStart>());
        reader.Expect<StreamStart>();

        Assert.IsNotNull(reader.Peek<DocumentStart>());
        reader.Expect<DocumentStart>();

        reader.Expect<MappingStart>();
        var key = reader.Expect<Scalar>();
        Assert.AreEqual("root", key.Value);

        // Skip the "root" mapping value entirely (it contains nested items).
        reader.Skip();

        reader.Expect<MappingEnd>();
        reader.Expect<DocumentEnd>();
        reader.Expect<StreamEnd>();
    }

    [TestMethod]
    public void Skip_UntilDepth_StopsAtRequestedDepth()
    {
        const string yaml = "a:\n  b:\n    c: 1\n";
        var parser = Parser.CreateParser(new StringReader(yaml));
        var reader = new EventReader(parser);

        reader.Expect<StreamStart>();
        reader.Expect<DocumentStart>();
        reader.Expect<MappingStart>();

        var depthAtRootMapping = reader.CurrentDepth;
        reader.Expect<Scalar>(); // 'a'
        reader.Expect<MappingStart>();
        reader.Expect<Scalar>(); // 'b'
        reader.Expect<MappingStart>();
        Assert.IsTrue(reader.CurrentDepth > depthAtRootMapping);

        reader.Skip(depthAtRootMapping);
        Assert.AreEqual(depthAtRootMapping, reader.CurrentDepth);

        // We should now be positioned to read the end of the root mapping/doc/stream.
        reader.Expect<MappingEnd>();
        reader.Expect<DocumentEnd>();
        reader.Expect<StreamEnd>();
    }

    [TestMethod]
    public void AcceptAtEndOfStream_ThrowsEndOfStreamException()
    {
        var parser = Parser.CreateParser(new StringReader(string.Empty));
        var reader = new EventReader(parser);

        reader.Expect<StreamStart>();
        reader.Expect<StreamEnd>();

        Assert.Throws<EndOfStreamException>(() => reader.Accept<StreamStart>());
    }
}
