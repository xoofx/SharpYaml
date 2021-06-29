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
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpYaml.Events;
using SharpYaml.Serialization;
using DocumentStart = SharpYaml.Events.DocumentStart;
using Scalar = SharpYaml.Events.Scalar;
using StreamStart = SharpYaml.Events.StreamStart;

namespace SharpYaml.Model {
    public struct YamlNodeEventEnumerator : IEnumerable<ParsingEvent>, IEnumerator<ParsingEvent> {
        private YamlNode root;
        private YamlNode currentNode;
        private int currentIndex;
        private Stack<YamlNode> nodePath;
        private Stack<int> indexPath;

        public YamlNodeEventEnumerator(YamlNode root) : this() {
            this.root = root;
            currentNode = root;
            currentIndex = -1;
        }

        public IEnumerator<ParsingEvent> GetEnumerator() {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Dispose() { }

        public bool MoveNext() {
            if (currentNode == null)
                return false;
            
            if (nodePath == null) {
                nodePath = new Stack<YamlNode>();
                indexPath = new Stack<int>();
            }
            
            if (currentNode is YamlStream stream) {
                if (currentIndex == -1) {
                    Current = stream.StreamStart;
                    currentIndex++;
                    return true;
                } 
                
                if (currentIndex < stream.Count) {
                    nodePath.Push(currentNode);
                    indexPath.Push(currentIndex);
                    currentNode = stream[currentIndex];
                    currentIndex = -1;
                    MoveNext();
                    return true;
                } 
                  
                Current = stream.StreamEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlDocument document) {
                if (currentIndex == -1) {
                    Current = document.DocumentStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < 1) {
                    nodePath.Push(currentNode);
                    indexPath.Push(currentIndex);
                    currentNode = document.Contents;
                    currentIndex = -1;
                    MoveNext();
                    return true;
                }

                Current = document.DocumentEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlMapping mapping) {
                if (currentIndex == -1) {
                    Current = mapping.MappingStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < mapping.Count * 2) {
                    nodePath.Push(currentNode);
                    indexPath.Push(currentIndex);
                    if (currentIndex % 2 == 0) {
                        currentNode = ((List<YamlElement>) mapping.Keys)[currentIndex / 2];
                        currentIndex = -1;
                    } else {
                        currentNode = mapping[(currentIndex - 1) / 2].Value;
                        currentIndex = -1;
                    }
                    MoveNext();
                    return true;
                }

                Current = mapping.MappingEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlSequence sequence) {
                if (currentIndex == -1) {
                    Current = sequence.SequenceStart;
                    currentIndex++;
                    return true;
                }

                if (currentIndex < sequence.Count) {
                    nodePath.Push(currentNode);
                    indexPath.Push(currentIndex);
                    currentNode = sequence[currentIndex];
                    currentIndex = -1;
                    MoveNext();
                    return true;
                }

                Current = sequence.SequenceEnd;
                Pop();
                return true;
            }

            if (currentNode is YamlValue value) {
                Current = value.Scalar;
                Pop();
                return true;
            }

            return false;
        }

        void Pop() {
            if (currentNode == root) {
                currentNode = null;
                return;
            }
            
            currentNode = nodePath.Pop();
            currentIndex = indexPath.Pop() + 1;
        }
        
        public void Reset() {
            Current = null;
            nodePath.Clear();
            indexPath.Clear();
            currentNode = root;
            currentIndex = -1;
        }

        public ParsingEvent Current { get; private set; }

        object IEnumerator.Current => Current;
    }
}