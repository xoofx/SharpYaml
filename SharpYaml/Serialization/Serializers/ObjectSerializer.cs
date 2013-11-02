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
			// When the node is not scalar, we need to instantiate the type directly
			if (value == null && !(typeDescriptor is PrimitiveDescriptor))
			{
			    value = CreateObject(context, typeDescriptor);
			}

			// Get the object accessor for the corresponding class
			var isSequence = CheckIsSequence(typeDescriptor);

			// Process members
			return new ValueOutput(isSequence
						? ReadItems<SequenceStart, SequenceEnd>(context, value, typeDescriptor)
						: ReadItems<MappingStart, MappingEnd>(context, value, typeDescriptor));
		}

        /// <summary>
        /// Overrides this method when deserializing an object that needs special instantiation. By default, this is calling
        /// <see cref="IObjectFactory.Create"/>.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="typeDescriptor">The type descriptor of the object to create.</param>
        /// <returns>A new instance of the object</returns>
	    protected virtual object CreateObject(SerializerContext context, ITypeDescriptor typeDescriptor)
	    {
            return context.ObjectFactory.Create(typeDescriptor.Type);
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
			reader.Expect<TStart>();
			while (!reader.Accept<TEnd>())
			{
				ReadItem(context, thisObject, typeDescriptor);
			}
			reader.Expect<TEnd>();
			return thisObject;
		}

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
				// Skip any member that we won't serialize
				if (!member.ShouldSerialize(thisObject)) continue;

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