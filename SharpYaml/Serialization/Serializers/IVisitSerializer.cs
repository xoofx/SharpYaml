using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Serializers
{
    /// <summary>
    /// 
    /// </summary>
    public interface IVisitSerializer
    {
        YamlStyle GetStyle(ref ObjectContext objectContext);

        string ReadMemberName(ref ObjectContext objectContext, string memberName);

        object ReadMemberValue(ref ObjectContext objectContext, IMemberDescriptor member, object memberValue, Type memberType);

        object ReadCollectionItem(ref ObjectContext objectContext, Type itemType);

        KeyValuePair<object, object> ReadDictionaryItem(ref ObjectContext objectContext);

        void WriteMemberName(ref ObjectContext objectContext, IMemberDescriptor member, string memberName);

        void WriteMemberValue(ref ObjectContext objectContext, IMemberDescriptor member, object memberValue, Type memberType);

        void WriteCollectionItem(ref ObjectContext objectContext, object item, Type itemType);

        void WriteDictionaryItem(ref ObjectContext objectContext, KeyValuePair<object, object> keyValue);
    }
}