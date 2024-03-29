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

using System.Collections.Generic;
using System.IO;
using SharpYaml;
using SharpYaml.Events;

namespace SharpYaml.Serialization
{
    /// <summary>
    /// Represents an YAML stream.
    /// </summary>
    public class YamlStream : IEnumerable<YamlDocument>
    {
        /// <summary>
        /// Gets the documents inside the stream.
        /// </summary>
        /// <value>The documents.</value>
        public IList<YamlDocument> Documents { get; } = new List<YamlDocument>();

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlStream"/> class.
        /// </summary>
        public YamlStream()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlStream"/> class.
        /// </summary>
        public YamlStream(params YamlDocument[] documents)
            : this((IEnumerable<YamlDocument>)documents)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlStream"/> class.
        /// </summary>
        public YamlStream(IEnumerable<YamlDocument> documents)
        {
            foreach (var document in documents)
            {
                this.Documents.Add(document);
            }
        }

        /// <summary>
        /// Adds the specified document to the <see cref="Documents"/> collection.
        /// </summary>
        /// <param name="document">The document.</param>
        public void Add(YamlDocument document)
        {
            Documents.Add(document);
        }

        /// <summary>
        /// Loads the stream from the specified input.
        /// </summary>
        /// <param name="input">The input.</param>
        public void Load(TextReader input)
        {
            Documents.Clear();

            var parser = Parser.CreateParser(input);

            var events = new EventReader(parser);
            events.Expect<StreamStart>();
            while (!events.Accept<StreamEnd>())
            {
                var document = new YamlDocument(events);
                Documents.Add(document);
            }
            events.Expect<StreamEnd>();
        }

        /// <summary>
        /// Saves the stream to the specified output.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="isLastDocumentEndImplicit">If set to <c>true</c>, last <see cref="DocumentEnd"/> will be implicit.</param>
        /// <param name="bestIndent">The desired indent.</param>
        public void Save(TextWriter output, bool isLastDocumentEndImplicit = false, int bestIndent = Emitter.MinBestIndent)
        {
            var emitter = new Emitter(output, bestIndent);

            emitter.Emit(new StreamStart());

            var lastDocument = Documents.Count > 0 ? Documents[Documents.Count - 1] : null;
            foreach (var document in Documents)
            {
                bool isDocumentEndImplicit = isLastDocumentEndImplicit && document == lastDocument;
                document.Save(emitter, isDocumentEndImplicit);
            }

            emitter.Emit(new StreamEnd());
        }

        /// <summary>
        /// Accepts the specified visitor by calling the appropriate Visit method on it.
        /// </summary>
        /// <param name="visitor">
        /// A <see cref="IYamlVisitor"/>.
        /// </param>
        public void Accept(IYamlVisitor visitor)
        {
            visitor.Visit(this);
        }

        #region IEnumerable<YamlDocument> Members

        /// <summary />
        public IEnumerator<YamlDocument> GetEnumerator()
        {
            return Documents.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
