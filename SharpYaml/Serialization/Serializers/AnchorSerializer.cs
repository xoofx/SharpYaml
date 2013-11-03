// Copyright (c) 2013 SharpYaml - Alexandre Mutel
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
using SharpYaml.Events;

namespace SharpYaml.Serialization.Serializers
{
	internal class AnchorSerializer : ChainedSerializer
	{
		private Dictionary<string, object> aliasToObject;
		private Dictionary<object, string> objectToAlias;

		public AnchorSerializer(IYamlSerializable next) : base(next)
		{
		}

		public bool TryGetAliasValue(string alias, out object value)
		{
			return AliasToObject.TryGetValue(alias, out value);
		}

		public override ValueOutput ReadYaml(SerializerContext context, object value, ITypeDescriptor typeDescriptor)
		{
			var reader = context.Reader;

			// Process Anchor alias (*oxxx)
			var alias = reader.Allow<AnchorAlias>();
			if (alias != null)
			{
				// Return an alias or directly the value
				return !AliasToObject.TryGetValue(alias.Value, out value) ? new ValueOutput(alias) : new ValueOutput(value);
			}

			// Test if current node has an anchor &oxxx
			string anchor = null;
			var nodeEvent = reader.Peek<NodeEvent>();
			if (nodeEvent != null && !string.IsNullOrEmpty(nodeEvent.Anchor))
			{
				anchor = nodeEvent.Anchor;
			}

			// Deserialize the current node
			var valueResult = base.ReadYaml(context, value, typeDescriptor);

			// Store Anchor (&oxxx) and override any defined anchor 
			if (anchor != null)
			{
				AliasToObject[anchor] = valueResult.Value;
			}

			return valueResult;
		}

		public override void WriteYaml(SerializerContext context, ValueInput input, ITypeDescriptor typeDescriptor)
		{
			var value = input.Value;

			// Only write anchors for object (and not value types)
			bool isAnchorable = false;
			if (value != null && !value.GetType().IsValueType)
			{
				var typeCode = Type.GetTypeCode(value.GetType());
				switch (typeCode)
				{
					case TypeCode.Object:
					case TypeCode.String:
						isAnchorable = true;
						break;
				}
			}

			if (isAnchorable)
			{
				string alias;
				if (ObjectToString.TryGetValue(value, out alias))
				{
					context.Writer.Emit(new AliasEventInfo(value, value.GetType()) {Alias = alias});
					return;
				}
				else
				{
					alias = string.Format("o{0}", context.AnchorCount);
					ObjectToString.Add(value, alias);

					// Store the alias in the context
					context.Anchors.Push(alias);
					context.AnchorCount++;
				}
			}

			base.WriteYaml(context, input, typeDescriptor);
		}

		private Dictionary<string, object> AliasToObject
		{
			get { return aliasToObject ?? (aliasToObject = new Dictionary<string, object>()); }
		}

		private Dictionary<object, string> ObjectToString
		{
			get { return objectToAlias ?? (objectToAlias = new Dictionary<object, string>(new IdentityEqualityComparer<object>())); }
		}
	}
}