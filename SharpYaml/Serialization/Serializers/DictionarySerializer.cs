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
using System.Linq;
using SharpYaml.Events;
using SharpYaml.Serialization.Descriptors;

namespace SharpYaml.Serialization.Serializers
{
    /// <summary>
    /// Class for serializing a <see cref="IDictionary{TKey,TValue}"/> or <see cref="System.Collections.IDictionary"/>
    /// </summary>
	public class DictionarySerializer : ObjectSerializer
	{
        /// <summary>
        /// Initializes a new instance of the <see cref="DictionarySerializer"/> class.
        /// </summary>
		public DictionarySerializer()
		{
		}

        /// <inheritdoc/>
		public override IYamlSerializable TryCreate(SerializerContext context, ITypeDescriptor typeDescriptor)
		{
			return typeDescriptor is DictionaryDescriptor ? this : null;
		}

		protected override void ReadItem(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var dictionaryDescriptor = (DictionaryDescriptor) typeDescriptor;

			if (dictionaryDescriptor.IsPureDictionary)
			{
				ReadDictionaryItems(context, thisObject, typeDescriptor);
			}
			else
			{
				var keyEvent = context.Reader.Peek<Scalar>();
			    if (keyEvent != null && keyEvent.Value == context.Settings.SpecialCollectionMember)
			    {
			        var reader = context.Reader;
			        reader.Parser.MoveNext();

			        reader.Expect<MappingStart>();
			        ReadDictionaryItems(context, thisObject, typeDescriptor);
			        reader.Expect<MappingEnd>();
			        return;
			    }

			    base.ReadItem(context, thisObject, typeDescriptor);	
			}
		}

		protected override void WriteMembers(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor, YamlStyle style)
		{
			var dictionaryDescriptor = (DictionaryDescriptor)typeDescriptor;
			if (dictionaryDescriptor.IsPureDictionary)
			{
                WriteDictionaryItems(context, thisObject, dictionaryDescriptor);
			}
			else
			{
				// Serialize Dictionary members
				foreach (var member in typeDescriptor.Members)
				{
                    WriteMember(context, thisObject, typeDescriptor, member);
				}

                WriteMemberName(context, context.Settings.SpecialCollectionMember);

				context.Writer.Emit(new MappingStartEventInfo(thisObject, thisObject.GetType()) { Style = style });
                WriteDictionaryItems(context, thisObject, dictionaryDescriptor);
				context.Writer.Emit(new MappingEndEventInfo(thisObject, thisObject.GetType()));
			}
		}

        /// <summary>
        /// Reads the dictionary items key-values.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="typeDescriptor">The type descriptor.</param>
		protected virtual void ReadDictionaryItems(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var dictionaryDescriptor = (DictionaryDescriptor)typeDescriptor;

			var reader = context.Reader;
			while (!reader.Accept<MappingEnd>())
			{
                // Give a chance to pre-process keys and replace them on the fly
                // Pre-processing is only working on pure scalar keys (string, integers...etc)
			    var keyDecode = context.Reader.Peek<Scalar>();
			    bool isKeyDecoded = false;
			    string preKey = null;
			    if (keyDecode != null)
			    {
			        string newKey;
			        preKey = keyDecode.Value;
                    isKeyDecoded = context.DecodeKeyPre(thisObject, typeDescriptor, keyDecode.Value, out newKey);
			        keyDecode.Value = newKey;
			    }

                // Read key and value
			    ValueOutput keyResult;
			    ValueOutput valueResult;
                ReadDictionaryItem(context, thisObject, dictionaryDescriptor, out keyResult, out valueResult);

                if (isKeyDecoded)
                {
                    context.DecodeKeyPost(thisObject, typeDescriptor, keyResult.Value, preKey);
                }

				// Handle aliasing
				if (keyResult.IsAlias || valueResult.IsAlias)
				{
					if (keyResult.IsAlias)
					{
						if (valueResult.IsAlias)
						{
							context.AddAliasBinding(keyResult.Alias,
							                        deferredKey =>
							                        dictionaryDescriptor.AddToDictionary(thisObject, deferredKey,
							                                                             context.GetAliasValue(valueResult.Alias)));
						}
						else
						{
							context.AddAliasBinding(keyResult.Alias,
							                        deferredKey =>
							                        dictionaryDescriptor.AddToDictionary(thisObject, deferredKey, valueResult.Value));
						}
					}
					else
					{
						context.AddAliasBinding(valueResult.Alias,
						                        deferredAlias =>
						                        dictionaryDescriptor.AddToDictionary(thisObject, keyResult.Value, deferredAlias));
					}
				}
				else
				{
					dictionaryDescriptor.AddToDictionary(thisObject, keyResult.Value, valueResult.Value);
				}
			}
		}

        /// <summary>
        /// Reads a dictionary item key-value.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="dictionaryDescriptor">The dictionary descriptor.</param>
        /// <param name="keyResult">The key result.</param>
        /// <param name="valueResult">The value result.</param>
	    protected virtual void ReadDictionaryItem(SerializerContext context, object thisObject,
            DictionaryDescriptor dictionaryDescriptor, out ValueOutput keyResult, out ValueOutput valueResult)
	    {
            keyResult = context.ReadYaml(null, dictionaryDescriptor.KeyType);
            valueResult = context.ReadYaml(null, dictionaryDescriptor.ValueType);
	    }

        /// <summary>
        /// Writes the dictionary items keys-values.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="dictionaryDescriptor">The dictionary descriptor.</param>
        protected virtual void WriteDictionaryItems(SerializerContext context, object thisObject, DictionaryDescriptor dictionaryDescriptor)
		{
			var keyValues = dictionaryDescriptor.GetEnumerator(thisObject).ToList();

			if (context.Settings.SortKeyForMapping)
			{
				keyValues.Sort(SortDictionaryByKeys);
			}

            // Allow to encode dictionary key before emitting them
            Func<object, string, string> encodeScalarKey = context.KeyTransform != null ? (key, keyText) => context.EncodeKey(thisObject, dictionaryDescriptor, key, keyText) : (Func<object, string, string>)null;

			foreach (var keyValue in keyValues)
			{
			    context.EncodeScalarKey = encodeScalarKey;
                WriteDictionaryItem(context, thisObject, dictionaryDescriptor, keyValue);
			}
		}

        /// <summary>
        /// Writes the dictionary item key-value.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="dictionaryDescriptor">The dictionary descriptor.</param>
        /// <param name="keyValue">The key value.</param>
	    protected virtual void WriteDictionaryItem(SerializerContext context, object thisObject, DictionaryDescriptor dictionaryDescriptor,
	        KeyValuePair<object, object> keyValue)
	    {
            context.WriteYaml(keyValue.Key, dictionaryDescriptor.KeyType);
            context.WriteYaml(keyValue.Value, dictionaryDescriptor.ValueType);
	    }

		private static int SortDictionaryByKeys(KeyValuePair<object, object> left, KeyValuePair<object, object> right)
		{
			if (left.Key is string && right.Key is string)
			{
				return string.CompareOrdinal((string)left.Key, (string)right.Key);
			}

			if (left.Key is IComparable && right.Key is IComparable)
			{
				return ((IComparable)left.Key).CompareTo(right.Key);
			}
			return 0;
		}
	}
}