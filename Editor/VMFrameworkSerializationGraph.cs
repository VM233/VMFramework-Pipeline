using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor
{
    internal sealed class VMFrameworkSerializationGraph
    {
        private const int MaximumValues = 65536;
        private const int MaximumDepth = 64;
        private const int MaximumFields = 128;
        private readonly Dictionary<object, int> encoded = new(new IdentityComparer());
        private readonly Dictionary<int, object> decoded = new();
        // Type metadata is immutable during this graph's single Editor invocation.
        private readonly Dictionary<Type, (FieldInfo[] Fields, Dictionary<string, FieldInfo> Names,
            HashSet<string> TransientFields)> contracts = new();
        private int values;
        private int metadataFields;

        public JObject Capture(Object asset)
        {
            return EncodeObject(asset, 0);
        }

        public void Restore(Object asset, JObject graph)
        {
            if (ResolveType((string)graph["$type"]) != asset.GetType())
            {
                throw new InvalidOperationException("The snapshot root type changed.");
            }
            RestoreFields(asset, (JObject)graph["fields"], 0);
        }

        private (FieldInfo[] Fields, Dictionary<string, FieldInfo> Names, HashSet<string> TransientFields) Contract(Type type)
        {
            if (contracts.TryGetValue(type, out var cached)) return cached;
            var result = new List<FieldInfo>();
            var transientFields = new HashSet<string>(StringComparer.Ordinal);
            for (Type current = type; current != null && current != typeof(object) &&
                 current != typeof(ScriptableObject) && current != typeof(MonoBehaviour) &&
                 !current.Assembly.GetName().Name.StartsWith("Sirenix.", StringComparison.Ordinal);
                 current = current.BaseType)
            {
                FieldInfo[] declared = current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in declared.Where(field => !field.IsStatic && field.IsNotSerialized))
                    transientFields.Add(field.Name);
                result.AddRange(declared.Where(field => !field.IsStatic && !field.IsNotSerialized &&
                    (field.IsPublic || field.IsDefined(typeof(SerializeField), false) ||
                     field.IsDefined(typeof(SerializeReference), false))));
            }
            if (result.Count + transientFields.Count > MaximumFields ||
                result.GroupBy(field => field.Name).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException($"Invalid or oversized serialized field contract: {type}.");
            }
            metadataFields += result.Count + transientFields.Count;
            if (metadataFields > MaximumValues)
            {
                throw new InvalidOperationException("The graph has too many serialized field definitions.");
            }
            FieldInfo[] fields = result.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
            var names = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo field in fields)
            {
                names.Add(field.Name, field);
                if (field.DeclaringType.FullName == "VMFramework.GameLogicArchitecture.GamePrefab" &&
                    field.Name == "nativeGameTags") names.Add("gameTags", field);
                if (field.DeclaringType.FullName == "VMFramework.GameEvents.InputSystemGameEventConfig" &&
                    field.Name == "nativeInputActionID") names.Add("inputActionID", field);
                if (field.DeclaringType.FullName == "VMFramework.GameLogicArchitecture.GamePrefabGeneralSetting" &&
                    field.Name == "initialGamePrefabProviderObjects") names.Add("initialGamePrefabProviders", field);
                foreach (FormerlySerializedAsAttribute former in field.GetCustomAttributes<FormerlySerializedAsAttribute>())
                {
                    names.Add(former.oldName, field);
                }
            }
            var contract = (fields, names, transientFields);
            contracts.Add(type, contract);
            return contract;
        }

        private JToken Encode(object value, Type declaredType, int depth)
        {
            Count(depth);
            if (value == null)
            {
                return declaredType == typeof(string) ? new JValue(string.Empty) : JValue.CreateNull();
            }
            Type type = value.GetType();
            if (value is Type systemType)
            {
                return new JObject { ["$systemType"] = systemType.AssemblyQualifiedName };
            }
            if (value is Guid guid)
            {
                return new JObject { ["$guid"] = guid.ToString("D") };
            }
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
            {
                return JToken.FromObject(value);
            }
            if (value is Object unityObject)
            {
                if (unityObject == null)
                {
                    return JValue.CreateNull();
                }
                GlobalObjectId identity = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
                if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity) != unityObject)
                {
                    throw new InvalidOperationException($"Unresolvable persistent Unity reference: {unityObject}.");
                }
                return new JObject { ["$unity"] = identity.ToString() };
            }
            if (IsNativeValue(type))
            {
                object box = CreateNativeBox(type, value);
                return new JObject { ["$nativeType"] = type.AssemblyQualifiedName,
                    ["json"] = JsonUtility.ToJson(box) };
            }
            if (!type.IsValueType && encoded.TryGetValue(value, out int id))
            {
                return new JObject { ["$ref"] = id };
            }
            var node = new JObject { ["$type"] = type.AssemblyQualifiedName };
            if (!type.IsValueType)
            {
                node["$id"] = encoded.Count + 1;
                encoded.Add(value, (int)node["$id"]);
            }
            if (IsDictionary(type))
            {
                var entries = new JArray();
                Type[] arguments = type.GetGenericArguments();
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    entries.Add(new JArray(Encode(entry.Key, arguments[0], depth + 1),
                        Encode(entry.Value, arguments[1], depth + 1)));
                }
                node["entries"] = entries;
            }
            else if (IsCollection(type))
            {
                var items = new JArray();
                Type elementType = ElementType(type);
                foreach (object item in (IEnumerable)value)
                {
                    items.Add(Encode(item, elementType, depth + 1));
                }
                node["items"] = items;
            }
            else
            {
                node["fields"] = EncodeFields(value, depth);
            }
            return node;
        }

        private JObject EncodeObject(object value, int depth)
        {
            return new JObject { ["$type"] = value.GetType().AssemblyQualifiedName,
                ["fields"] = EncodeFields(value, depth) };
        }

        private JObject EncodeFields(object value, int depth)
        {
            var fields = new JObject();
            foreach (FieldInfo field in Contract(value.GetType()).Fields)
            {
                fields.Add(field.Name, Encode(field.GetValue(value), field.FieldType, depth + 1));
            }
            return fields;
        }

        private object Decode(JToken token, Type expectedType, int depth, bool managedReference = false)
        {
            Count(depth);
            if (token.Type == JTokenType.Null)
            {
                // Unity persists inline null classes and collections as empty values. Managed
                // references and Unity object references retain their distinct null identity.
                if (expectedType == typeof(string)) return string.Empty;
                if (expectedType.IsArray) return Array.CreateInstance(expectedType.GetElementType(), 0);
                if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(List<>))
                    return Activator.CreateInstance(expectedType);
                if (!managedReference && !typeof(Object).IsAssignableFrom(expectedType) &&
                    expectedType.IsClass && expectedType.IsDefined(typeof(SerializableAttribute), false))
                    return JsonUtility.FromJson("{}", expectedType);
                return null;
            }
            if (!(token is JObject node))
            {
                return token.ToObject(expectedType);
            }
            if (node.TryGetValue("$unity", out JToken identityToken))
            {
                if (!GlobalObjectId.TryParse((string)identityToken, out GlobalObjectId identity))
                {
                    throw new InvalidOperationException($"Invalid Unity reference: {identityToken}.");
                }
                Object result = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity);
                if (result == null || !expectedType.IsInstanceOfType(result))
                {
                    throw new InvalidOperationException($"Missing or incompatible Unity reference: {identity}.");
                }
                return result;
            }
            if (node.TryGetValue("$systemType", out JToken typeToken))
            {
                Type value = ResolveType((string)typeToken);
                if (expectedType == typeof(Type))
                {
                    return value;
                }
                if (expectedType.FullName != "VMFramework.Configuration.SerializableType")
                {
                    throw new InvalidOperationException($"System.Type has no migration into {expectedType}.");
                }
                return Activator.CreateInstance(expectedType, value);
            }
            if (node.TryGetValue("$guid", out JToken guidToken))
            {
                Guid value = Guid.Parse((string)guidToken);
                if (expectedType == typeof(Guid)) return value;
                if (expectedType == typeof(string)) return value.ToString("D");
                throw new InvalidOperationException($"Guid has no migration into {expectedType}.");
            }
            if (node.TryGetValue("$nativeType", out JToken nativeTypeToken))
            {
                Type valueType = ResolveType((string)nativeTypeToken);
                if (valueType != expectedType)
                {
                    throw new InvalidOperationException($"Native value type changed: {valueType} -> {expectedType}.");
                }
                Type boxType = typeof(NativeBox<>).MakeGenericType(valueType);
                object box = JsonUtility.FromJson((string)node["json"], boxType);
                return boxType.GetField(nameof(NativeBox<int>.value)).GetValue(box);
            }
            if (node.TryGetValue("$ref", out JToken reference))
            {
                object value = decoded[(int)reference];
                if (!expectedType.IsInstanceOfType(value))
                {
                    throw new InvalidOperationException($"Shared reference changed type: {expectedType}.");
                }
                return value;
            }
            Type storedType = ResolveType((string)node["$type"]);
            Type targetType = node["items"] != null || node["entries"] != null ? expectedType : storedType;
            if (!expectedType.IsAssignableFrom(targetType))
            {
                throw new InvalidOperationException($"Stored type {targetType} is incompatible with {expectedType}.");
            }
            object instance = targetType.IsArray
                ? Array.CreateInstance(targetType.GetElementType(), ((JArray)node["items"]).Count)
                : IsCollection(targetType) || IsDictionary(targetType)
                    ? Activator.CreateInstance(targetType, true)
                    : JsonUtility.FromJson("{}", targetType);
            if (node["$id"] != null) decoded.Add((int)node["$id"], instance);
            if (node["items"] is JArray items)
            {
                Type elementType = ElementType(targetType);
                MethodInfo add = targetType.IsArray ? null : targetType.GetMethod("Add", new[] { elementType });
                for (int index = 0; index < items.Count; index++)
                {
                    object value = Decode(items[index], elementType, depth + 1, managedReference);
                    if (instance is Array array) array.SetValue(value, index);
                    else add.Invoke(instance, new[] { value });
                }
            }
            else if (node["entries"] is JArray entries)
            {
                Type[] arguments = targetType.GetGenericArguments();
                foreach (JArray entry in entries)
                {
                    ((IDictionary)instance).Add(Decode(entry[0], arguments[0], depth + 1),
                        Decode(entry[1], arguments[1], depth + 1));
                }
            }
            else RestoreFields(instance, (JObject)node["fields"], depth);
            return instance;
        }

        private void RestoreFields(object target, JObject data, int depth)
        {
            var contract = Contract(target.GetType());
            foreach (JProperty member in data.Properties())
            {
                if (contract.TransientFields.Contains(member.Name)) continue;
                if (!contract.Names.TryGetValue(member.Name, out FieldInfo field))
                {
                    throw new MissingFieldException(target.GetType().FullName, member.Name);
                }
                field.SetValue(target, Decode(member.Value, field.FieldType, depth + 1,
                    field.IsDefined(typeof(SerializeReference), false)));
            }
            if (target is ISerializationCallbackReceiver receiver) receiver.OnAfterDeserialize();
        }

        private void Count(int depth)
        {
            if (++values > MaximumValues || depth > MaximumDepth)
            {
                throw new InvalidOperationException("The asset graph exceeds the snapshot contract (65536 values, depth 64).");
            }
        }

        private static bool IsCollection(Type type) => type.IsArray || type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(HashSet<>));

        private static bool IsDictionary(Type type) => type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Dictionary<,>);

        private static Type ElementType(Type type) => type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

        private static Type ResolveType(string identifier) => Type.GetType(identifier, true);

        private static bool IsNativeValue(Type type) =>
            type.Assembly.GetName().Name.StartsWith("UnityEngine.", StringComparison.Ordinal);

        private static object CreateNativeBox(Type type, object value)
        {
            Type boxType = typeof(NativeBox<>).MakeGenericType(type);
            object box = Activator.CreateInstance(boxType);
            boxType.GetField(nameof(NativeBox<int>.value)).SetValue(box, value);
            return box;
        }

        [Serializable]
        private sealed class NativeBox<T>
        {
            public T value = default;
        }

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
