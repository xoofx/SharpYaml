using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlObjectConverter<T> : YamlConverter<T?>
{
    private Contract? _contract;

    public override T? Read(YamlReader reader)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return (T)rootAliasValue!;
        }

        if (reader.TokenType == YamlTokenType.Alias)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Aliases are not supported when deserializing into '{typeof(T)}' unless ReferenceHandling is Preserve.");
        }

        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return default;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw YamlThrowHelper.ThrowExpectedMapping(reader);
        }

        Contract contract;
        try
        {
            contract = _contract ??= Contract.Create(typeof(T), reader);
        }
        catch (YamlException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, exception.Message, exception);
        }
        if (contract.Polymorphism is not null)
        {
            return ReadPolymorphic(reader, contract);
        }

        return ReadObjectCore(reader, contract);
    }

    public override void Write(YamlWriter writer, T? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var contract = _contract ??= Contract.Create(typeof(T), writer);

        if (writer.ReferenceWriter is not null && value is not string && !typeof(T).IsValueType)
        {
            if (writer.ReferenceWriter.TryGetAnchor(value, out var existing))
            {
                writer.WriteAlias(existing);
                return;
            }

            var anchor = writer.ReferenceWriter.GetOrAddAnchor(value);
            writer.WriteAnchor(anchor);
        }

        if (value is IYamlOnSerializing onSerializing)
        {
            try
            {
                onSerializing.OnSerializing();
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(Mark.Empty, Mark.Empty, $"An error occurred while invoking '{nameof(IYamlOnSerializing)}.{nameof(IYamlOnSerializing.OnSerializing)}' on '{value.GetType()}'.", exception);
            }
        }

        var runtimeType = value.GetType();
        if (contract.Polymorphism is not null && runtimeType != typeof(T))
        {
            WritePolymorphic(writer, value, runtimeType, contract);
        }
        else
        {
            WriteObjectCore(writer, value, contract);
        }

        if (value is IYamlOnSerialized onSerialized)
        {
            try
            {
                onSerialized.OnSerialized();
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(Mark.Empty, Mark.Empty, $"An error occurred while invoking '{nameof(IYamlOnSerialized)}.{nameof(IYamlOnSerialized.OnSerialized)}' on '{value.GetType()}'.", exception);
            }
        }
    }

    private T? ReadObjectCore(YamlReader reader, Contract contract)
    {
        T instance;
        try
        {
            instance = (T)contract.CreateInstance();
        }
        catch (YamlException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, exception.Message, exception);
        }

        if (reader.ReferenceReader is not null && reader.Anchor is not null)
        {
            reader.ReferenceReader.Register(reader.Anchor, instance!);
        }

        if (instance is IYamlOnDeserializing onDeserializing)
        {
            try
            {
                onDeserializing.OnDeserializing();
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, reader.Start, reader.End, $"An error occurred while invoking '{nameof(IYamlOnDeserializing)}.{nameof(IYamlOnDeserializing.OnDeserializing)}' on '{typeof(T)}'.", exception);
            }
        }

        var options = reader.Options;
        HashSet<Member>? seenMembers = options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins ? null : new HashSet<Member>();
        var mappingStart = reader.Start;
        var requiredSeen = contract.RequiredMembers.Length == 0 ? null : new bool[contract.RequiredMembers.Length];

        reader.Read();
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
            }

            var keyStart = reader.Start;
            var keyEnd = reader.End;
            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            if (!contract.TryGetMember(key, out var member))
            {
                if (contract.ExtensionData is null)
                {
                    reader.Skip();
                    continue;
                }

                try
                {
                    ReadExtensionData(reader, instance!, contract.ExtensionData, key);
                }
                catch (YamlException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
                }
                continue;
            }

            if (requiredSeen is not null && member.RequiredIndex >= 0)
            {
                requiredSeen[member.RequiredIndex] = true;
            }

            var wasSeen = seenMembers is not null && !seenMembers.Add(member);
            if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, $"Duplicate mapping key '{key}'.");
            }

            if (wasSeen && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins)
            {
                reader.Skip();
                continue;
            }

            var converter = member.Converter ??= reader.GetConverter(member.MemberType);
            object? value;
            try
            {
                value = converter.Read(reader, member.MemberType);
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }

            try
            {
                member.SetValue(instance!, value);
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, keyStart, keyEnd, exception.Message, exception);
            }
        }

        if (requiredSeen is not null)
        {
            List<string>? missing = null;
            for (var i = 0; i < requiredSeen.Length; i++)
            {
                if (!requiredSeen[i])
                {
                    missing ??= new List<string>();
                    missing.Add(contract.RequiredMembers[i].Name);
                }
            }

            if (missing is not null)
            {
                throw new YamlException(reader.SourceName, mappingStart, reader.End, $"Missing required mapping key(s) for '{typeof(T)}': {string.Join(", ", missing)}.");
            }
        }

        reader.Read();

        if (instance is IYamlOnDeserialized onDeserialized)
        {
            try
            {
                onDeserialized.OnDeserialized();
            }
            catch (YamlException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new YamlException(reader.SourceName, reader.Start, reader.End, $"An error occurred while invoking '{nameof(IYamlOnDeserialized)}.{nameof(IYamlOnDeserialized.OnDeserialized)}' on '{typeof(T)}'.", exception);
            }
        }

        return instance;
    }

    private void WriteObjectCore(YamlWriter writer, object value, Contract contract)
    {
        writer.WriteStartMapping();

        var options = writer.Options;
        var members = options.MappingOrder == YamlMappingOrderPolicy.Sorted
            ? contract.MembersSorted
            : contract.MembersDeclaration;

        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (!member.CanRead)
            {
                continue;
            }

            var memberValue = member.GetValue(value);
            if (member.ShouldIgnoreOnWrite(memberValue, options))
            {
                continue;
            }

            writer.WritePropertyName(member.Name);
            var converter = member.Converter ??= writer.GetConverter(member.MemberType);
            converter.Write(writer, memberValue);
        }

        WriteExtensionData(writer, value, contract);
        writer.WriteEndMapping();
    }

    private T? ReadPolymorphic(YamlReader reader, Contract contract)
    {
        var polymorphism = contract.Polymorphism!;
        var rootTag = reader.Tag;

        var buffered = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, polymorphism.DiscriminatorPropertyName, out var discriminatorValue);

        Type? targetType = null;
        if (polymorphism.AcceptsPropertyDiscriminator && discriminatorValue is not null)
        {
            if (polymorphism.TryGetDerivedTypeFromDiscriminator(discriminatorValue, out var derived))
            {
                targetType = derived;
            }
            else if (polymorphism.UnknownDerivedTypeHandling == YamlUnknownDerivedTypeHandling.Fail)
            {
                throw YamlThrowHelper.ThrowUnknownTypeDiscriminator(reader, discriminatorValue, typeof(T));
            }
        }

        if (targetType is null && polymorphism.AcceptsTagDiscriminator && rootTag is not null)
        {
            if (polymorphism.TryGetDerivedTypeFromTag(rootTag, out var derivedFromTag))
            {
                targetType = derivedFromTag;
            }
        }

        targetType ??= typeof(T);

        var bufferedReader = reader.CreateReader(buffered);
        if (!bufferedReader.Read())
        {
            return default;
        }

        if (targetType == typeof(T))
        {
            return ReadObjectCore(bufferedReader, contract);
        }

        var converter = bufferedReader.GetConverter(targetType);
        var value = converter.Read(bufferedReader, targetType);
        return (T?)value;
    }

    private void WritePolymorphic(YamlWriter writer, object value, Type runtimeType, Contract contract)
    {
        var polymorphism = contract.Polymorphism!;
        if (!polymorphism.TryGetDerivedTypeInfo(runtimeType, out var derivedInfo))
        {
            throw new NotSupportedException($"Type '{runtimeType}' is not a registered derived type of '{typeof(T)}'.");
        }

        if (polymorphism.EmitsTagDiscriminator && derivedInfo.Tag is not null)
        {
            writer.WriteTag(derivedInfo.Tag);
        }

        writer.WriteStartMapping();

        if (polymorphism.EmitsPropertyDiscriminator)
        {
            writer.WritePropertyName(polymorphism.DiscriminatorPropertyName);
            writer.WriteScalar(derivedInfo.Discriminator);
        }

        var options = writer.Options;
        var derivedContract = Contract.Create(runtimeType, writer);
        var members = options.MappingOrder == YamlMappingOrderPolicy.Sorted
            ? derivedContract.MembersSorted
            : derivedContract.MembersDeclaration;

        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (!member.CanRead)
            {
                continue;
            }

            if (string.Equals(member.Name, polymorphism.DiscriminatorPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            var memberValue = member.GetValue(value);
            if (member.ShouldIgnoreOnWrite(memberValue, options))
            {
                continue;
            }

            writer.WritePropertyName(member.Name);
            var converter = member.Converter ??= writer.GetConverter(member.MemberType);
            converter.Write(writer, memberValue);
        }

        WriteExtensionData(writer, value, derivedContract);
        writer.WriteEndMapping();
    }

    private sealed class Contract
    {
        private readonly Dictionary<string, Member> _membersByName;

        public Contract(
            Func<object> createInstance,
            Member[] membersDeclaration,
            Member[] membersSorted,
            Dictionary<string, Member> membersByName,
            Member[] requiredMembers,
            ExtensionDataInfo? extensionData,
            PolymorphismModel? polymorphism)
        {
            CreateInstance = createInstance;
            MembersDeclaration = membersDeclaration;
            MembersSorted = membersSorted;
            _membersByName = membersByName;
            RequiredMembers = requiredMembers;
            ExtensionData = extensionData;
            Polymorphism = polymorphism;
        }

        public Func<object> CreateInstance { get; }

        public Member[] MembersDeclaration { get; }

        public Member[] MembersSorted { get; }

        public Member[] RequiredMembers { get; }

        public ExtensionDataInfo? ExtensionData { get; }

        public PolymorphismModel? Polymorphism { get; }

        public static Contract Create(Type type, YamlReaderWriterBase readerWriter)
        {
            ArgumentNullException.ThrowIfNull(readerWriter);
            var options = readerWriter.Options;

            object CreateInstance()
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    throw new NotSupportedException($"Type '{type}' cannot be instantiated.");
                }

                object? instance;
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (MissingMethodException exception)
                {
                    throw new NotSupportedException($"Type '{type}' does not have a public parameterless constructor.", exception);
                }
                if (instance is null)
                {
                    throw new NotSupportedException($"Type '{type}' does not have a public parameterless constructor.");
                }

                return instance;
            }

            var members = new List<Member>();
            var requiredMembers = new List<Member>();
            ExtensionDataInfo? extensionData = null;

            const BindingFlags allInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var property in type.GetProperties(allInstance))
            {
                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                if (IsExtensionData(property))
                {
                    if (extensionData is not null)
                    {
                        throw new NotSupportedException($"Type '{type}' defines multiple extension data members.");
                    }

                    if (IsIgnored(property, out _))
                    {
                        throw new NotSupportedException($"Extension data member '{property.Name}' on '{type}' cannot be ignored.");
                    }

                    if (IsRequired(property))
                    {
                        throw new NotSupportedException($"Extension data member '{property.Name}' on '{type}' cannot be required.");
                    }

                    var extensionMember = new Member(property.Name, order: 0, declarationOrder: property.MetadataToken, property.PropertyType, property, ignoreCondition: null, isRequired: false);
                    extensionData = ExtensionDataInfo.Create(type, extensionMember, property.PropertyType);
                    continue;
                }

                var include = property.GetMethod is { IsPublic: true } && property.SetMethod is { IsPublic: true };
                var hasIncludeAttr = property.IsDefined(typeof(YamlIncludeAttribute), inherit: true) ||
                                     property.IsDefined(typeof(JsonIncludeAttribute), inherit: true);
                if (!include && !hasIncludeAttr)
                {
                    continue;
                }

                if (IsIgnored(property, out var ignoreCondition))
                {
                    continue;
                }

                var name = GetMemberName(property, readerWriter);
                var order = GetMemberOrder(property);
                var token = property.MetadataToken;

                var member = new Member(name, order, token, property.PropertyType, property, ignoreCondition, IsRequired(property));
                member.Converter = CreateConverterFromAttribute(property, property.PropertyType, options);
                members.Add(member);
                if (member.IsRequired)
                {
                    requiredMembers.Add(member);
                }
            }

            foreach (var field in type.GetFields(allInstance))
            {
                if (IsExtensionData(field))
                {
                    if (extensionData is not null)
                    {
                        throw new NotSupportedException($"Type '{type}' defines multiple extension data members.");
                    }

                    if (IsIgnored(field, out _))
                    {
                        throw new NotSupportedException($"Extension data member '{field.Name}' on '{type}' cannot be ignored.");
                    }

                    if (IsRequired(field))
                    {
                        throw new NotSupportedException($"Extension data member '{field.Name}' on '{type}' cannot be required.");
                    }

                    var extensionMember = new Member(field.Name, order: 0, declarationOrder: field.MetadataToken, field.FieldType, field, ignoreCondition: null, isRequired: false);
                    extensionData = ExtensionDataInfo.Create(type, extensionMember, field.FieldType);
                    continue;
                }

                var hasIncludeAttr = field.IsDefined(typeof(YamlIncludeAttribute), inherit: true) ||
                                     field.IsDefined(typeof(JsonIncludeAttribute), inherit: true);
                if (!hasIncludeAttr)
                {
                    continue;
                }

                if (IsIgnored(field, out var ignoreCondition))
                {
                    continue;
                }

                var name = GetMemberName(field, readerWriter);
                var order = GetMemberOrder(field);
                var token = field.MetadataToken;

                var member = new Member(name, order, token, field.FieldType, field, ignoreCondition, IsRequired(field));
                member.Converter = CreateConverterFromAttribute(field, field.FieldType, options);
                members.Add(member);
                if (member.IsRequired)
                {
                    requiredMembers.Add(member);
                }
            }

            members.Sort(static (x, y) =>
            {
                var orderCompare = x.Order.CompareTo(y.Order);
                return orderCompare != 0 ? orderCompare : x.DeclarationOrder.CompareTo(y.DeclarationOrder);
            });
            var membersDeclaration = members.ToArray();

            var membersSorted = (Member[])membersDeclaration.Clone();
            Array.Sort(membersSorted, static (x, y) =>
            {
                var orderCompare = x.Order.CompareTo(y.Order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                var nameCompare = string.CompareOrdinal(x.Name, y.Name);
                return nameCompare != 0 ? nameCompare : x.DeclarationOrder.CompareTo(y.DeclarationOrder);
            });

            var map = new Dictionary<string, Member>(readerWriter.PropertyNameComparer);
            for (var i = 0; i < membersDeclaration.Length; i++)
            {
                var member = membersDeclaration[i];
                map[member.Name] = member;
            }

            var polymorphism = PolymorphismModel.TryCreate(type, options);
            for (var i = 0; i < requiredMembers.Count; i++)
            {
                requiredMembers[i].RequiredIndex = i;
            }

            return new Contract(CreateInstance, membersDeclaration, membersSorted, map, requiredMembers.ToArray(), extensionData, polymorphism);
        }

        public bool TryGetMember(string name, out Member member) => _membersByName.TryGetValue(name, out member!);
    }

    private enum ExtensionDataKind
    {
        Dictionary,
        Mapping,
    }

    private sealed class ExtensionDataInfo
    {
        private ExtensionDataInfo(Member member, ExtensionDataKind kind, Type? dictionaryValueType, Func<object> createContainer)
        {
            Member = member;
            Kind = kind;
            DictionaryValueType = dictionaryValueType;
            CreateContainer = createContainer;
        }

        public Member Member { get; }

        public ExtensionDataKind Kind { get; }

        public Type? DictionaryValueType { get; }

        public Func<object> CreateContainer { get; }

        public static ExtensionDataInfo Create(Type declaringType, Member member, Type memberType)
        {
            ArgumentNullException.ThrowIfNull(declaringType);
            ArgumentNullException.ThrowIfNull(member);
            ArgumentNullException.ThrowIfNull(memberType);

            if (typeof(YamlMapping).IsAssignableFrom(memberType))
            {
                object CreateMapping()
                {
                    if (memberType == typeof(YamlMapping))
                    {
                        return new YamlMapping();
                    }

                    if (memberType.IsAbstract || memberType.IsInterface)
                    {
                        throw new NotSupportedException($"Extension data member '{member.Name}' on '{declaringType}' must be a concrete '{typeof(YamlMapping)}' type.");
                    }

                    return Activator.CreateInstance(memberType)
                           ?? throw new NotSupportedException($"Extension data member '{member.Name}' on '{declaringType}' could not be instantiated.");
                }

                return new ExtensionDataInfo(member, ExtensionDataKind.Mapping, dictionaryValueType: null, CreateMapping);
            }

            if (!TryGetExtensionDataDictionaryValueType(memberType, out var valueType))
            {
                throw new NotSupportedException($"Extension data member '{member.Name}' on '{declaringType}' must be a '{typeof(YamlMapping)}' or implement 'IDictionary<string, object>' or 'IDictionary<string, YamlNode>'.");
            }

            Type createType;
            if (valueType == typeof(object))
            {
                createType = typeof(Dictionary<string, object?>);
            }
            else if (typeof(YamlNode).IsAssignableFrom(valueType))
            {
                createType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
            }
            else
            {
                throw new NotSupportedException($"Extension data dictionary member '{member.Name}' on '{declaringType}' must use 'object' or '{typeof(YamlNode)}' values.");
            }

            if (!memberType.IsAssignableFrom(createType))
            {
                throw new NotSupportedException($"Extension data dictionary member '{member.Name}' on '{declaringType}' must be assignable from '{createType}'.");
            }

            object CreateDictionary()
            {
                return Activator.CreateInstance(createType)
                       ?? throw new NotSupportedException($"Extension data member '{member.Name}' on '{declaringType}' could not be instantiated.");
            }

            return new ExtensionDataInfo(member, ExtensionDataKind.Dictionary, valueType, CreateDictionary);
        }

        private static bool TryGetExtensionDataDictionaryValueType(Type type, out Type valueType)
        {
            valueType = null!;

            if (TryGetDictionaryInterface(type, out var dictionaryInterface))
            {
                valueType = dictionaryInterface.GetGenericArguments()[1];
                if (valueType == typeof(object))
                {
                    return true;
                }

                if (typeof(YamlNode).IsAssignableFrom(valueType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetDictionaryInterface(Type type, out Type dictionaryInterface)
        {
            dictionaryInterface = null!;

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                if (definition == typeof(IDictionary<,>) && type.GetGenericArguments()[0] == typeof(string))
                {
                    dictionaryInterface = type;
                    return true;
                }
            }

            var interfaces = type.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                var candidate = interfaces[i];
                if (!candidate.IsGenericType)
                {
                    continue;
                }

                var definition = candidate.GetGenericTypeDefinition();
                if (definition == typeof(IDictionary<,>) && candidate.GetGenericArguments()[0] == typeof(string))
                {
                    dictionaryInterface = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class PolymorphismModel
    {
        private readonly Dictionary<string, Type> _discriminatorToType;
        private readonly Dictionary<string, Type> _tagToType;
        private readonly Dictionary<Type, DerivedTypeInfo> _typeToDerived;

        private PolymorphismModel(
            string discriminatorPropertyName,
            YamlTypeDiscriminatorStyle style,
            YamlUnknownDerivedTypeHandling unknownDerivedTypeHandling,
            Dictionary<string, Type> discriminatorToType,
            Dictionary<string, Type> tagToType,
            Dictionary<Type, DerivedTypeInfo> typeToDerived)
        {
            DiscriminatorPropertyName = discriminatorPropertyName;
            Style = style;
            UnknownDerivedTypeHandling = unknownDerivedTypeHandling;
            _discriminatorToType = discriminatorToType;
            _tagToType = tagToType;
            _typeToDerived = typeToDerived;
        }

        public string DiscriminatorPropertyName { get; }

        public YamlTypeDiscriminatorStyle Style { get; }

        public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; }

        public bool AcceptsPropertyDiscriminator => Style is YamlTypeDiscriminatorStyle.Property or YamlTypeDiscriminatorStyle.Both;

        public bool AcceptsTagDiscriminator => Style is YamlTypeDiscriminatorStyle.Tag or YamlTypeDiscriminatorStyle.Both;

        public bool EmitsPropertyDiscriminator => Style is YamlTypeDiscriminatorStyle.Property or YamlTypeDiscriminatorStyle.Both;

        public bool EmitsTagDiscriminator => Style is YamlTypeDiscriminatorStyle.Tag or YamlTypeDiscriminatorStyle.Both;

        public bool TryGetDerivedTypeFromDiscriminator(string discriminator, out Type derivedType)
            => _discriminatorToType.TryGetValue(discriminator, out derivedType!);

        public bool TryGetDerivedTypeFromTag(string tag, out Type derivedType)
            => _tagToType.TryGetValue(tag, out derivedType!);

        public bool TryGetDerivedTypeInfo(Type derivedType, out DerivedTypeInfo info)
            => _typeToDerived.TryGetValue(derivedType, out info);

        public static PolymorphismModel? TryCreate(Type type, YamlSerializerOptions options)
        {
            var yamlDerived = type.GetCustomAttributes(typeof(YamlDerivedTypeAttribute), inherit: false);
            var jsonDerived = type.GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false);

            if (yamlDerived.Length == 0 && jsonDerived.Length == 0)
            {
                return null;
            }

            var yamlPolymorphic = type.GetCustomAttribute<YamlPolymorphicAttribute>(inherit: false);
            var jsonPolymorphic = type.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false);

            var style = options.PolymorphismOptions.DiscriminatorStyle;
            if (yamlPolymorphic is not null && yamlPolymorphic.DiscriminatorStyle != YamlTypeDiscriminatorStyle.Unspecified)
            {
                style = yamlPolymorphic.DiscriminatorStyle;
            }

            var discriminatorPropertyName = yamlPolymorphic?.TypeDiscriminatorPropertyName;
            if (string.IsNullOrWhiteSpace(discriminatorPropertyName))
            {
                discriminatorPropertyName = jsonPolymorphic?.TypeDiscriminatorPropertyName;
            }

            discriminatorPropertyName ??= options.PolymorphismOptions.TypeDiscriminatorPropertyName;

            var unknownHandling = options.PolymorphismOptions.UnknownDerivedTypeHandling;
            if (jsonPolymorphic is not null)
            {
                unknownHandling = jsonPolymorphic.UnknownDerivedTypeHandling switch
                {
                    JsonUnknownDerivedTypeHandling.FallBackToBaseType => YamlUnknownDerivedTypeHandling.FallBackToBase,
                    _ => YamlUnknownDerivedTypeHandling.Fail,
                };
            }

            var discriminatorToType = new Dictionary<string, Type>(StringComparer.Ordinal);
            var tagToType = new Dictionary<string, Type>(StringComparer.Ordinal);
            var typeToDerived = new Dictionary<Type, DerivedTypeInfo>();

            foreach (YamlDerivedTypeAttribute attribute in yamlDerived)
            {
                if (!type.IsAssignableFrom(attribute.DerivedType))
                {
                    throw new InvalidOperationException($"Derived type '{attribute.DerivedType}' is not assignable to '{type}'.");
                }

                discriminatorToType.Add(attribute.Discriminator, attribute.DerivedType);
                typeToDerived[attribute.DerivedType] = new DerivedTypeInfo(attribute.Discriminator, attribute.Tag);
                if (attribute.Tag is not null)
                {
                    tagToType.Add(attribute.Tag, attribute.DerivedType);
                }
            }

            foreach (JsonDerivedTypeAttribute attribute in jsonDerived)
            {
                if (!type.IsAssignableFrom(attribute.DerivedType))
                {
                    throw new InvalidOperationException($"Derived type '{attribute.DerivedType}' is not assignable to '{type}'.");
                }

                var discriminator = attribute.TypeDiscriminator switch
                {
                    null => throw new InvalidOperationException($"JsonDerivedTypeAttribute for '{attribute.DerivedType}' did not specify a discriminator."),
                    string s => s,
                    _ => Convert.ToString(attribute.TypeDiscriminator, CultureInfo.InvariantCulture) ?? string.Empty,
                };

                discriminatorToType.TryAdd(discriminator, attribute.DerivedType);
                typeToDerived.TryAdd(attribute.DerivedType, new DerivedTypeInfo(discriminator, tag: null));
            }

            return new PolymorphismModel(discriminatorPropertyName, style, unknownHandling, discriminatorToType, tagToType, typeToDerived);
        }

        public readonly struct DerivedTypeInfo
        {
            public DerivedTypeInfo(string discriminator, string? tag)
            {
                Discriminator = discriminator;
                Tag = tag;
            }

            public string Discriminator { get; }

            public string? Tag { get; }
        }
    }

    private sealed class Member
    {
        private readonly PropertyInfo? _property;
        private readonly FieldInfo? _field;

        public Member(string name, int order, int declarationOrder, Type memberType, PropertyInfo property, YamlIgnoreCondition? ignoreCondition, bool isRequired)
        {
            Name = name;
            Order = order;
            DeclarationOrder = declarationOrder;
            MemberType = memberType;
            _property = property;
            _field = null;
            IgnoreCondition = ignoreCondition;
            IsRequired = isRequired;
        }

        public Member(string name, int order, int declarationOrder, Type memberType, FieldInfo field, YamlIgnoreCondition? ignoreCondition, bool isRequired)
        {
            Name = name;
            Order = order;
            DeclarationOrder = declarationOrder;
            MemberType = memberType;
            _property = null;
            _field = field;
            IgnoreCondition = ignoreCondition;
            IsRequired = isRequired;
        }

        public string Name { get; }

        public int Order { get; }

        public int DeclarationOrder { get; }

        public Type MemberType { get; }

        public bool CanRead => _property?.GetMethod is not null || _field is not null;

        public YamlIgnoreCondition? IgnoreCondition { get; }

        public bool IsRequired { get; }

        public int RequiredIndex { get; set; } = -1;

        public YamlConverter? Converter { get; set; }

        public object? GetValue(object instance)
        {
            if (_property is not null)
            {
                return _property.GetValue(instance);
            }

            return _field!.GetValue(instance);
        }

        public void SetValue(object instance, object? value)
        {
            if (_property is not null)
            {
                _property.SetValue(instance, value);
                return;
            }

            _field!.SetValue(instance, value);
        }

        public bool ShouldIgnoreOnWrite(object? value, YamlSerializerOptions options)
        {
            var ignoreCondition = IgnoreCondition ?? options.DefaultIgnoreCondition;

            switch (ignoreCondition)
            {
                case YamlIgnoreCondition.Never:
                    return false;

                case YamlIgnoreCondition.WhenWritingNull:
                    return value is null;

                case YamlIgnoreCondition.WhenWritingDefault:
                    if (value is null)
                    {
                        return true;
                    }

                    if (MemberType.IsValueType)
                    {
                        var defaultValue = Activator.CreateInstance(MemberType);
                        return value.Equals(defaultValue);
                    }

                    return false;

                default:
                    return false;
            }
        }
    }

    private static void ReadExtensionData(YamlReader reader, object instance, ExtensionDataInfo extensionData, string key)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(extensionData);
        ArgumentNullException.ThrowIfNull(key);

        var member = extensionData.Member;
        var container = member.GetValue(instance);
        if (container is null)
        {
            container = extensionData.CreateContainer();
            try
            {
                member.SetValue(instance, container);
            }
            catch (Exception exception)
            {
                throw new NotSupportedException($"Extension data member '{member.Name}' could not be assigned on '{instance.GetType()}'.", exception);
            }
        }

        switch (extensionData.Kind)
        {
            case ExtensionDataKind.Dictionary:
            {
                if (container is not IDictionary dictionary)
                {
                    throw new NotSupportedException($"Extension data member '{member.Name}' on '{instance.GetType()}' must implement '{typeof(IDictionary)}'.");
                }

                var valueType = extensionData.DictionaryValueType ?? typeof(object);
                object? value;
                var converter = reader.GetConverter(valueType);
                value = converter.Read(reader, valueType);

                if (value is not null && valueType != typeof(object) && !valueType.IsInstanceOfType(value))
                {
                    throw new NotSupportedException($"Extension data value '{value.GetType()}' cannot be stored in '{valueType}'.");
                }

                dictionary[key] = value;
                return;
            }

            case ExtensionDataKind.Mapping:
            {
                if (container is not YamlMapping mapping)
                {
                    throw new NotSupportedException($"Extension data member '{member.Name}' on '{instance.GetType()}' must be a '{typeof(YamlMapping)}'.");
                }

                var elementConverter = reader.GetConverter(typeof(YamlElement));
                var element = (YamlElement?)elementConverter.Read(reader, typeof(YamlElement));

                var list = (IList<KeyValuePair<YamlElement, YamlElement?>>)mapping;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Key is YamlValue keyValue && string.Equals(keyValue.Value, key, StringComparison.Ordinal))
                    {
                        list[i] = new KeyValuePair<YamlElement, YamlElement?>(list[i].Key, element);
                        return;
                    }
                }

                mapping.Add(new YamlValue(key), element);
                return;
            }

            default:
                throw new InvalidOperationException($"Unknown extension data kind '{extensionData.Kind}'.");
        }
    }

    private static void WriteExtensionData(YamlWriter writer, object instance, Contract contract)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(contract);

        var extensionData = contract.ExtensionData;
        if (extensionData is null)
        {
            return;
        }

        var member = extensionData.Member;
        var container = member.GetValue(instance);
        if (container is null)
        {
            return;
        }

        switch (extensionData.Kind)
        {
            case ExtensionDataKind.Dictionary:
                WriteExtensionDictionary(writer, container, extensionData.DictionaryValueType ?? typeof(object));
                return;

            case ExtensionDataKind.Mapping:
                if (container is not YamlMapping mapping)
                {
                    throw new YamlException(Mark.Empty, Mark.Empty, $"Extension data member '{member.Name}' on '{instance.GetType()}' must be a '{typeof(YamlMapping)}'.");
                }

                WriteExtensionMapping(writer, mapping);
                return;

            default:
                throw new InvalidOperationException($"Unknown extension data kind '{extensionData.Kind}'.");
        }
    }

    private static void WriteExtensionDictionary(YamlWriter writer, object container, Type valueType)
    {
        if (container is not IDictionary dictionary)
        {
            throw new YamlException(Mark.Empty, Mark.Empty, $"Extension data dictionary must implement '{typeof(IDictionary)}'.");
        }

        if (writer.Options.MappingOrder == YamlMappingOrderPolicy.Sorted)
        {
            var items = new List<KeyValuePair<string, object?>>(dictionary.Count);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key)
                {
                    throw new YamlException(Mark.Empty, Mark.Empty, "Extension data dictionary keys must be strings.");
                }

                items.Add(new KeyValuePair<string, object?>(key, entry.Value));
            }

            items.Sort(static (x, y) => string.CompareOrdinal(x.Key, y.Key));
            for (var i = 0; i < items.Count; i++)
            {
                WriteExtensionEntry(writer, items[i].Key, items[i].Value, valueType);
            }

            return;
        }

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key)
            {
                throw new YamlException(Mark.Empty, Mark.Empty, "Extension data dictionary keys must be strings.");
            }

            WriteExtensionEntry(writer, key, entry.Value, valueType);
        }
    }

    private static void WriteExtensionMapping(YamlWriter writer, YamlMapping mapping)
    {
        var list = (IList<KeyValuePair<YamlElement, YamlElement?>>)mapping;
        if (writer.Options.MappingOrder == YamlMappingOrderPolicy.Sorted)
        {
            var items = new List<KeyValuePair<string, YamlElement?>>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                var pair = list[i];
                if (pair.Key is not YamlValue keyValue)
                {
                    throw new YamlException(Mark.Empty, Mark.Empty, "Only scalar mapping keys are supported for extension data.");
                }

                items.Add(new KeyValuePair<string, YamlElement?>(keyValue.Value, pair.Value));
            }

            items.Sort(static (x, y) => string.CompareOrdinal(x.Key, y.Key));
            for (var i = 0; i < items.Count; i++)
            {
                WriteExtensionEntry(writer, items[i].Key, items[i].Value, typeof(YamlNode));
            }

            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var pair = list[i];
            if (pair.Key is not YamlValue keyValue)
            {
                throw new YamlException(Mark.Empty, Mark.Empty, "Only scalar mapping keys are supported for extension data.");
            }

            WriteExtensionEntry(writer, keyValue.Value, pair.Value, typeof(YamlNode));
        }
    }

    private static void WriteExtensionEntry(YamlWriter writer, string key, object? value, Type valueType)
    {
        writer.WritePropertyName(key);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var converter = writer.GetConverter(valueType);
        converter.Write(writer, value);
    }

    private static bool IsIgnored(MemberInfo member, out YamlIgnoreCondition? ignoreCondition)
    {
        ignoreCondition = null;

        if (member.IsDefined(typeof(YamlIgnoreAttribute), inherit: true))
        {
            ignoreCondition = YamlIgnoreCondition.WhenWritingDefault;
            return true;
        }

        var jsonIgnore = member.GetCustomAttribute<JsonIgnoreAttribute>(inherit: true);
        if (jsonIgnore is not null)
        {
            if (jsonIgnore.Condition == JsonIgnoreCondition.Always)
            {
                ignoreCondition = YamlIgnoreCondition.WhenWritingDefault;
                return true;
            }

            ignoreCondition = jsonIgnore.Condition switch
            {
                JsonIgnoreCondition.WhenWritingNull => YamlIgnoreCondition.WhenWritingNull,
                JsonIgnoreCondition.WhenWritingDefault => YamlIgnoreCondition.WhenWritingDefault,
                _ => null,
            };
        }

        return false;
    }

    private static bool IsRequired(MemberInfo member)
    {
        if (member.IsDefined(typeof(YamlRequiredAttribute), inherit: true))
        {
            return true;
        }

        if (member.IsDefined(typeof(JsonRequiredAttribute), inherit: true))
        {
            return true;
        }

        return false;
    }

    private static bool IsExtensionData(MemberInfo member)
    {
        if (member.IsDefined(typeof(YamlExtensionDataAttribute), inherit: true))
        {
            return true;
        }

        if (member.IsDefined(typeof(JsonExtensionDataAttribute), inherit: true))
        {
            return true;
        }

        return false;
    }

    private static YamlConverter? CreateConverterFromAttribute(MemberInfo member, Type memberType, YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(memberType);
        ArgumentNullException.ThrowIfNull(options);

        var attribute = member.GetCustomAttribute<YamlConverterAttribute>(inherit: true);
        if (attribute is null)
        {
            return null;
        }

        var converterType = attribute.ConverterType;
        if (converterType.IsGenericTypeDefinition)
        {
            throw new NotSupportedException($"Converter type '{converterType}' cannot be an open generic type.");
        }

        if (!typeof(YamlConverter).IsAssignableFrom(converterType))
        {
            throw new NotSupportedException($"Converter type '{converterType}' must derive from '{typeof(YamlConverter)}'.");
        }

        var converter = (YamlConverter)Activator.CreateInstance(converterType)!;
        if (converter is YamlConverterFactory factory)
        {
            var created = factory.CreateConverter(memberType, options);
            if (created is null || !created.CanConvert(memberType))
            {
                throw new InvalidOperationException($"Converter factory '{factory.GetType()}' returned an invalid converter for '{memberType}'.");
            }

            return created;
        }

        if (!converter.CanConvert(memberType))
        {
            throw new NotSupportedException($"Converter '{converterType}' cannot handle '{memberType}'.");
        }

        return converter;
    }

    private static string GetMemberName(MemberInfo member, YamlReaderWriterBase readerWriter)
    {
        var yamlName = member.GetCustomAttribute<YamlPropertyNameAttribute>(inherit: true);
        if (yamlName is not null)
        {
            return yamlName.Name;
        }

        var jsonName = member.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true);
        if (jsonName is not null)
        {
            return jsonName.Name;
        }

        var name = member.Name;
        return readerWriter.ConvertName(name);
    }

    private static int GetMemberOrder(MemberInfo member)
    {
        var yamlOrder = member.GetCustomAttribute<YamlPropertyOrderAttribute>(inherit: true);
        if (yamlOrder is not null)
        {
            return yamlOrder.Order;
        }

        var jsonOrder = member.GetCustomAttribute<JsonPropertyOrderAttribute>(inherit: true);
        if (jsonOrder is not null)
        {
            return jsonOrder.Order;
        }

        return 0;
    }
}
