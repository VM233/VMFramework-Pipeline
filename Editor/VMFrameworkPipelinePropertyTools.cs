#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VMUnityAutomation.Editor;
using VMFramework.Properties;
using static VMFramework.Pipeline.Editor.VMFrameworkPipelineGamePrefabTools;
using static VMFramework.Pipeline.Editor.VMFrameworkPipelineTools;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor
{
    [InitializeOnLoad]
    public static class VMFrameworkPipelinePropertyTools
    {
        private const string GET_PROPERTY_TOOL_NAME = "vmframework/get-property";
        private const string SET_PROPERTY_TOOL_NAME = "vmframework/set-property";
        private const string START_PROPERTY_TRACE_TOOL_NAME = "vmframework/start-property-trace";
        private const string GET_PROPERTY_TRACE_TOOL_NAME = "vmframework/get-property-trace";
        private const string STOP_PROPERTY_TRACE_TOOL_NAME = "vmframework/stop-property-trace";

        private const string PROPERTY_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"string\",\"description\":\"Exact decimal PropertyManager object id.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Scene GameObject path or name.\"}," +
            "\"managerIndex\":{\"type\":\"integer\",\"description\":\"PropertyManager index under the GameObject. Defaults to 0.\"}," +
            "\"includeChildren\":{\"type\":\"boolean\",\"description\":\"Resolve PropertyManagers below the selected GameObject. Defaults to true.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Exact property name.\"}" +
            "},\"required\":[\"propertyName\"],\"additionalProperties\":false}";

        private const string SET_PROPERTY_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"string\",\"description\":\"Exact decimal PropertyManager object id.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Scene GameObject path or name.\"}," +
            "\"managerIndex\":{\"type\":\"integer\",\"description\":\"PropertyManager index under the GameObject. Defaults to 0.\"}," +
            "\"includeChildren\":{\"type\":\"boolean\",\"description\":\"Resolve PropertyManagers below the selected GameObject. Defaults to true.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Exact writable property name.\"}," +
            "\"value\":{\"$ref\":\"#/$defs/vmJsonValue\",\"description\":\"Typed value. Unity Object values accept an asset path or {assetPath}.\"}," +
            "\"initial\":{\"type\":\"boolean\",\"description\":\"Pass initial=true to SetObjectValue. Defaults to false.\"}" +
            "},\"required\":[\"propertyName\",\"value\"],\"additionalProperties\":false}";

        private const string START_TRACE_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"string\",\"description\":\"Exact decimal PropertyManager object id.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Scene GameObject path or name. The current selection is used when both selectors are omitted.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Optional exact property filter.\"}," +
            "\"includeChildren\":{\"type\":\"boolean\",\"description\":\"Trace child PropertyManagers. Defaults to true.\"}," +
            "\"maxEvents\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":10000,\"description\":\"Maximum retained events. Uses Preferences > VMFramework Pipeline when omitted; initially 1000.\",\"x-vmAutomationDefaultSource\":\"Preferences > VMFramework Pipeline > Property Trace\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string READ_TRACE_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Event offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":10000,\"description\":\"Maximum returned events. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 100.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string PROPERTY_OUTPUT_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"string\"},\"gameObjectPath\":{\"type\":\"string\"}," +
            "\"propertyName\":{\"type\":\"string\"},\"propertyType\":{\"type\":\"string\"}," +
            "\"valueType\":{\"type\":\"string\"},\"writable\":{\"type\":\"boolean\"}," +
            "\"value\":" + VMFrameworkPipelineSchemaJson.ValueReference + ",\"valueError\":{\"type\":\"string\"}" +
            "},\"required\":[\"managerInstanceID\",\"gameObjectPath\",\"propertyName\",\"propertyType\",\"valueType\",\"writable\",\"value\",\"valueError\"],\"additionalProperties\":false}";

        private const string SET_PROPERTY_OUTPUT_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"managerInstanceID\":{\"type\":\"string\"},\"gameObjectPath\":{\"type\":\"string\"}," +
            "\"propertyName\":{\"type\":\"string\"},\"propertyType\":{\"type\":\"string\"}," +
            "\"valueType\":{\"type\":\"string\"},\"initial\":{\"type\":\"boolean\"}," +
            "\"before\":" + VMFrameworkPipelineSchemaJson.ValueReference + ",\"after\":" + VMFrameworkPipelineSchemaJson.ValueReference + "," +
            "\"beforeValueError\":{\"type\":\"string\"},\"afterValueError\":{\"type\":\"string\"}" +
            "},\"required\":[\"managerInstanceID\",\"gameObjectPath\",\"propertyName\",\"propertyType\",\"valueType\",\"initial\",\"before\",\"after\",\"beforeValueError\",\"afterValueError\"],\"additionalProperties\":false}";

        private const string START_TRACE_OUTPUT_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\"},\"active\":{\"type\":\"boolean\"}," +
            "\"targetCount\":{\"type\":\"integer\"},\"maxEvents\":{\"type\":\"integer\"}" +
            "},\"required\":[\"action\",\"active\",\"targetCount\",\"maxEvents\"],\"additionalProperties\":false}";

        private const string READ_TRACE_OUTPUT_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\"},\"active\":{\"type\":\"boolean\"}," +
            "\"targetCount\":{\"type\":\"integer\"},\"events\":{\"type\":\"array\",\"items\":{" +
            "\"type\":\"object\",\"properties\":{\"sequence\":{\"type\":\"integer\"},\"time\":{\"type\":\"number\"}," +
            "\"initial\":{\"type\":\"boolean\"},\"managerInstanceID\":{\"type\":\"string\"}," +
            "\"gameObjectPath\":{\"type\":\"string\"},\"propertyName\":{\"type\":\"string\"}," +
            "\"value\":" + VMFrameworkPipelineSchemaJson.ValueReference + ",\"valueError\":{\"type\":\"string\"}}," +
            "\"required\":[\"sequence\",\"time\",\"initial\",\"managerInstanceID\",\"gameObjectPath\",\"propertyName\",\"value\",\"valueError\"],\"additionalProperties\":false}}," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"},\"offset\":{\"type\":\"integer\"}," +
            "\"limit\":{\"type\":\"integer\"},\"nextOffset\":{\"type\":[\"integer\",\"null\"]}," +
            "\"stoppedTargetCount\":{\"type\":\"integer\"}" +
            "},\"required\":[\"action\",\"active\",\"targetCount\",\"events\",\"count\",\"total\",\"offset\",\"limit\",\"nextOffset\"],\"additionalProperties\":false}";

        private static readonly Dictionary<IReadOnlyProperty, PropertyTraceTarget> propertyTraceTargets = new();
        private static readonly List<Dictionary<string, object>> propertyTraceEvents = new();
        private static int propertyTraceMaxEvents = 1000;
        private static long propertyTraceSequence;
        private static bool propertyTraceActive;

        static VMFrameworkPipelinePropertyTools()
        {
            EditorApplication.playModeStateChanged -= OnPropertyTracePlayModeChanged;
            EditorApplication.playModeStateChanged += OnPropertyTracePlayModeChanged;
        }

        [VmProjectTool(GET_PROPERTY_TOOL_NAME,
            Description = "Read one VMFramework PropertyManager property with its concrete property type and value type.",
            InputSchemaJson = PROPERTY_SCHEMA,
            OutputSchemaJson = PROPERTY_OUTPUT_SCHEMA,
            ReadOnly = true)]
        public static object GetProperty(Dictionary<string, object> args)
        {
            args ??= new();
            var manager = ResolvePropertyManager(args);
            var propertyName = GetRequiredString(args, "propertyName");
            if (!manager.Properties.TryGetValue(propertyName, out var property))
                throw new KeyNotFoundException($"Property '{propertyName}' was not found on '{GetGameObjectPath(manager.transform)}'.");
            return DescribeTypedProperty(manager, propertyName, property);
        }

        [VmProjectTool(SET_PROPERTY_TOOL_NAME,
            Description = "Set one writable VMFramework runtime property in Play Mode using its concrete value type.",
            InputSchemaJson = SET_PROPERTY_SCHEMA,
            OutputSchemaJson = SET_PROPERTY_OUTPUT_SCHEMA,
            MutatesRuntime = true,
            RequiresPlayMode = true)]
        public static object SetProperty(Dictionary<string, object> args)
        {
            args ??= new();
            if (!Application.isPlaying)
                throw new InvalidOperationException("set-property requires Play Mode.");

            var manager = ResolvePropertyManager(args);
            var propertyName = GetRequiredString(args, "propertyName");
            if (!manager.Properties.TryGetValue(propertyName, out var readOnlyProperty))
                throw new KeyNotFoundException($"Property '{propertyName}' was not found.");
            if (readOnlyProperty is not IProperty property)
                throw new InvalidOperationException($"Property '{propertyName}' is read-only ({readOnlyProperty.GetType().FullName}).");
            if (!args.TryGetValue("value", out var rawValue)) throw new ArgumentException("value is required.");

            var before = DescribeTypedProperty(manager, propertyName, readOnlyProperty);
            var valueType = GetPropertyValueType(readOnlyProperty);
            var converted = ConvertSerializedValue(rawValue, valueType, propertyName);
            bool initial = GetBool(args, "initial", false);
            property.SetObjectValue(converted, initial);
            var after = DescribeTypedProperty(manager, propertyName, readOnlyProperty);
            return new Dictionary<string, object>
            {
                { "managerInstanceID", before["managerInstanceID"] },
                { "gameObjectPath", before["gameObjectPath"] },
                { "propertyName", propertyName },
                { "propertyType", before["propertyType"] },
                { "valueType", before["valueType"] },
                { "initial", initial },
                { "before", before["value"] },
                { "after", after["value"] },
                { "beforeValueError", before["valueError"] },
                { "afterValueError", after["valueError"] },
            };
        }

        [VmProjectTool(START_PROPERTY_TRACE_TOOL_NAME,
            Description = "Start tracing dirty events from selected VMFramework PropertyManager properties.",
            InputSchemaJson = START_TRACE_SCHEMA,
            OutputSchemaJson = START_TRACE_OUTPUT_SCHEMA,
            MutatesRuntime = true)]
        public static object StartPropertyTrace(Dictionary<string, object> args)
        {
            args ??= new();
            StopPropertyTraceInternal();
            propertyTraceEvents.Clear();
            propertyTraceSequence = 0;
            propertyTraceMaxEvents = VMFrameworkPipelineSettingsManager.ResolvePreferenceInt(
                args, "maxEvents", VMFrameworkPipelineSettingsManager.PropertyTraceMaxEvents, 1, 10000);
            var propertyName = GetString(args, "propertyName");
            var managers = ResolvePropertyManagers(args);
            if (managers.Count == 0)
                throw new InvalidOperationException("No PropertyManager matched the trace selectors.");

            foreach (var manager in managers)
            {
                foreach (var pair in manager.Properties)
                {
                    if (!string.IsNullOrWhiteSpace(propertyName) && pair.Key != propertyName) continue;
                    if (propertyTraceTargets.ContainsKey(pair.Value)) continue;
                    propertyTraceTargets[pair.Value] = new PropertyTraceTarget(manager, pair.Key);
                    pair.Value.OnDirty += OnTracedPropertyDirty;
                }
            }

            if (propertyTraceTargets.Count == 0)
                throw new KeyNotFoundException(
                    string.IsNullOrWhiteSpace(propertyName)
                        ? "No traceable properties were found."
                        : $"Property '{propertyName}' was not found on the matched PropertyManagers.");

            propertyTraceActive = true;
            return DescribePropertyTraceStatus("start");
        }

        [VmProjectTool(GET_PROPERTY_TRACE_TOOL_NAME,
            Description = "Return retained VMFramework property-change trace events.",
            InputSchemaJson = READ_TRACE_SCHEMA,
            OutputSchemaJson = READ_TRACE_OUTPUT_SCHEMA,
            ReadOnly = true)]
        public static object GetPropertyTrace(Dictionary<string, object> args)
        {
            args ??= new();
            return DescribePropertyTrace("get", args);
        }

        [VmProjectTool(STOP_PROPERTY_TRACE_TOOL_NAME,
            Description = "Stop VMFramework property-change tracing and return retained events.",
            InputSchemaJson = READ_TRACE_SCHEMA,
            OutputSchemaJson = READ_TRACE_OUTPUT_SCHEMA,
            MutatesRuntime = true)]
        public static object StopPropertyTrace(Dictionary<string, object> args)
        {
            args ??= new();
            var stoppedTargetCount = propertyTraceTargets.Count;
            StopPropertyTraceInternal();
            var result = DescribePropertyTrace("stop", args);
            result["stoppedTargetCount"] = stoppedTargetCount;
            return result;
        }

        private static PropertyManager ResolvePropertyManager(Dictionary<string, object> args)
        {
            var managers = ResolvePropertyManagers(args);
            if (managers.Count == 0) throw new InvalidOperationException("No PropertyManager matched the request.");
            var index = GetInt(args, "managerIndex", 0);
            if (index < 0 || index >= managers.Count) throw new IndexOutOfRangeException($"managerIndex {index} is invalid.");
            return managers[index];
        }

        private static List<PropertyManager> ResolvePropertyManagers(Dictionary<string, object> args)
        {
            string objectID = GetString(args, "managerInstanceID");
            if (string.IsNullOrWhiteSpace(objectID) == false &&
                objectID != "0")
            {
                var manager = VmObjectId.ToObject(objectID) as PropertyManager;
                return manager == null ? new List<PropertyManager>() : new List<PropertyManager> { manager };
            }

            var gameObjectPath = GetString(args, "gameObjectPath");
            GameObject root = null;
            if (!string.IsNullOrWhiteSpace(gameObjectPath)) root = FindSceneGameObject(gameObjectPath);
            else if (Selection.activeGameObject != null) root = Selection.activeGameObject;
            if (root == null) return new List<PropertyManager>();

            var managers = new List<PropertyManager>();
            AddPropertyManagers(root, managers, GetBool(args, "includeChildren", true));
            return managers.Where(manager => manager != null).Distinct().ToList();
        }

        private static Dictionary<string, object> DescribeTypedProperty(PropertyManager manager, string name,
            IReadOnlyProperty property)
        {
            object value;
            string error = "";
            try { value = property.ObjectValue; }
            catch (Exception ex) { value = null; error = ex.Message; }
            return new Dictionary<string, object>
            {
                { "managerInstanceID", VmObjectId.Get(manager) },
                { "gameObjectPath", GetGameObjectPath(manager.transform) },
                { "propertyName", name }, { "propertyType", property.GetType().FullName },
                { "valueType", GetPropertyValueType(property).FullName },
                { "writable", property is IProperty }, { "value", DescribeValue(value) }, { "valueError", error }
            };
        }

        private static Type GetPropertyValueType(IReadOnlyProperty property)
        {
            var propertyInterface = property.GetType().GetInterfaces().FirstOrDefault(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyProperty<>));
            return propertyInterface?.GetGenericArguments()[0] ?? property.ObjectValue?.GetType() ?? typeof(object);
        }

        private static void OnTracedPropertyDirty(IReadOnlyProperty property, bool initial)
        {
            if (!propertyTraceTargets.TryGetValue(property, out var target)) return;
            if (propertyTraceEvents.Count >= propertyTraceMaxEvents) propertyTraceEvents.RemoveAt(0);
            object value;
            string valueError = "";
            try
            {
                value = DescribeValue(property.ObjectValue);
            }
            catch (Exception ex)
            {
                value = null;
                valueError = ex.Message;
            }
            propertyTraceEvents.Add(new Dictionary<string, object>
            {
                { "sequence", propertyTraceSequence++ }, { "time", EditorApplication.timeSinceStartup },
                { "initial", initial }, { "managerInstanceID", VmObjectId.Get(target.Manager) },
                { "gameObjectPath", GetGameObjectPath(target.Manager.transform) },
                { "propertyName", target.PropertyName }, { "value", value }, { "valueError", valueError },
            });
        }

        private static Dictionary<string, object> DescribePropertyTrace(string action,
            Dictionary<string, object> args)
        {
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 100, 10000);
            var events = propertyTraceEvents.Skip(offset).Take(limit).ToList();
            return new Dictionary<string, object>
            {
                { "action", action }, { "active", propertyTraceActive },
                { "targetCount", propertyTraceTargets.Count },
                { "events", events },
                { "count", events.Count },
                { "total", propertyTraceEvents.Count },
                { "offset", offset },
                { "limit", limit },
                { "nextOffset", offset + events.Count < propertyTraceEvents.Count ? (object)(offset + events.Count) : null },
            };
        }

        private static Dictionary<string, object> DescribePropertyTraceStatus(string action)
        {
            return new Dictionary<string, object>
            {
                { "action", action },
                { "active", propertyTraceActive },
                { "targetCount", propertyTraceTargets.Count },
                { "maxEvents", propertyTraceMaxEvents },
            };
        }

        private static void StopPropertyTraceInternal()
        {
            foreach (var property in propertyTraceTargets.Keys) property.OnDirty -= OnTracedPropertyDirty;
            propertyTraceTargets.Clear();
            propertyTraceActive = false;
        }

        private static void OnPropertyTracePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                StopPropertyTraceInternal();
        }

        private readonly struct PropertyTraceTarget
        {
            public readonly PropertyManager Manager;
            public readonly string PropertyName;
            public PropertyTraceTarget(PropertyManager manager, string propertyName)
            {
                Manager = manager;
                PropertyName = propertyName;
            }
        }
    }
}
#endif
