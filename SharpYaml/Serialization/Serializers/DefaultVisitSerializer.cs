using System;
using System.Collections.Generic;
using SharpYaml.Serialization.Descriptors;

namespace SharpYaml.Serialization.Serializers
{
    /// <summary>
    /// 
    /// </summary>
    public class DefaultVisitSerializer : IVisitSerializer
    {
        public virtual YamlStyle GetStyle(ref ObjectContext objectContext)
        {
            var context = objectContext.Context;

            // Resolve the style, use default style if not defined.
            // First pop style of current member being serialized.
            var style = objectContext.Style;

            // If no style yet defined
            if (style != YamlStyle.Any)
            {
                return style;
            }

            // Try to get the style from this serializer
            style = objectContext.Descriptor.Style;

            // In case of any style, allow to emit a flow sequence depending on Settings LimitPrimitiveFlowSequence.
            // Apply this only for primitives
            if (style == YamlStyle.Any)
            {
                bool isPrimitiveElementType = false;
                var collectionDescriptor = objectContext.Descriptor as CollectionDescriptor;
                int count = 0;
                if (collectionDescriptor != null)
                {
                    isPrimitiveElementType = PrimitiveDescriptor.IsPrimitive(collectionDescriptor.ElementType);
                    count = collectionDescriptor.GetCollectionCount(objectContext.Instance);
                }
                else
                {
                    var arrayDescriptor = objectContext.Descriptor as ArrayDescriptor;
                    if (arrayDescriptor != null)
                    {
                        isPrimitiveElementType = PrimitiveDescriptor.IsPrimitive(arrayDescriptor.ElementType);
                        count = objectContext.Instance != null ? ((Array)objectContext.Instance).Length : -1;
                    }
                }

                style = objectContext.Instance == null || count >= objectContext.Context.Settings.LimitPrimitiveFlowSequence || !isPrimitiveElementType
                    ? YamlStyle.Block
                    : YamlStyle.Flow;
            }

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

            return style;
        }

        public virtual string ReadMemberName(ref ObjectContext objectContext, string memberName)
        {
            return memberName;
        }

        public virtual object ReadMemberValue(ref ObjectContext objectContext, IMemberDescriptor memberDescriptor, object memberValue,
            Type memberType)
        {
            return objectContext.Context.ReadYaml(memberValue, memberType);
        }

        public virtual object ReadCollectionItem(ref ObjectContext objectContext, Type itemType)
        {
            return objectContext.Context.ReadYaml(null, itemType);
        }

        public virtual KeyValuePair<object, object> ReadDictionaryItem(ref ObjectContext objectContext, KeyValuePair<Type, Type> keyValueType)
        {
            var keyResult = objectContext.Context.ReadYaml(null, keyValueType.Key);
            var valueResult = objectContext.Context.ReadYaml(null, keyValueType.Value);

            return new KeyValuePair<object, object>(keyResult, valueResult);
        }

        public virtual void WriteMemberName(ref ObjectContext objectContext, IMemberDescriptor member, string name)
        {
            // Emit the key name
            objectContext.Writer.Emit(new ScalarEventInfo(name, typeof(string))
            {
                RenderedValue = name,
                IsPlainImplicit = true,
                Style = ScalarStyle.Plain
            });
        }

        public virtual void WriteMemberValue(ref ObjectContext objectContext, IMemberDescriptor member, object memberValue,
            Type memberType)
        {
            // Push the style of the current member
            objectContext.Context.WriteYaml(memberValue, memberType, member.Style);
        }

        public virtual void WriteCollectionItem(ref ObjectContext objectContext, object item, Type itemType)
        {
            objectContext.Context.WriteYaml(item, itemType);
        }

        public virtual void WriteDictionaryItem(ref ObjectContext objectContext, KeyValuePair<object, object> keyValue, KeyValuePair<Type, Type> types)
        {
            objectContext.Context.WriteYaml(keyValue.Key, types.Key);
            objectContext.Context.WriteYaml(keyValue.Value, types.Value);
        }
    }
}