using System;
using System.Collections.Generic;
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
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalarParser.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return default;
        }

        if (reader.TokenType != YamlTokenType.StartMapping)
        {
            throw new InvalidOperationException($"Expected a mapping token but found '{reader.TokenType}'.");
        }

        var contract = _contract ??= Contract.Create(typeof(T), options);
        var instance = (T)contract.CreateInstance();
        HashSet<Member>? seenMembers = options.DuplicateKeyHandling == YamlDuplicateKeyHandling.LastWins ? null : new HashSet<Member>();

        reader.Read();
        while (reader.TokenType != YamlTokenType.EndMapping)
        {
            if (reader.TokenType != YamlTokenType.Scalar)
            {
                throw new InvalidOperationException($"Expected a scalar key token but found '{reader.TokenType}'.");
            }

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
                throw new InvalidOperationException($"Duplicate mapping key '{key}'.");
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

    public override void Write(YamlWriter writer, T? value, YamlSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var contract = _contract ??= Contract.Create(typeof(T), options);
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

    private sealed class Contract
    {
        private readonly Dictionary<string, Member> _membersByName;

        public Contract(
            Func<object> createInstance,
            Member[] membersDeclaration,
            Member[] membersSorted,
            Dictionary<string, Member> membersByName)
        {
            CreateInstance = createInstance;
            MembersDeclaration = membersDeclaration;
            MembersSorted = membersSorted;
            _membersByName = membersByName;
        }

        public Func<object> CreateInstance { get; }

        public Member[] MembersDeclaration { get; }

        public Member[] MembersSorted { get; }

        public static Contract Create(Type type, YamlSerializerOptions options)
        {
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                throw new NotSupportedException($"Type '{type}' does not have a public parameterless constructor.");
            }

            object CreateInstance() => Activator.CreateInstance(type)!;

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

            return new Contract((Func<object>)CreateInstance, membersDeclaration, membersSorted, map);
        }

        public bool TryGetMember(string name, out Member member) => _membersByName.TryGetValue(name, out member!);
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
