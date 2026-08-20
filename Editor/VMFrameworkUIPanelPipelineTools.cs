#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VMUnityAutomation.Editor;
using VMFramework.Configuration;
using VMFramework.Containers;
using VMFramework.GameLogicArchitecture;
using VMFramework.GameLogicArchitecture.Editor;
using VMFramework.OdinExtensions;
using VMFramework.Properties;
using VMFramework.UI;
using Object = UnityEngine.Object;
using static VMFramework.Pipeline.Editor.VMFrameworkPipelineTools;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkUIPanelPipelineTools
    {
        private const string INSPECT_UI_PANEL_TOOL_NAME = "vmframework/inspect-ui-panel";
        private const string INSPECT_BIND_OBJECTS_TOOL_NAME = "vmframework/inspect-bind-objects";
        private const string VALIDATE_VISUAL_ELEMENT_PATHS_TOOL_NAME = "vmframework/validate-visual-element-paths";
        private const string INSPECT_CONTAINER_PANEL_TOOL_NAME = "vmframework/inspect-container-panel";
        private const string INSPECT_PROPERTY_MANAGER_TOOL_NAME = "vmframework/inspect-property-manager";
        private const string PANEL_SELECTOR_PROPERTIES_JSON =
            "\"panelID\":{\"type\":\"string\",\"minLength\":1,\"description\":\"UIPanelConfig id.\"}," +
            "\"prefabPath\":{\"type\":\"string\",\"minLength\":1,\"description\":\"Panel prefab asset path.\"}";

        private const string PANEL_SELECTOR_ONE_OF_JSON =
            "\"oneOf\":[" +
            "{\"required\":[\"panelID\"],\"not\":{\"required\":[\"prefabPath\"]}}," +
            "{\"required\":[\"prefabPath\"],\"not\":{\"required\":[\"panelID\"]}}" +
            "]";

        private const string PANEL_SOURCE_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            PANEL_SELECTOR_PROPERTIES_JSON + "," +
            "\"includeRuntime\":{\"type\":\"boolean\",\"description\":\"Include runtime unique panel state when Play Mode is running. Defaults to false and remains request-owned.\"}" +
            "}," + PANEL_SELECTOR_ONE_OF_JSON + ",\"additionalProperties\":false}";

        private const string VALIDATE_VISUAL_ELEMENT_PATHS_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            PANEL_SELECTOR_PROPERTIES_JSON + "," +
            "\"allPanels\":{\"type\":\"boolean\",\"description\":\"Validate every registered or prefab-backed VMFramework panel. Must be true and cannot be combined with panelID or prefabPath.\"}," +
            "\"includeValid\":{\"type\":\"boolean\",\"description\":\"Include valid paths in the result. Defaults to false and remains request-owned.\"}," +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Reported-path offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned path records. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 100.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"oneOf\":[" +
            "{\"required\":[\"panelID\"],\"not\":{\"anyOf\":[{\"required\":[\"prefabPath\"]},{\"required\":[\"allPanels\"]}]}}," +
            "{\"required\":[\"prefabPath\"],\"not\":{\"anyOf\":[{\"required\":[\"panelID\"]},{\"required\":[\"allPanels\"]}]}}," +
            "{\"required\":[\"allPanels\"],\"properties\":{\"allPanels\":{\"const\":true,\"description\":\"Select every registered or prefab-backed panel.\"}},\"not\":{\"anyOf\":[{\"required\":[\"panelID\"]},{\"required\":[\"prefabPath\"]}]}}" +
            "],\"additionalProperties\":false}";

        private const string INSPECT_PROPERTY_MANAGER_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"prefabPath\":{\"type\":\"string\",\"description\":\"Prefab asset path whose PropertyManagers should be inspected.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Slash-separated scene GameObject path or GameObject name.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Optional exact property name filter.\"}," +
            "\"includeChildren\":{\"type\":\"boolean\",\"description\":\"Inspect child PropertyManagers. Defaults to true.\"}," +
            "\"useSelection\":{\"type\":\"boolean\",\"description\":\"Use selected GameObjects when prefabPath and gameObjectPath are omitted. Defaults to false so omitted selectors scan loaded scenes deterministically.\"}," +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Manager offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned managers. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 50.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string INSPECT_UI_PANEL_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"panelID\":{\"type\":\"string\"}," +
            "\"config\":" + VMFrameworkPipelineSchemaJson.NullableMap + "," +
            "\"prefab\":" + VMFrameworkPipelineSchemaJson.NullableMap + "," +
            "\"bindObjects\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"runtime\":" + VMFrameworkPipelineSchemaJson.NullableMap +
            "},\"required\":[\"panelID\",\"config\",\"prefab\",\"bindObjects\",\"runtime\"],\"additionalProperties\":false}";

        private const string INSPECT_BIND_OBJECTS_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"panelID\":{\"type\":\"string\"},\"prefabPath\":{\"type\":\"string\"}," +
            "\"managers\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"managerCount\":{\"type\":\"integer\"}," +
            "\"runtime\":" + VMFrameworkPipelineSchemaJson.NullableMap +
            "},\"required\":[\"panelID\",\"prefabPath\",\"managers\",\"managerCount\",\"runtime\"],\"additionalProperties\":false}";

        private const string VALIDATE_VISUAL_ELEMENT_PATHS_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"mode\":{\"type\":\"string\"},\"valid\":{\"type\":\"boolean\"}," +
            "\"error\":{\"type\":\"string\"},\"panelID\":{\"type\":\"string\"}," +
            "\"prefabPath\":{\"type\":\"string\"},\"visualTreeAssetPath\":{\"type\":\"string\"}," +
            "\"panelCount\":{\"type\":\"integer\"},\"invalidPanelCount\":{\"type\":\"integer\"}," +
            "\"missingPrefabCount\":{\"type\":\"integer\"},\"missingVisualTreeCount\":{\"type\":\"integer\"}," +
            "\"checkedCount\":{\"type\":\"integer\"},\"invalidPathCount\":{\"type\":\"integer\"}," +
            "\"invalidCount\":{\"type\":\"integer\"},\"paths\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"}," +
            "\"offset\":{\"type\":\"integer\"},\"limit\":{\"type\":\"integer\"}," +
            "\"nextOffset\":{\"type\":[\"integer\",\"null\"]}" +
            "},\"required\":[\"valid\"],\"additionalProperties\":false}";

        private const string INSPECT_CONTAINER_PANEL_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"panelID\":{\"type\":\"string\"},\"prefabPath\":{\"type\":\"string\"}," +
            "\"containerPanelModifiers\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"count\":{\"type\":\"integer\"}" +
            "},\"required\":[\"panelID\",\"prefabPath\",\"containerPanelModifiers\",\"count\"],\"additionalProperties\":false}";

        private const string INSPECT_PROPERTY_MANAGER_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"sourceType\":{\"type\":\"string\"},\"propertyName\":{\"type\":\"string\"}," +
            "\"includeChildren\":{\"type\":\"boolean\"},\"managers\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"}," +
            "\"offset\":{\"type\":\"integer\"},\"limit\":{\"type\":\"integer\"}," +
            "\"nextOffset\":{\"type\":[\"integer\",\"null\"]}" +
            "},\"required\":[\"sourceType\",\"propertyName\",\"includeChildren\",\"managers\",\"count\",\"total\",\"offset\",\"limit\",\"nextOffset\"],\"additionalProperties\":false}";

        [VmProjectTool(INSPECT_UI_PANEL_TOOL_NAME,
            Description = "Inspect a VMFramework UI panel prefab/config, UIDocument, VisualTreeAsset, modifiers, bind object names, and optional runtime state.",
            InputSchemaJson = PANEL_SOURCE_INPUT_SCHEMA_JSON,
            OutputSchemaJson = INSPECT_UI_PANEL_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectUIPanel(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);

            return new Dictionary<string, object>
            {
                { "panelID", source.panelID ?? "" },
                { "config", DescribePanelConfig(source.config) },
                { "prefab", DescribePanelPrefab(source.prefab) },
                { "bindObjects", InspectBindObjects(source, includeRuntime: GetBool(args, "includeRuntime", false)) },
                { "runtime", InspectRuntimePanel(source.panelID, GetBool(args, "includeRuntime", false)) }
            };
        }

        [VmProjectTool(INSPECT_BIND_OBJECTS_TOOL_NAME,
            Description = "Inspect VMFramework BindObjectsManager names, single-mode names, providers, and optional runtime bound object counts for a UI panel.",
            InputSchemaJson = PANEL_SOURCE_INPUT_SCHEMA_JSON,
            OutputSchemaJson = INSPECT_BIND_OBJECTS_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectBindObjects(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);
            return InspectBindObjects(source, includeRuntime: GetBool(args, "includeRuntime", false));
        }

        [VmProjectTool(VALIDATE_VISUAL_ELEMENT_PATHS_TOOL_NAME,
            Description = "Validate VisualElementPath fields on one or every VMFramework UI panel prefab against its UIDocument VisualTreeAsset.",
            InputSchemaJson = VALIDATE_VISUAL_ELEMENT_PATHS_INPUT_SCHEMA_JSON,
            OutputSchemaJson = VALIDATE_VISUAL_ELEMENT_PATHS_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object ValidateVisualElementPaths(Dictionary<string, object> args)
        {
            args ??= new();
            bool includeValid = GetBool(args, "includeValid", false);
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 100, 5000);

            if (args.ContainsKey("allPanels"))
            {
                if (GetBool(args, "allPanels", false) == false)
                {
                    throw new ArgumentException("allPanels must be true when provided.");
                }

                if (string.IsNullOrWhiteSpace(GetString(args, "panelID")) == false ||
                    string.IsNullOrWhiteSpace(GetString(args, "prefabPath")) == false)
                {
                    throw new ArgumentException(
                        "allPanels cannot be combined with panelID or prefabPath.");
                }

                return ValidateAllVisualElementPaths(includeValid, offset, limit);
            }

            var source = ResolvePanelSource(args);
            var validation = ValidateVisualElementPaths(source, includeValid);
            if (validation.error != null)
            {
                return new Dictionary<string, object>
                {
                    { "valid", false },
                    { "error", validation.error },
                    { "panelID", source.panelID ?? "" },
                    { "prefabPath", GetAssetPath(source.prefab) }
                };
            }

            var results = validation.reportedPaths.Skip(offset).Take(limit).ToList();

            return new Dictionary<string, object>
            {
                { "valid", validation.invalidCount == 0 },
                { "invalidCount", validation.invalidCount },
                { "checkedCount", validation.checkedCount },
                { "panelID", source.panelID ?? "" },
                { "prefabPath", GetAssetPath(source.prefab) },
                { "visualTreeAssetPath", GetAssetPath(validation.visualTree) },
                { "paths", results },
                { "count", results.Count },
                { "total", validation.reportedPaths.Count },
                { "offset", offset },
                { "limit", limit },
                {
                    "nextOffset",
                    offset + results.Count < validation.reportedPaths.Count
                        ? (object)(offset + results.Count)
                        : null
                },
            };
        }

        [VmProjectTool(INSPECT_CONTAINER_PANEL_TOOL_NAME,
            Description = "Inspect VMFramework UIToolkitContainerModifierBase components, bind object names, slot distributor configs, and optional runtime slot/container state.",
            InputSchemaJson = PANEL_SOURCE_INPUT_SCHEMA_JSON,
            OutputSchemaJson = INSPECT_CONTAINER_PANEL_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectContainerPanel(Dictionary<string, object> args)
        {
            args ??= new();
            var source = ResolvePanelSource(args);
            bool includeRuntime = GetBool(args, "includeRuntime", false);

            var modifiers = source.prefab.GetComponentsInChildren<UIToolkitContainerModifierBase>(true)
                .Select(modifier => DescribeContainerModifier(modifier, includeRuntime))
                .ToList();

            return new Dictionary<string, object>
            {
                { "panelID", source.panelID ?? "" },
                { "prefabPath", GetAssetPath(source.prefab) },
                { "containerPanelModifiers", modifiers },
                { "count", modifiers.Count }
            };
        }

        [VmProjectTool(INSPECT_PROPERTY_MANAGER_TOOL_NAME,
            Description = "Inspect VMFramework PropertyManager components on a prefab, selected GameObjects, a scene GameObject path, or loaded scenes.",
            InputSchemaJson = INSPECT_PROPERTY_MANAGER_INPUT_SCHEMA_JSON,
            OutputSchemaJson = INSPECT_PROPERTY_MANAGER_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectPropertyManager(Dictionary<string, object> args)
        {
            args ??= new();
            string prefabPath = GetString(args, "prefabPath");
            string gameObjectPath = GetString(args, "gameObjectPath");
            string propertyName = GetString(args, "propertyName");
            bool includeChildren = GetBool(args, "includeChildren", true);
            bool useSelection = GetBool(args, "useSelection", false);
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 50, 5000);

            var managers = new List<PropertyManager>();
            string sourceType;

            if (string.IsNullOrWhiteSpace(prefabPath) == false)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new ArgumentException($"Could not load prefab at '{prefabPath}'.");
                }

                sourceType = "prefab";
                AddPropertyManagers(prefab, managers, includeChildren);
            }
            else if (string.IsNullOrWhiteSpace(gameObjectPath) == false)
            {
                var gameObject = FindSceneGameObject(gameObjectPath);
                if (gameObject == null)
                {
                    throw new ArgumentException($"Could not find scene GameObject '{gameObjectPath}'.");
                }

                sourceType = "sceneGameObject";
                AddPropertyManagers(gameObject, managers, includeChildren);
            }
            else if (useSelection && Selection.gameObjects.Length > 0)
            {
                sourceType = "selection";
                foreach (var gameObject in Selection.gameObjects)
                {
                    AddPropertyManagers(gameObject, managers, includeChildren);
                }
            }
            else
            {
                sourceType = "loadedScenes";
                managers.AddRange(Object.FindObjectsByType<PropertyManager>(
                    FindObjectsInactive.Include));
            }

            var allManagers = managers
                .Where(manager => manager != null)
                .Distinct()
                .OrderBy(manager => GetGameObjectPath(manager.transform), StringComparer.Ordinal)
                .ToList();
            var distinctManagers = allManagers
                .Skip(offset)
                .Take(limit)
                .Select(manager => DescribePropertyManager(manager, propertyName))
                .ToList();

            return new Dictionary<string, object>
            {
                { "sourceType", sourceType },
                { "propertyName", propertyName ?? "" },
                { "includeChildren", includeChildren },
                { "managers", distinctManagers },
                { "count", distinctManagers.Count },
                { "total", allManagers.Count },
                { "offset", offset },
                { "limit", limit },
                { "nextOffset", offset + distinctManagers.Count < allManagers.Count ? (object)(offset + distinctManagers.Count) : null },
            };
        }
        private static PanelSource ResolvePanelSource(Dictionary<string, object> args)
        {
            string panelID = GetString(args, "panelID");
            string prefabPath = GetString(args, "prefabPath");
            bool hasPanelID = string.IsNullOrWhiteSpace(panelID) == false;
            bool hasPrefabPath = string.IsNullOrWhiteSpace(prefabPath) == false;

            if (hasPanelID == hasPrefabPath)
            {
                throw new ArgumentException(
                    "Exactly one panel selector is required: panelID or prefabPath.");
            }

            if (hasPrefabPath)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new ArgumentException($"Could not load prefab at '{prefabPath}'.");
                }

                return new PanelSource
                {
                    panelID = panelID,
                    prefab = prefab,
                    config = FindPanelConfigByPrefab(prefab)
                };
            }

            var info = FindGamePrefabInfos(panelID, null, typeof(UIPanelConfig), 1).FirstOrDefault();
            if (info?.gamePrefab is not UIPanelConfig config)
            {
                throw new ArgumentException($"Could not find UIPanelConfig with id '{panelID}'.");
            }

            if (config.prefab == null)
            {
                throw new InvalidOperationException($"UIPanelConfig '{panelID}' has no prefab.");
            }

            return new PanelSource
            {
                panelID = panelID,
                config = config,
                prefab = config.prefab,
                wrapper = info.wrapper
            };
        }

        private static object ValidateAllVisualElementPaths(bool includeValid, int offset, int limit)
        {
            var sources = FindAllPanelSources();
            var allPaths = new List<Dictionary<string, object>>();
            int checkedCount = 0;
            int invalidCount = 0;
            int invalidPanelCount = 0;
            int missingPrefabCount = 0;
            int missingVisualTreeCount = 0;

            foreach (var source in sources)
            {
                var validation = ValidateVisualElementPaths(source, includeValid);
                checkedCount += validation.checkedCount;
                invalidCount += validation.invalidCount;

                if (validation.error != null)
                {
                    invalidPanelCount++;
                    if (source.prefab == null)
                    {
                        missingPrefabCount++;
                    }
                    else
                    {
                        missingVisualTreeCount++;
                    }

                    allPaths.Add(new Dictionary<string, object>
                    {
                        { "valid", false },
                        { "panelID", source.panelID ?? "" },
                        { "prefabPath", GetAssetPath(source.prefab) },
                        { "error", validation.error }
                    });
                    continue;
                }

                if (validation.invalidCount > 0)
                {
                    invalidPanelCount++;
                }

                foreach (var path in validation.reportedPaths)
                {
                    path["panelID"] = source.panelID ?? "";
                    path["prefabPath"] = GetAssetPath(source.prefab);
                    path["visualTreeAssetPath"] = GetAssetPath(validation.visualTree);
                    allPaths.Add(path);
                }
            }

            var paths = allPaths.Skip(offset).Take(limit).ToList();
            return new Dictionary<string, object>
            {
                { "mode", "allPanels" },
                { "valid", invalidPanelCount == 0 },
                { "panelCount", sources.Count },
                { "invalidPanelCount", invalidPanelCount },
                { "missingPrefabCount", missingPrefabCount },
                { "missingVisualTreeCount", missingVisualTreeCount },
                { "checkedCount", checkedCount },
                { "invalidPathCount", invalidCount },
                { "invalidCount", invalidCount + missingPrefabCount + missingVisualTreeCount },
                { "paths", paths },
                { "count", paths.Count },
                { "total", allPaths.Count },
                { "offset", offset },
                { "limit", limit },
                {
                    "nextOffset",
                    offset + paths.Count < allPaths.Count
                        ? (object)(offset + paths.Count)
                        : null
                }
            };
        }

        private static List<PanelSource> FindAllPanelSources()
        {
            var sourcesByKey = new Dictionary<string, PanelSource>(StringComparer.OrdinalIgnoreCase);

            foreach (var info in FindGamePrefabInfos(null, null, typeof(UIPanelConfig), int.MaxValue))
            {
                if (info.gamePrefab is not UIPanelConfig config)
                {
                    continue;
                }

                string prefabPath = GetAssetPath(config.prefab);
                string key = string.IsNullOrWhiteSpace(prefabPath)
                    ? $"config:{config.id}"
                    : $"prefab:{prefabPath}";
                sourcesByKey[key] = new PanelSource
                {
                    panelID = config.id,
                    config = config,
                    prefab = config.prefab,
                    wrapper = info.wrapper
                };
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                string key = $"prefab:{prefabPath}";
                if (sourcesByKey.ContainsKey(key))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null || prefab.GetComponentInChildren<UIPanel>(true) == null)
                {
                    continue;
                }

                sourcesByKey[key] = new PanelSource
                {
                    panelID = null,
                    config = null,
                    prefab = prefab
                };
            }

            return sourcesByKey.Values
                .OrderBy(source => source.panelID ?? "", StringComparer.Ordinal)
                .ThenBy(source => GetAssetPath(source.prefab), StringComparer.Ordinal)
                .ToList();
        }

        private static PanelValidationResult ValidateVisualElementPaths(PanelSource source, bool includeValid)
        {
            var validation = new PanelValidationResult();
            if (source?.prefab == null)
            {
                validation.error = "Panel source has no prefab.";
                return validation;
            }

            validation.visualTree = GetVisualTreeAsset(source.prefab);
            if (validation.visualTree == null)
            {
                validation.error = "Panel prefab has no UIDocument VisualTreeAsset.";
                return validation;
            }

            var root = validation.visualTree.CloneTree();
            var records = new List<VisualElementPathRecord>();
            foreach (var component in source.prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                ScanVisualElementPaths(component,
                    GetGameObjectPath(component.transform) + "/" + component.GetType().Name,
                    records, new HashSet<object>(ReferenceEqualityComparer.Instance), 0, null);
            }

            validation.checkedCount = records.Count;
            foreach (var record in records)
            {
                var result = ValidateVisualElementPath(root, record);
                bool isValid = (bool)result["valid"];
                if (isValid == false)
                {
                    validation.invalidCount++;
                }

                if (includeValid || isValid == false)
                {
                    validation.reportedPaths.Add(result);
                }
            }

            return validation;
        }

        private static UIPanelConfig FindPanelConfigByPrefab(GameObject prefab)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            foreach (var info in FindGamePrefabInfos(null, null, typeof(UIPanelConfig), int.MaxValue))
            {
                if (info.gamePrefab is UIPanelConfig config &&
                    config.prefab != null &&
                    AssetDatabase.GetAssetPath(config.prefab) == prefabPath)
                {
                    return config;
                }
            }

            return null;
        }

        private static Dictionary<string, object> DescribePanelConfig(UIPanelConfig config)
        {
            if (config == null)
            {
                return null;
            }

            var result = DescribeGamePrefab(config);
            result["sortingOrder"] = config.sortingOrder;
            result["isUnique"] = config.isUnique;
            result["prefabPath"] = GetAssetPath(config.prefab);

            if (config is UIToolkitPanelConfig toolkitConfig)
            {
                result["useDefaultPanelSettings"] = toolkitConfig.useDefaultPanelSettings;
                result["customPanelSettingsPath"] = GetAssetPath(toolkitConfig.customPanelSettings);
                result["panelSettingsPath"] = GetSafePanelSettingsPath(toolkitConfig);
                result["ignoreMouseEvents"] = toolkitConfig.ignoreMouseEvents;
                result["closeMode"] = toolkitConfig.closeMode.ToString();
            }

            return result;
        }

        private static string GetSafePanelSettingsPath(UIToolkitPanelConfig config)
        {
            try
            {
                return GetAssetPath(config.PanelSettings);
            }
            catch (Exception ex)
            {
                if (config.useDefaultPanelSettings &&
                    GetGamePrefabGeneralSetting(typeof(UIPanelConfig)) is UIPanelGeneralSetting setting)
                {
                    string defaultPanelSettingsPath = GetAssetPath(setting.panelSettings);
                    if (string.IsNullOrWhiteSpace(defaultPanelSettingsPath) == false)
                    {
                        return defaultPanelSettingsPath;
                    }
                }

                return $"<unavailable: {ex.GetType().Name}>";
            }
        }

        private static Dictionary<string, object> DescribePanelPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            var uiDocument = prefab.GetComponentInChildren<UIDocument>(true);
            var modifiers = prefab.GetComponentsInChildren<IPanelModifier>(true)
                .OfType<Component>()
                .Select(DescribeComponent)
                .ToList();

            return new Dictionary<string, object>
            {
                { "name", prefab.name },
                { "path", GetAssetPath(prefab) },
                { "hasUIPanel", prefab.GetComponentInChildren<UIPanel>(true) != null },
                { "uiDocumentPath", uiDocument == null ? "" : GetGameObjectPath(uiDocument.transform) },
                { "visualTreeAssetPath", uiDocument?.visualTreeAsset == null ? "" : GetAssetPath(uiDocument.visualTreeAsset) },
                { "panelSettingsPath", uiDocument?.panelSettings == null ? "" : GetAssetPath(uiDocument.panelSettings) },
                { "bindObjectsManagerCount", prefab.GetComponentsInChildren<BindObjectsManager>(true).Length },
                { "panelModifierCount", modifiers.Count },
                { "panelModifiers", modifiers }
            };
        }

        private static Dictionary<string, object> InspectBindObjects(PanelSource source, bool includeRuntime)
        {
            var managerInfos = source.prefab.GetComponentsInChildren<BindObjectsManager>(true)
                .Select(manager => DescribeBindObjectsManager(manager, includeRuntime))
                .ToList();

            return new Dictionary<string, object>
            {
                { "panelID", source.panelID ?? "" },
                { "prefabPath", GetAssetPath(source.prefab) },
                { "managers", managerInfos },
                { "managerCount", managerInfos.Count },
                { "runtime", InspectRuntimeBindObjects(source.panelID, includeRuntime) }
            };
        }

        private static Dictionary<string, object> DescribeBindObjectsManager(BindObjectsManager manager,
            bool includeRuntime)
        {
            var names = new HashSet<string> { BindObjectsManager.GLOBAL_BIND_NAME };
            var singleModeNames = new HashSet<string>();
            var providers = new List<Dictionary<string, object>>();

            foreach (var provider in manager.GetComponentsInChildren<IBindObjectsNamesProvider>(true))
            {
                var beforeNames = names.Count;
                var beforeSingle = singleModeNames.Count;
                try
                {
                    provider.GetBindObjectsNames(names, singleModeNames);
                }
                catch (Exception ex)
                {
                    providers.Add(new Dictionary<string, object>
                    {
                        { "type", provider.GetType().FullName },
                        { "error", ex.Message }
                    });
                    continue;
                }

                providers.Add(new Dictionary<string, object>
                {
                    { "type", provider.GetType().FullName },
                    { "gameObjectPath", provider is Component component ? GetGameObjectPath(component.transform) : "" },
                    { "addedNameCount", names.Count - beforeNames },
                    { "addedSingleModeNameCount", singleModeNames.Count - beforeSingle },
                    { "details", DescribeBindObjectsNamesProvider(provider) }
                });
            }

            return new Dictionary<string, object>
            {
                { "gameObjectPath", GetGameObjectPath(manager.transform) },
                { "type", manager.GetType().FullName },
                { "names", names.OrderBy(name => name).ToList() },
                { "singleModeNames", singleModeNames.OrderBy(name => name).ToList() },
                { "providers", providers },
                { "isInitialized", manager.IsInitialized },
                { "runtimeObjectCounts", includeRuntime && manager.IsInitialized ? DescribeBindObjectCounts(manager, names) : null }
            };
        }

        private static Dictionary<string, object> DescribeBindObjectsNamesProvider(IBindObjectsNamesProvider provider)
        {
            if (provider is PreDefinedBindObjectsNames predefined)
            {
                return new Dictionary<string, object>
                {
                    { "names", predefined.names.ToArray() },
                    { "singleModeNames", predefined.singleModeNames.ToArray() },
                    { "useGameObjectNames", predefined.useGameObjectNames }
                };
            }

            return new Dictionary<string, object>();
        }

        private static Dictionary<string, object> InspectRuntimePanel(string panelID, bool includeRuntime)
        {
            if (includeRuntime == false || Application.isPlaying == false || string.IsNullOrWhiteSpace(panelID))
            {
                return null;
            }

            var panel = GetRuntimePanel(panelID);
            if (panel == null)
            {
                return new Dictionary<string, object>
                {
                    { "found", false },
                    { "isPlaying", Application.isPlaying }
                };
            }

            return new Dictionary<string, object>
            {
                { "found", true },
                { "id", panel.id },
                { "type", panel.GetType().FullName },
                { "isOpened", panel.IsOpened },
                { "isClosing", panel.IsClosing },
                { "uiEnabled", panel.UIEnabled },
                { "modifierCount", panel.Modifiers.Count },
                { "bindObjects", panel.BindObjectsManager == null ? null : DescribeBindObjectsManager(panel.BindObjectsManager, includeRuntime: true) }
            };
        }

        private static Dictionary<string, object> InspectRuntimeBindObjects(string panelID, bool includeRuntime)
        {
            if (includeRuntime == false || Application.isPlaying == false || string.IsNullOrWhiteSpace(panelID))
            {
                return null;
            }

            var panel = GetRuntimePanel(panelID);
            if (panel?.BindObjectsManager == null)
            {
                return new Dictionary<string, object> { { "found", false } };
            }

            return DescribeBindObjectsManager(panel.BindObjectsManager, includeRuntime: true);
        }

        private static IUIPanel GetRuntimePanel(string panelID)
        {
            return SafeGet(() =>
            {
                var manager = UIPanelManager.Instance;
                return manager != null && manager.TryGetUniquePanel(panelID, out var panel) ? panel : null;
            });
        }

        private static Dictionary<string, object> DescribeBindObjectCounts(BindObjectsManager manager,
            IEnumerable<string> names)
        {
            var counts = new Dictionary<string, object>();
            foreach (string name in names)
            {
                var objects = manager.GetObjects(name);
                counts[name] = new Dictionary<string, object>
                {
                    { "count", objects.Count },
                    { "objects", objects.Take(20).Select(DescribeRuntimeObject).ToList() }
                };
            }

            return counts;
        }

        private static Dictionary<string, object> DescribeContainerModifier(UIToolkitContainerModifierBase modifier,
            bool includeRuntime)
        {
            var configs = modifier.slotDistributorConfigs
                .Select(DescribeContainerSlotDistributorConfig)
                .ToList();

            return new Dictionary<string, object>
            {
                { "type", modifier.GetType().FullName },
                { "gameObjectPath", GetGameObjectPath(modifier.transform) },
                { "bindObjectsName", modifier.bindObjectsName ?? "" },
                { "slotDistributorConfigs", configs },
                { "slotDistributorConfigCount", configs.Count },
                { "isInitialized", modifier.IsInitialized },
                { "runtime", includeRuntime && modifier.IsInitialized ? DescribeRuntimeContainerModifier(modifier) : null }
            };
        }

        private static Dictionary<string, object> DescribeContainerSlotDistributorConfig(
            ContainerSlotDistributorConfig config)
        {
            var result = new Dictionary<string, object>
            {
                { "parentName", config.parentName ?? "" },
                { "findMethod", config.findMethod.ToString() },
                { "slotName", config.slotName ?? "" },
                { "removeExtraSlots", config.removeExtraSlots },
                { "isFinite", config.isFinite },
                { "startSlotIndex", config.startSlotIndex },
                { "slotIndexRange", DescribeRange(config.slotIndexRange) },
                { "autoFill", config.autoFill },
                { "hasCustomContainer", config.hasCustomContainer },
                { "customContainerName", config.customContainerName ?? "" },
                { "containerName", config.ContainerName ?? "" },
                { "startIndex", config.StartIndex },
                { "count", config.Count == int.MaxValue ? "int.MaxValue" : config.Count.ToString(CultureInfo.InvariantCulture) }
            };

            result["slotNameBindings"] = config.slotNameBindings
                .Select(binding => new Dictionary<string, object>
                {
                    { "slotName", binding.slotName ?? "" },
                    { "slotIndex", binding.slotIndex }
                })
                .ToList();

            return result;
        }

        private static Dictionary<string, object> DescribeRuntimeContainerModifier(UIToolkitContainerModifierBase modifier)
        {
            IContainer container = null;
            try
            {
                container = modifier.Panel?.BindObjectsManager?.GetObject(modifier.bindObjectsName) as IContainer;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "error", ex.Message },
                    { "slotCount", modifier.Slots.Count }
                };
            }

            return new Dictionary<string, object>
            {
                { "slotCount", modifier.Slots.Count },
                { "container", DescribeContainer(container) }
            };
        }

        private static Dictionary<string, object> DescribePropertyManager(PropertyManager manager,
            string propertyName)
        {
            var properties = manager.Properties
                .Where(pair => string.IsNullOrWhiteSpace(propertyName) || pair.Key == propertyName)
                .Select(pair => DescribeProperty(pair.Key, pair.Value))
                .ToList();

            return new Dictionary<string, object>
            {
                { "gameObjectPath", GetGameObjectPath(manager.transform) },
                { "type", manager.GetType().FullName },
                { "propertyCount", manager.Properties.Count },
                { "reportedPropertyCount", properties.Count },
                { "properties", properties }
            };
        }

        private static Dictionary<string, object> DescribeProperty(string name, IReadOnlyProperty property)
        {
            object value = null;
            string valueError = "";
            try
            {
                value = property.ObjectValue;
            }
            catch (Exception ex)
            {
                valueError = ex.Message;
            }

            return new Dictionary<string, object>
            {
                { "name", name },
                { "type", property.GetType().FullName },
                { "owner", DescribeRuntimeObject(property.Owner) },
                { "value", DescribeValue(value) },
                { "valueError", valueError }
            };
        }

        private static Dictionary<string, object> ValidateVisualElementPath(VisualElement root,
            VisualElementPathRecord record)
        {
            var names = record.path?.names ?? new List<string>();
            string joinedPath = string.Join("/", names);
            var result = new Dictionary<string, object>
            {
                { "owner", record.owner },
                { "member", record.member },
                { "path", joinedPath },
                { "required", record.required },
                { "expectedTypes", record.allowedTypes.Select(type => type.Name).ToArray() }
            };

            if (record.path == null || names.Count == 0)
            {
                result["valid"] = record.required == false;
                if (record.required)
                {
                    result["error"] = "Required VisualElementPath is empty.";
                }
                else
                {
                    result["skipped"] = true;
                    result["reason"] = "Optional VisualElementPath is empty.";
                }

                return result;
            }

            var element = record.path.Query(root);
            if (element == null)
            {
                result["valid"] = false;
                result["error"] = "VisualElementPath was not found.";
                return result;
            }

            if (record.allowedTypes.Count > 0 && record.allowedTypes.Any(type => type.IsInstanceOfType(element)) == false)
            {
                result["valid"] = false;
                result["error"] = $"VisualElement type '{element.GetType().Name}' does not match expected type.";
                result["actualType"] = element.GetType().Name;
                result["actualName"] = element.name;
                return result;
            }

            result["valid"] = true;
            result["actualType"] = element.GetType().Name;
            result["actualName"] = element.name;
            result["classList"] = element.GetClasses().ToArray();
            return result;
        }

        private static void ScanVisualElementPaths(object target, string owner,
            List<VisualElementPathRecord> records, HashSet<object> visited, int depth,
            VisualElementPathSettingsAttribute inheritedSettings)
        {
            if (target == null || depth > 5)
            {
                return;
            }

            Type targetType = target.GetType();
            if (ShouldRecurseInto(targetType, allowUnityObjectRoot: depth == 0) == false)
            {
                return;
            }

            if (target is not ValueType && visited.Add(target) == false)
            {
                return;
            }

            foreach (var field in GetSerializableFields(targetType))
            {
                if (OdinConditionalFieldUtility.IsActive(target, field) == false)
                {
                    continue;
                }

                object value;
                try
                {
                    value = field.GetValue(target);
                }
                catch
                {
                    continue;
                }

                var settings = field.GetCustomAttribute<VisualElementPathSettingsAttribute>() ?? inheritedSettings;
                string member = field.Name;

                if (value is VisualElementPath path)
                {
                    records.Add(new VisualElementPathRecord
                    {
                        owner = owner,
                        member = member,
                        path = path,
                        required = IsVisualElementPathRequired(field),
                        allowedTypes = GetAllowedTypes(settings)
                    });
                    continue;
                }

                // UnityEngine.Object instances can implement IEnumerable (Transform is the
                // common case), but their serialized object graphs are not nested config data.
                // Enumerating a missing/destroyed reference throws before ShouldRecurseInto can
                // reject it, so stop at every non-root Unity object boundary.
                if (value is Object)
                {
                    continue;
                }

                if (value is IEnumerable enumerable && value is not string)
                {
                    int index = 0;
                    foreach (object item in enumerable)
                    {
                        if (item is VisualElementPath itemPath)
                        {
                            records.Add(new VisualElementPathRecord
                            {
                                owner = owner,
                                member = $"{member}[{index}]",
                                path = itemPath,
                                required = IsVisualElementPathRequired(field),
                                allowedTypes = GetAllowedTypes(settings)
                            });
                        }
                        else
                        {
                            ScanVisualElementPaths(item, owner, records, visited, depth + 1, settings);
                        }

                        index++;
                        if (index > 500)
                        {
                            break;
                        }
                    }

                    continue;
                }

                ScanVisualElementPaths(value, owner, records, visited, depth + 1, settings);
            }
        }

        private static bool IsVisualElementPathRequired(FieldInfo field)
        {
            return field.IsDefined(typeof(IsNotNullOrEmptyAttribute), true);
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (current == typeof(MonoBehaviour) ||
                    current == typeof(Behaviour) ||
                    current == typeof(Component) ||
                    current == typeof(Object))
                {
                    yield break;
                }

                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic)
                    {
                        continue;
                    }

                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool ShouldRecurseInto(Type type, bool allowUnityObjectRoot = false)
        {
            if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                type == typeof(decimal))
            {
                return false;
            }

            if (typeof(Object).IsAssignableFrom(type))
            {
                return allowUnityObjectRoot && typeof(Component).IsAssignableFrom(type);
            }

            if (type == typeof(VisualElementPath))
            {
                return true;
            }

            string ns = type.Namespace ?? "";
            if (ns.StartsWith("System", StringComparison.Ordinal) ||
                ns.StartsWith("Unity", StringComparison.Ordinal) ||
                ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
                ns.StartsWith("Sirenix", StringComparison.Ordinal) ||
                ns.StartsWith("Newtonsoft", StringComparison.Ordinal))
            {
                return false;
            }

            return ns.Length > 0 || type.GetCustomAttribute<SerializableAttribute>() != null;
        }

        private static List<Type> GetAllowedTypes(VisualElementPathSettingsAttribute settings)
        {
            if (settings?.AllowedTypes == null)
            {
                return new List<Type> { typeof(VisualElement) };
            }

            return settings.AllowedTypes.Where(type => type != null).ToList();
        }

        private static VisualTreeAsset GetVisualTreeAsset(GameObject prefab)
        {
            var uiDocument = prefab == null ? null : prefab.GetComponentInChildren<UIDocument>(true);
            return uiDocument == null ? null : uiDocument.visualTreeAsset;
        }

        private sealed class PanelSource
        {
            public string panelID;
            public UIPanelConfig config;
            public GameObject prefab;
            public GamePrefabWrapper wrapper;
        }

        private sealed class PanelValidationResult
        {
            public string error;
            public VisualTreeAsset visualTree;
            public int checkedCount;
            public int invalidCount;
            public readonly List<Dictionary<string, object>> reportedPaths = new();
        }
        private sealed class VisualElementPathRecord
        {
            public string owner;
            public string member;
            public VisualElementPath path;
            public bool required;
            public List<Type> allowedTypes;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
#endif
