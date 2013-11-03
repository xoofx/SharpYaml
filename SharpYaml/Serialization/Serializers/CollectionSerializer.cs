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
using System.Collections;
using SharpYaml.Events;
using SharpYaml.Serialization.Descriptors;

namespace SharpYaml.Serialization.Serializers
{
	internal class CollectionSerializer : ObjectSerializer
	{
		public CollectionSerializer()
		{
		}

		public override IYamlSerializable TryCreate(SerializerContext context, ITypeDescriptor typeDescriptor)
		{
			return typeDescriptor is CollectionDescriptor ? this : null;
		}

		protected override bool CheckIsSequence(ITypeDescriptor typeDescriptor)
		{
			var collectionDescriptor = (CollectionDescriptor)typeDescriptor;

			// If the dictionary is pure, we can directly output a sequence instead of a mapping
			return collectionDescriptor.IsPureCollection || collectionDescriptor.HasOnlyCapacity;
		}

		protected override void ReadItem(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var collectionDescriptor = (CollectionDescriptor)typeDescriptor;

			if (CheckIsSequence(collectionDescriptor))
			{
				ReadPureCollectionItems(context, thisObject, typeDescriptor);
			}
			else
			{
				var keyEvent = context.Reader.Peek<Scalar>();
				if (keyEvent != null)
				{
					if (keyEvent.Value == context.Settings.SpecialCollectionMember)
					{
						var reader = context.Reader;
						reader.Parser.MoveNext();

						// Read inner sequence
						reader.Expect<SequenceStart>();
						ReadPureCollectionItems(context, thisObject, typeDescriptor);
						reader.Expect<SequenceEnd>();
						return;
					}
				}

                base.ReadItem(context, thisObject, typeDescriptor);
			}
		}

		protected override YamlStyle GetStyle(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var style = base.GetStyle(context, thisObject, typeDescriptor);

			// In case of any style, allow to emit a flow sequence depending on Settings LimitPrimitiveFlowSequence.
			// Apply this only for primitives
			if (style == YamlStyle.Any)
			{
				bool isPrimitiveElementType = false;
				var collectionDescriptor = typeDescriptor as CollectionDescriptor;
				int count = 0;
				if (collectionDescriptor != null)
				{
					isPrimitiveElementType = PrimitiveDescriptor.IsPrimitive(collectionDescriptor.ElementType);
					count = collectionDescriptor.GetCollectionCount(thisObject);
				}
				else
				{
					var arrayDescriptor = typeDescriptor as ArrayDescriptor;
					if (arrayDescriptor != null)
					{
						isPrimitiveElementType = PrimitiveDescriptor.IsPrimitive(arrayDescriptor.ElementType);
						count = thisObject != null ? ((Array) thisObject).Length : -1;
					}
				}

				style = thisObject == null || count >= context.Settings.LimitPrimitiveFlowSequence || !isPrimitiveElementType
					       ? YamlStyle.Block
					       : YamlStyle.Flow;
			}

			return style;
		}

		protected override void WriteItems(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor, YamlStyle style)
		{
			var collectionDescriptor = (CollectionDescriptor)typeDescriptor;
			if (CheckIsSequence(collectionDescriptor))
			{
				WritePureCollectionItems(context, thisObject, typeDescriptor);
			}
			else
			{
				// Serialize Dictionary members
				foreach (var member in typeDescriptor.Members)
				{
					if (member.Name == "Capacity" && !context.Settings.EmitCapacityForList)
					{
						continue;
					}

					// Emit the key name
					WriteMemberName(context, member.Name);

					var memberValue = member.Get(thisObject);
					var memberType = member.Type;

					context.PushStyle(member.Style);
					context.WriteYaml(memberValue, memberType);
				}

                WriteMemberName(context, context.Settings.SpecialCollectionMember);

				context.Writer.Emit(new SequenceStartEventInfo(thisObject, thisObject.GetType()) { Style = style });
				WritePureCollectionItems(context, thisObject, typeDescriptor);
				context.Writer.Emit(new SequenceEndEventInfo(thisObject, thisObject.GetType()));
			}
		}

		private void ReadPureCollectionItems(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var collectionDescriptor = (CollectionDescriptor)typeDescriptor;
			if (!collectionDescriptor.HasAdd)
			{
				throw new InvalidOperationException("Cannot deserialize list to type [{0}]. No Add method found".DoFormat(thisObject.GetType()));
			}
			if (collectionDescriptor.IsReadOnly(thisObject))
			{
				throw new InvalidOperationException("Cannot deserialize list to readonly collection type [{0}].".DoFormat(thisObject.GetType()));
			}

			var reader = context.Reader;

			while (!reader.Accept<SequenceEnd>())
			{
				var valueResult = context.ReadYaml(null, collectionDescriptor.ElementType);
	
				// Handle aliasing. TODO: Aliasing doesn't preserve order here. This is not an expected behavior
				if (valueResult.IsAlias)
				{
					context.AddAliasBinding(valueResult.Alias, deferredValue => collectionDescriptor.CollectionAdd(thisObject, deferredValue));
				}
				else
				{
					collectionDescriptor.CollectionAdd(thisObject, valueResult.Value);
				}
			}
		}

		private void WritePureCollectionItems(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var collection = (IEnumerable)thisObject;
			var collectionDescriptor = (CollectionDescriptor)typeDescriptor;

			foreach (var item in collection)
			{
				context.WriteYaml(item, collectionDescriptor.ElementType);
			}
		}
	}
}