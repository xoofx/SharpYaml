using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlObjectConverter<T> : YamlConverter<T?>
{
    private readonly IYamlConverterResolver _resolver;
    private Contract? _contract;

    public YamlObjectConverter(IYamlConverterResolver resolver)
    {
        _resolver = resolver;
    }

    public override T? Read(ref YamlReader reader, YamlSerializerOptions options)
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
            throw YamlThrowHelper.ThrowExpectedMapping(ref reader);
        }

        Contract contract;
        try
        {
            contract = _contract ??= Contract.Create(typeof(T), options);
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
            return ReadPolymorphic(ref reader, options, contract);
        }

        return ReadObjectCore(ref reader, options, contract);
    }

    public override void Write(YamlWriter writer, T? value, YamlSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var contract = _contract ??= Contract.Create(typeof(T), options);

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

        var runtimeType = value.GetType();
        if (contract.Polymorphism is not null && runtimeType != typeof(T))
        {
            WritePolymorphic(writer, value, runtimeType, options, contract);
            return;
        }

        WriteObjectCore(writer, value, options, contract);
    }

    private T? ReadObjectCore(ref YamlReader reader, YamlSerializerOptions options, Contract contract)
    {
        var instance = (T)contract.CreateInstance();
        if (reader.ReferenceReader is not null && reader.Anchor is not null)
        {
            reader.ReferenceReader.Register(reader.Anchor, instance!);
        }

        HashSet<Member>? seenMembers = options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins ? null : new HashSet<Member>();

        reader.Read();
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw YamlThrowHelper.ThrowExpectedScalarKey(ref reader);
            }

            var keyStart = reader.Start;
            var keyEnd = reader.End;
            var key = reader.ScalarValue ?? string.Empty;
            reader.Read();

            if (!contract.TryGetMember(key, out var member))
            {
                reader.Skip();
                continue;
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

            var converter = member.Converter ??= _resolver.GetConverter(member.MemberType);
            var value = converter.Read(ref reader, member.MemberType, options);
            member.SetValue(instance!, value);
        }

        reader.Read();
        return instance;
    }

    private void WriteObjectCore(YamlWriter writer, object value, YamlSerializerOptions options, Contract contract)
    {
        writer.WriteStartMapping();

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
            var converter = member.Converter ??= _resolver.GetConverter(member.MemberType);
            converter.Write(writer, memberValue, options);
        }

        writer.WriteEndMapping();
    }

    private T? ReadPolymorphic(ref YamlReader reader, YamlSerializerOptions options, Contract contract)
    {
        var polymorphism = contract.Polymorphism!;
        var rootTag = reader.Tag;

        var buffered = BufferCurrentMappingToString(ref reader, options, polymorphism, out var discriminatorValue);

        Type? targetType = null;
        if (polymorphism.AcceptsPropertyDiscriminator && discriminatorValue is not null)
        {
            if (polymorphism.TryGetDerivedTypeFromDiscriminator(discriminatorValue, out var derived))
            {
                targetType = derived;
            }
            else if (polymorphism.UnknownDerivedTypeHandling == YamlUnknownDerivedTypeHandling.Fail)
            {
                throw YamlThrowHelper.ThrowUnknownTypeDiscriminator(ref reader, discriminatorValue, typeof(T));
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

        var bufferedReader = YamlReader.Create(buffered, reader.ReferenceReader, reader.SourceName);
        if (!bufferedReader.Read())
        {
            return default;
        }

        if (targetType == typeof(T))
        {
            return ReadObjectCore(ref bufferedReader, options, contract);
        }

        var converter = _resolver.GetConverter(targetType);
        var value = converter.Read(ref bufferedReader, targetType, options);
        return (T?)value;
    }

    private void WritePolymorphic(YamlWriter writer, object value, Type runtimeType, YamlSerializerOptions options, Contract contract)
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

        var derivedContract = Contract.Create(runtimeType, options);
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
            var converter = member.Converter ??= _resolver.GetConverter(member.MemberType);
            converter.Write(writer, memberValue, options);
        }

        writer.WriteEndMapping();
    }

    private static string BufferCurrentMappingToString(ref YamlReader reader, YamlSerializerOptions options, PolymorphismModel polymorphism, out string? discriminatorValue)
    {
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        discriminatorValue = null;

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var yamlWriter = new YamlWriter(writer, options);

        WriteBufferedNode(ref reader, yamlWriter, comparer, polymorphism.DiscriminatorPropertyName, isRootMapping: true, ref discriminatorValue);
        return writer.ToString();
    }

    private static void WriteBufferedNode(
        ref YamlReader reader,
        YamlWriter writer,
        StringComparer keyComparer,
        string discriminatorPropertyName,
        bool isRootMapping,
        ref string? discriminatorValue)
    {
        switch (reader.TokenType)
        {
            case YamlTokenType.Scalar:
                if (reader.Anchor is not null)
                {
                    writer.WriteAnchor(reader.Anchor);
                }

                if (reader.Tag is not null)
                {
                    writer.WriteTag(reader.Tag);
                }

                writer.WriteScalar(reader.ScalarValue);
                reader.Read();
                return;

            case YamlTokenType.Alias:
                var alias = reader.Alias;
                if (alias is null)
                {
                    throw YamlThrowHelper.ThrowAliasMissingValue(ref reader);
                }

                writer.WriteAlias(alias);
                reader.Read();
                return;

            case YamlTokenType.StartSequence:
                if (reader.Anchor is not null)
                {
                    writer.WriteAnchor(reader.Anchor);
                }

                if (reader.Tag is not null)
                {
                    writer.WriteTag(reader.Tag);
                }

                writer.WriteStartSequence();
                reader.Read();
                while (reader.TokenType != YamlTokenType.EndSequence)
                {
                    WriteBufferedNode(ref reader, writer, keyComparer, discriminatorPropertyName, isRootMapping: false, ref discriminatorValue);
                }
                writer.WriteEndSequence();
                reader.Read();
                return;

            case YamlTokenType.StartMapping:
                if (reader.Anchor is not null)
                {
                    writer.WriteAnchor(reader.Anchor);
                }

                if (reader.Tag is not null)
                {
                    writer.WriteTag(reader.Tag);
                }

                writer.WriteStartMapping();
                reader.Read();
                while (reader.TokenType != YamlTokenType.EndMapping)
                {
                    if (reader.TokenType != YamlTokenType.Scalar)
                    {
                        throw YamlThrowHelper.ThrowExpectedScalarKey(ref reader);
                    }

                    var key = reader.ScalarValue ?? string.Empty;
                    writer.WritePropertyName(key);
                    reader.Read();

                    if (isRootMapping && discriminatorValue is null && keyComparer.Equals(key, discriminatorPropertyName))
                    {
                        if (reader.TokenType != YamlTokenType.Scalar)
                        {
                            throw YamlThrowHelper.ThrowExpectedDiscriminatorScalar(ref reader, discriminatorPropertyName);
                        }

                        discriminatorValue = reader.ScalarValue ?? string.Empty;
                    }

                    WriteBufferedNode(ref reader, writer, keyComparer, discriminatorPropertyName, isRootMapping: false, ref discriminatorValue);
                }
                writer.WriteEndMapping();
                reader.Read();
                return;

            default:
                throw YamlThrowHelper.ThrowUnexpectedToken(ref reader);
        }
    }

    private sealed class Contract
    {
        private readonly Dictionary<string, Member> _membersByName;

        public Contract(
            Func<object> createInstance,
            Member[] membersDeclaration,
            Member[] membersSorted,
            Dictionary<string, Member> membersByName,
            PolymorphismModel? polymorphism)
        {
            CreateInstance = createInstance;
            MembersDeclaration = membersDeclaration;
            MembersSorted = membersSorted;
            _membersByName = membersByName;
            Polymorphism = polymorphism;
        }

        public Func<object> CreateInstance { get; }

        public Member[] MembersDeclaration { get; }

        public Member[] MembersSorted { get; }

        public PolymorphismModel? Polymorphism { get; }

        public static Contract Create(Type type, YamlSerializerOptions options)
        {
            object CreateInstance()
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    throw new NotSupportedException($"Type '{type}' cannot be instantiated.");
                }

                var instance = Activator.CreateInstance(type);
                if (instance is null)
                {
                    throw new NotSupportedException($"Type '{type}' does not have a public parameterless constructor.");
                }

                return instance;
            }

            var members = new List<Member>();

            const BindingFlags allInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var property in type.GetProperties(allInstance))
            {
                if (property.GetIndexParameters().Length != 0)
                {
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

                var name = GetMemberName(property, options);
                var order = GetMemberOrder(property);
                var token = property.MetadataToken;

                members.Add(new Member(name, order, token, property.PropertyType, property, ignoreCondition));
            }

            foreach (var field in type.GetFields(allInstance))
            {
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

                var name = GetMemberName(field, options);
                var order = GetMemberOrder(field);
                var token = field.MetadataToken;

                members.Add(new Member(name, order, token, field.FieldType, field, ignoreCondition));
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

            var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var map = new Dictionary<string, Member>(comparer);
            for (var i = 0; i < membersDeclaration.Length; i++)
            {
                var member = membersDeclaration[i];
                map[member.Name] = member;
            }

            var polymorphism = PolymorphismModel.TryCreate(type, options);
            return new Contract(CreateInstance, membersDeclaration, membersSorted, map, polymorphism);
        }

        public bool TryGetMember(string name, out Member member) => _membersByName.TryGetValue(name, out member!);
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

        public Member(string name, int order, int declarationOrder, Type memberType, PropertyInfo property, YamlIgnoreCondition? ignoreCondition)
        {
            Name = name;
            Order = order;
            DeclarationOrder = declarationOrder;
            MemberType = memberType;
            _property = property;
            _field = null;
            IgnoreCondition = ignoreCondition;
        }

        public Member(string name, int order, int declarationOrder, Type memberType, FieldInfo field, YamlIgnoreCondition? ignoreCondition)
        {
            Name = name;
            Order = order;
            DeclarationOrder = declarationOrder;
            MemberType = memberType;
            _property = null;
            _field = field;
            IgnoreCondition = ignoreCondition;
        }

        public string Name { get; }

        public int Order { get; }

        public int DeclarationOrder { get; }

        public Type MemberType { get; }

        public bool CanRead => _property?.GetMethod is not null || _field is not null;

        public YamlIgnoreCondition? IgnoreCondition { get; }

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

    private static string GetMemberName(MemberInfo member, YamlSerializerOptions options)
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
        return options.PropertyNamingPolicy?.ConvertName(name) ?? name;
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
