// Copyright (c) SharpYaml - Alexandre Mutel
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
using System.Collections;
using System.Collections.Generic;
using SharpYaml.Events;

namespace SharpYaml.Model
{
    /// <summary>Represents the Yaml Node Event Enumerator.</summary>
    public sealed class YamlNodeEventEnumerator : IEnumerable<ParsingEvent>, IEnumerator<ParsingEvent>
    {
        private readonly YamlNode root;
        private YamlNode? currentNode;
        private int currentIndex;
        private Stack<YamlNode>? nodePath;
        private Stack<int>? indexPath;

        /// <summary>Initializes a new instance of this type.</summary>
        public YamlNodeEventEnumerator(YamlNode root)
        {
            this.root = root;
            currentNode = root;
            currentIndex = -1;
        }

        /// <summary>Gets enumerator.</summary>
        public IEnumerator<ParsingEvent> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Releases resources used by the current instance.</summary>
        public void Dispose() { }

        /// <summary>Advances the enumerator to the next element.</summary>
        public bool MoveNext()
        {
            if (currentNode == null)
                return false;

            if (currentNode is YamlStream stream)
            {
                if (currentIndex == -1)
                {
                    Current = stream.StreamStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < stream.Count)
                {
                    Push(stream[currentIndex]);
                    return true;
                }

                Current = stream.StreamEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlDocument document)
            {
                if (currentIndex == -1)
                {
                    Current = document.DocumentStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < 1)
                {
                    Push(document.Contents);
                    return true;
                }

                Current = document.DocumentEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlMapping mapping)
            {
                if (currentIndex == -1)
                {
                    Current = mapping.MappingStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < mapping.Count * 2)
                {
                    if (currentIndex % 2 == 0)
                        Push(((List<YamlElement>)mapping.Keys)[currentIndex / 2]);
                    else
                        Push(mapping[(currentIndex - 1) / 2].Value);
                    return true;
                }

                Current = mapping.MappingEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlSequence sequence)
            {
                if (currentIndex == -1)
                {
                    Current = sequence.SequenceStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < sequence.Count)
                {
                    Push(sequence[currentIndex]);
                    return true;
                }

                Current = sequence.SequenceEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlValue value)
            {
                Current = value.Scalar;
                Pop();
                return true;
            }

            return false;
        }

        void Push(YamlNode nextNode)
        {
            if (nextNode is YamlValue value)
            {
                Current = value.Scalar;
                currentIndex++;
                return;
            }

            if (nodePath == null)
            {
                nodePath = new Stack<YamlNode>();
                indexPath = new Stack<int>();
            }

            nodePath.Push(currentNode);
            indexPath.Push(currentIndex);
            currentNode = nextNode;
            currentIndex = -1;
            MoveNext();
        }

        void Pop()
        {
            if (currentNode == root)
            {
                currentNode = null;
                return;
            }

            currentNode = nodePath.Pop();
            currentIndex = indexPath.Pop() + 1;
        }

        /// <summary>Resets the enumerator to its initial position.</summary>
        public void Reset()
        {
            Current = null!;
            nodePath.Clear();
            indexPath.Clear();
            currentNode = root;
            currentIndex = -1;
        }

        /// <summary>Gets or sets current.</summary>
        public ParsingEvent Current { get; private set; } = null!;

        object IEnumerator.Current => Current;
    }
}
