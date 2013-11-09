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
using SharpYaml.Events;
using SharpYaml.Serialization.Descriptors;

namespace SharpYaml.Serialization.Serializers
{
    /// <summary>
	/// Base class for serializing an object that can be a Yaml !!map or !!seq.
	/// </summary>
	public class ObjectSerializer : IYamlSerializable, IYamlSerializableFactory
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ObjectSerializer"/> class.
		/// </summary>
		public ObjectSerializer()
		{
		}

		public virtual IYamlSerializable TryCreate(SerializerContext context, ITypeDescriptor typeDescriptor)
		{
			// always accept
			return this;
		}
		
		/// <summary>
		/// Checks if a type is a sequence.
		/// </summary>
		/// <param name="typeDescriptor">The type descriptor.</param>
		/// <returns><c>true</c> if a type is a sequence, <c>false</c> otherwise.</returns>
		protected virtual bool CheckIsSequence(ITypeDescriptor typeDescriptor)
		{
			// By default an object serializer is a mapping
			return false;
		}

		protected virtual YamlStyle GetStyle(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			return typeDescriptor.Style;
		}

		public virtual ValueOutput ReadYaml(SerializerContext context, object value, ITypeDescriptor typeDescriptor)
		{
            // Create or transform the value to deserialize
            // If the new value to serialize is not the same as the one we were expecting to serialize
            CreateOrTransformObjectInternal(context, ref value, ref typeDescriptor);

			// Get the object accessor for the corresponding class
			var isSequence = CheckIsSequence(typeDescriptor);

			// Process members
			return new ValueOutput(isSequence
						? ReadItems<SequenceStart, SequenceEnd>(context, value, typeDescriptor)
						: ReadItems<MappingStart, MappingEnd>(context, value, typeDescriptor));
		}


        private void CreateOrTransformObjectInternal(SerializerContext context, ref object value,
            ref ITypeDescriptor typeDescriptor)
        {
            var newValue = CreateOrTransformObject(context, value, typeDescriptor);
            if (!ReferenceEquals(newValue, value) && newValue != null && newValue.GetType() != typeDescriptor.Type)
            {
                typeDescriptor = context.FindTypeDescriptor(newValue.GetType());
            }
            value = newValue;
        }

        /// <summary>
        /// Overrides this method when deserializing/serializing an object that needs a special instantiation or transformation. By default, this is calling
        /// <see cref="IObjectFactory.Create" /> if the <see cref="currentObject"/> is null or returning <see cref="currentObject"/>.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="currentObject">The current object, may be null. (in case the object is an instance in a member that is not settable).</param>
        /// <param name="typeDescriptor">The type descriptor of the object to create.</param>
        /// <returns>A new instance of the object or <see cref="currentObject"/> if not null</returns>
	    protected virtual object CreateOrTransformObject(SerializerContext context, object currentObject, ITypeDescriptor typeDescriptor)
	    {
            return currentObject ?? context.ObjectFactory.Create(typeDescriptor.Type);
	    }

        /// <summary>
        /// Transforms the object after it has been read. This method is called after an object has been read and before returning the object to
        /// the deserialization process. See remarks for usage.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="currentObject">The current object that has been deserialized..</param>
        /// <param name="typeDescriptor">The type descriptor.</param>
        /// <returns>The actual object deserialized. By default same as <see cref="currentObject"/>.</returns>
        /// <remarks>
        /// This method is usefull in conjunction with <see cref="CreateOrTransformObject"/>.
        /// For example, in the case of deserializing to an immutable member, where we need to call the constructor of a type instead of setting each of 
        /// its member, we can instantiate a mutable object in <see cref="CreateOrTransformObject"/>, receive the mutable object filled in 
        /// <see cref="TransformObjectAfterRead"/> and transform it back to an immutable object.
        /// </remarks>
        protected virtual object TransformObjectAfterRead(SerializerContext context, object currentObject, ITypeDescriptor typeDescriptor)
        {
            return currentObject;
        }

        /// <summary>
        /// Reads the members from the current stream.
        /// </summary>
        /// <typeparam name="TStart">The type of the t start.</typeparam>
        /// <typeparam name="TEnd">The type of the t end.</typeparam>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="typeDescriptor">The type descriptor.</param>
        /// <returns>Return the object being read, by default thisObject passed by argument.</returns>
		protected virtual object ReadItems<TStart, TEnd>(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor) 
			where TStart : NodeEvent
			where TEnd : ParsingEvent
		{
			var reader = context.Reader;
			var start = reader.Expect<TStart>();

            // throws an exception while deserializing
            if (thisObject == null)
            {
                throw new YamlException(start.Start, start.End, "Cannot instantiate an object for type [{0}]".DoFormat(typeDescriptor));
            }

			while (!reader.Accept<TEnd>())
			{
				ReadItem(context, thisObject, typeDescriptor);
			}
			reader.Expect<TEnd>();

            return TransformObjectAfterRead(context, thisObject, typeDescriptor);
		}

        /// <summary>
        /// Reads an item of the object from the YAML flow (either a sequence item or mapping key/value item).
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object.</param>
        /// <param name="typeDescriptor">The type descriptor.</param>
        /// <exception cref="YamlException">Unable to deserialize property [{0}] not found in type [{1}].DoFormat(propertyName, typeDescriptor)</exception>
		protected virtual void ReadItem(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor)
		{
			var reader = context.Reader;

			// For a regular object, the key is expected to be a simple scalar
		    string propertyName;
		    var propertyNode = reader.Expect<Scalar>();
		    var keyName = propertyNode.Value;
            var isKeyDecoded = context.DecodeKeyPre(thisObject, typeDescriptor, keyName, out propertyName);

            var memberAccessor = typeDescriptor[propertyName];

		    if (isKeyDecoded)
		    {
                context.DecodeKeyPost(thisObject, typeDescriptor, memberAccessor, keyName);
            }

		    if (!ReadMemberByName(context, thisObject, propertyName, memberAccessor, typeDescriptor))
                throw new YamlException(propertyNode.Start, propertyNode.End, "Unable to deserialize property [{0}] not found in type [{1}]".DoFormat(propertyName, typeDescriptor));
		}

