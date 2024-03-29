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

using System;
using System.Collections.Generic;
using System.Text;
using SharpYaml;
using SharpYaml.Events;

namespace SharpYaml.Serialization
{
    /// <summary>
    /// Represents a mapping node in the YAML document.
    /// </summary>
    public class YamlMappingNode : YamlNode, IEnumerable<KeyValuePair<YamlNode, YamlNode>>
    {
        /// <summary>
        /// Gets the children of the current node.
        /// </summary>
        /// <value>The children.</value>
        public IOrderedDictionary<YamlNode, YamlNode> Children { get; } = new OrderedDictionary<YamlNode, YamlNode>();

        /// <summary>
        /// Gets or sets the style of the node.
        /// </summary>
        /// <value>The style.</value>
        public YamlStyle Style { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlMappingNode"/> class.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <param name="state">The state.</param>
        internal YamlMappingNode(EventReader events, DocumentLoadingState state)
        {
            var mapping = events.Expect<MappingStart>();
            Load(mapping, state);
            Style = mapping.Style;

            bool hasUnresolvedAliases = false;
            while (!events.Accept<MappingEnd>())
            {
                var key = ParseNode(events, state);
                var value = ParseNode(events, state);

                try
                {
                    Children.Add(key, value);
                }
                catch (ArgumentException err)
                {
                    throw new YamlException(key.Start, key.End, "Duplicate key", err);
                }

                hasUnresolvedAliases |= key is YamlAliasNode || value is YamlAliasNode;
            }

            if (hasUnresolvedAliases)
            {
                state.AddNodeWithUnresolvedAliases(this);
            }
#if DEBUG
            else
            {
                foreach (var child in Children)
                {
                    if (child.Key is YamlAliasNode)
                    {
                        throw new InvalidOperationException("Error in alias resolution.");
                    }
                    if (child.Value is YamlAliasNode)
                    {
                        throw new InvalidOperationException("Error in alias resolution.");
                    }
                }
            }
#endif

            events.Expect<MappingEnd>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlMappingNode"/> class.
        /// </summary>
        public YamlMappingNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlMappingNode"/> class.
        /// </summary>
        public YamlMappingNode(params KeyValuePair<YamlNode, YamlNode>[] children)
            : this((IEnumerable<KeyValuePair<YamlNode, YamlNode>>)children)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlMappingNode"/> class.
        /// </summary>
        public YamlMappingNode(IEnumerable<KeyValuePair<YamlNode, YamlNode>> children)
        {
            foreach (var child in children)
            {
                this.Children.Add(child);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlMappingNode"/> class.
        /// </summary>
        /// <param name="children">A sequence of <see cref="YamlNode"/> where even elements are keys and odd elements are values.</param>
        public YamlMappingNode(params YamlNode[] children)
            : this((IEnumerable<YamlNode>)children)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlMappingNode"/> class.
        /// </summary>
        /// <param name="children">A sequence of <see cref="YamlNode"/> where even elements are keys and odd elements are values.</param>
        public YamlMappingNode(IEnumerable<YamlNode> children)
        {
            using (var enumerator = children.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var key = enumerator.Current;
                    if (!enumerator.MoveNext())
                    {
                        throw new ArgumentException("When constructing a mapping node with a sequence, the number of elements of the sequence must be even.");
                    }

                    Add(key, enumerator.Current);
                }
            }
        }

        /// <summary>
        /// Adds the specified mapping to the <see cref="Children"/> collection.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        public void Add(YamlNode key, YamlNode value)
        {
            Children.Add(key, value);
        }

        /// <summary>
        /// Adds the specified mapping to the <see cref="Children"/> collection.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        public void Add(string key, YamlNode value)
        {
            Children.Add(new YamlScalarNode(key), value);
        }

        /// <summary>
        /// Adds the specified mapping to the <see cref="Children"/> collection.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        public void Add(YamlNode key, string value)
        {
            Children.Add(key, new YamlScalarNode(value));
        }

        /// <summary>
        /// Adds the specified mapping to the <see cref="Children"/> collection.
        /// </summary>
        /// <param name="key">The key node.</param>
        /// <param name="value">The value node.</param>
        public void Add(string key, string value)
        {
            Children.Add(new YamlScalarNode(key), new YamlScalarNode(value));
        }

        /// <summary>
        /// Resolves the aliases that could not be resolved when the node was created.
        /// </summary>
        /// <param name="state">The state of the document.</param>
        internal override void ResolveAliases(DocumentLoadingState state)
        {
            Dictionary<YamlNode, YamlNode>? keysToUpdate = null;
            Dictionary<YamlNode, YamlNode>? valuesToUpdate = null;
            foreach (var entry in Children)
            {
                if (entry.Key is YamlAliasNode)
                {
                    if (keysToUpdate == null)
                    {
                        keysToUpdate = new Dictionary<YamlNode, YamlNode>();
                    }
                    keysToUpdate.Add(entry.Key, state.GetNode(entry.Key.Anchor!, true, entry.Key.Start, entry.Key.End));
                }
                if (entry.Value is YamlAliasNode)
                {
                    if (valuesToUpdate == null)
                    {
                        valuesToUpdate = new Dictionary<YamlNode, YamlNode>();
                    }
                    valuesToUpdate.Add(entry.Key, state.GetNode(entry.Value.Anchor!, true, entry.Value.Start, entry.Value.End));
                }
            }
            if (valuesToUpdate != null)
            {
                foreach (var entry in valuesToUpdate)
                {
                    Children[entry.Key] = entry.Value;
                }
            }
            if (keysToUpdate != null)
            {
                foreach (var entry in keysToUpdate)
                {
                    var value = Children[entry.Key];
                    Children.Remove(entry.Key);
                    Children.Add(entry.Value, value);
                }
            }
        }

        /// <summary>
        /// Saves the current node to the specified emitter.
        /// </summary>
        /// <param name="emitter">The emitter where the node is to be saved.</param>
        /// <param name="state">The state.</param>
        internal override void Emit(IEmitter emitter, EmitterState state)
        {
            emitter.Emit(new MappingStart(Anchor, Tag, string.IsNullOrEmpty(Tag), Style));
            foreach (var entry in Children)
            {
                entry.Key.Save(emitter, state);
                entry.Value.Save(emitter, state);
            }
            emitter.Emit(new MappingEnd());
        }

        /// <summary>
        /// Accepts the specified visitor by calling the appropriate Visit method on it.
        /// </summary>
        /// <param name="visitor">
        /// A <see cref="IYamlVisitor"/>.
        /// </param>
        public override void Accept(IYamlVisitor visitor)
        {
            visitor.Visit(this);
        }

        /// <summary />
        public override bool Equals(object? other)
        {
            if (other is not YamlMappingNode obj || !Equals(obj) || Children.Count != obj.Children.Count)
            {
                return false;
            }

            foreach (var entry in Children)
            {
                if (!obj.Children.TryGetValue(entry.Key, out var otherNode) || !SafeEquals(entry.Value, otherNode))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            var hashCode = base.GetHashCode();

            foreach (var entry in Children)
            {
                hashCode = CombineHashCodes(hashCode, GetHashCode(entry.Key));
                hashCode = CombineHashCodes(hashCode, GetHashCode(entry.Value));
            }
            return hashCode;
        }

        /// <summary>
        /// Gets all nodes from the document, starting on the current node.
        /// </summary>
        public override IEnumerable<YamlNode> AllNodes
        {
            get
            {
                yield return this;
                foreach (var child in Children)
                {
                    foreach (var node in child.Key.AllNodes)
                    {
                        yield return node;
                    }
                    foreach (var node in child.Value.AllNodes)
                    {
                        yield return node;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var text = new StringBuilder("{ ");

            foreach (var child in Children)
            {
                if (text.Length > 2)
                {
                    text.Append(", ");
                }
                text.Append("{ ").Append(child.Key).Append(", ").Append(child.Value).Append(" }");
            }

            text.Append(" }");

            return text.ToString();
        }

        #region IEnumerable<KeyValuePair<YamlNode,YamlNode>> Members

        /// <summary />
        public IEnumerator<KeyValuePair<YamlNode, YamlNode>> GetEnumerator()
        {
            return Children.GetEnumerator();
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