        /// <summary>
        /// Reads a member by its name.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="thisObject">The this object where the member to read applies.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="memberAccessor">The member accessor. May be null if member acecssor was not found</param>
        /// <param name="typeDescriptor">The type descriptor of the this object.</param>
        /// <returns><c>true</c> if reading the member was successfull, <c>false</c> otherwise.</returns>
        protected virtual bool ReadMemberByName(SerializerContext context, object thisObject, string memberName, IMemberDescriptor memberAccessor, ITypeDescriptor typeDescriptor)
        {
            // Check that property exist before trying to access the descriptor
            if (memberAccessor == null)
            {
                return false;
            }

            // Read the value according to the type
            var propertyType = memberAccessor.Type;

            object value = null;
            if (memberAccessor.SerializeMemberMode == SerializeMemberMode.Content)
            {
                value = memberAccessor.Get(thisObject);
            }

            var valueResult = context.ReadYaml(value, propertyType);

            // Handle late binding
            if (memberAccessor.HasSet && memberAccessor.SerializeMemberMode != SerializeMemberMode.Content)
            {
                // If result value is a late binding, register it.
                if (valueResult.IsAlias)
                {
                    context.AddAliasBinding(valueResult.Alias, lateValue => memberAccessor.Set(thisObject, lateValue));
                }
                else
                {
                    memberAccessor.Set(thisObject, valueResult.Value);
                }
            }

            return true;
        }

		public virtual void WriteYaml(SerializerContext context, ValueInput input, ITypeDescriptor typeDescriptor)
		{
			var value = input.Value;
			var typeOfValue = value.GetType();

			var isSequence = CheckIsSequence(typeDescriptor);

			// Resolve the style, use default style if not defined.
			var style = ResolveStyle(context, value, typeDescriptor);

            // Allow to create on the fly an object that will be used to serialize an object
		    CreateOrTransformObjectInternal(context, ref value, ref typeDescriptor);

			if (isSequence)
			{
				context.Writer.Emit(new SequenceStartEventInfo(value, typeOfValue) { Tag = input.Tag, Anchor = context.GetAnchor(), Style = style});
				WriteItems(context, value, typeDescriptor, style);
				context.Writer.Emit(new SequenceEndEventInfo(value, typeOfValue));
			}
			else
			{
				context.Writer.Emit(new MappingStartEventInfo(value, typeOfValue) { Tag = input.Tag, Anchor = context.GetAnchor(), Style = style });
				WriteItems(context, value, typeDescriptor, style);
				context.Writer.Emit(new MappingEndEventInfo(value, typeOfValue));
			}
		}

		protected virtual void WriteItems(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor, YamlStyle style)
		{
			foreach (var member in typeDescriptor.Members)
			{
			    WriteMember(context, thisObject, typeDescriptor, style, member);
			}
		}

        protected virtual void WriteMember(SerializerContext context, object thisObject, ITypeDescriptor typeDescriptor,
            YamlStyle style, IMemberDescriptor member)
        {
            // Skip any member that we won't serialize
            if (!member.ShouldSerialize(thisObject)) return;

            // Emit the key name
            WriteMemberName(context, context.EncodeKey(thisObject, typeDescriptor, member, member.Name));

            var memberValue = member.Get(thisObject);
            var memberType = member.Type;

            // In case of serializing a property/field which is not writeable
            // we need to change the expected type to the actual type of the 
            // content value
            if (member.SerializeMemberMode == SerializeMemberMode.Content)
            {
                if (memberValue != null)
                {
                    memberType = memberValue.GetType();
                }
            }

            // Push the style of the current member
            context.PushStyle(member.Style);
            context.WriteYaml(memberValue, memberType);
        }

		protected void WriteMemberName(SerializerContext context, string name)
		{
			// Emit the key name
			context.Writer.Emit(new ScalarEventInfo(name, typeof(string))
			{
				RenderedValue = name,
				IsPlainImplicit = true,
				Style = ScalarStyle.Plain
			});
		}

		private YamlStyle ResolveStyle(SerializerContext context, object value, ITypeDescriptor typeDescriptor)
		{
			// Resolve the style, use default style if not defined.
			// First pop style of current member being serialized.
			var style = context.PopStyle();

			// If a dynamic style format is found, try to resolve through it
			if (context.Settings.DynamicStyleFormat != null)
			{
				var dynamicStyle = context.Settings.DynamicStyleFormat.GetStyle(context, value, typeDescriptor);
				if (dynamicStyle != YamlStyle.Any)
				{
					style = dynamicStyle;
				}
			}

			// If no style yet defined
			if (style == YamlStyle.Any)
			{
				// Try to get the style from this serializer
				style = GetStyle(context, value, typeDescriptor);

				// If not defined, get the default style
				if (style == YamlStyle.Any)
				{
					style = context.Settings.DefaultStyle;

					// If default style is set to Any, set it to Block by default.
					if (style == YamlStyle.Any)
					{
						style = YamlStyle.Block;
					}
				}
			}

			return style;
		}
	}
}