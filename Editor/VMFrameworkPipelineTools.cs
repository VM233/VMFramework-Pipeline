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

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkPipelineTools
    {
        private const string LIST_GAME_PREFAB_TYPES_TOOL_NAME = "vmframework/list-game-prefab-types";
        private const string ADD_GAME_PREFAB_TOOL_NAME = "vmframework/add-game-prefab";
        private const string FIND_GAME_PREFAB_TOOL_NAME = "vmframework/find-game-prefab";
        private const string INSPECT_GAME_PREFAB_WRAPPER_TOOL_NAME = "vmframework/inspect-game-prefab-wrapper";
        private const string LIST_GENERAL_SETTINGS_TOOL_NAME = "vmframework/list-general-settings";

        private const string LIST_GAME_PREFAB_TYPES_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"filter\":{\"type\":\"string\",\"description\":\"Optional case-insensitive type name filter.\"}," +
            "\"includeAbstract\":{\"type\":\"boolean\",\"description\":\"Include abstract and interface GamePrefab types. Defaults to false.\"}," +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Result offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned types. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 100.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string INSPECT_GAME_PREFAB_WRAPPER_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"GamePrefab id contained by the wrapper.\"}," +
            "\"wrapperPath\":{\"type\":\"string\",\"description\":\"Asset path of a GamePrefabWrapper.\"}," +
            "\"filter\":{\"type\":\"string\",\"description\":\"Optional wrapper path, wrapper name, GamePrefab id, or type filter.\"}," +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Result offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned wrappers. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 50.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string LIST_GENERAL_SETTINGS_INPUT_SCHEMA_JSON =
            "{\"type\":\"object\",\"properties\":{" +
            "\"filter\":{\"type\":\"string\",\"description\":\"Case-insensitive type, asset name, or path filter.\"}," +
            "\"includeGamePrefabDetails\":{\"type\":\"boolean\",\"description\":\"Include potentially large GamePrefabGeneralSetting provider details. Defaults to false and remains request-owned.\"}," +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Result offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned settings. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 100.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string LIST_GAME_PREFAB_TYPES_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"types\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"}," +
            "\"offset\":{\"type\":\"integer\"},\"limit\":{\"type\":\"integer\"}," +
            "\"nextOffset\":{\"type\":[\"integer\",\"null\"]}" +
            "},\"required\":[\"types\",\"count\",\"total\",\"offset\",\"limit\",\"nextOffset\"],\"additionalProperties\":false}";

        private const string INSPECT_GAME_PREFAB_WRAPPER_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"wrappers\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"}," +
            "\"offset\":{\"type\":\"integer\"},\"limit\":{\"type\":\"integer\"}," +
            "\"nextOffset\":{\"type\":[\"integer\",\"null\"]}" +
            "},\"required\":[\"wrappers\",\"count\",\"total\",\"offset\",\"limit\",\"nextOffset\"],\"additionalProperties\":false}";

        private const string LIST_GENERAL_SETTINGS_OUTPUT_SCHEMA_JSON =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"generalSettingsFolderPath\":{\"type\":\"string\"}," +
            "\"settings\":" + VMFrameworkPipelineSchemaJson.MapArray + "," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"}," +
            "\"offset\":{\"type\":\"integer\"},\"limit\":{\"type\":\"integer\"}," +
            "\"nextOffset\":{\"type\":[\"integer\",\"null\"]}" +
            "},\"required\":[\"generalSettingsFolderPath\",\"settings\",\"count\",\"total\",\"offset\",\"limit\",\"nextOffset\"],\"additionalProperties\":false}";


        [VmProjectTool(LIST_GAME_PREFAB_TYPES_TOOL_NAME,
            Description = "List VMFramework GamePrefab types and their matching GamePrefabGeneralSetting.",
            InputSchemaJson = LIST_GAME_PREFAB_TYPES_INPUT_SCHEMA_JSON,
            OutputSchemaJson = LIST_GAME_PREFAB_TYPES_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object ListGamePrefabTypes(Dictionary<string, object> args)
        {
            args ??= new();
            string filter = GetString(args, "filter");
            bool includeAbstract = GetBool(args, "includeAbstract", false);
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 100, 5000);

            var allTypes = GetGamePrefabTypes(includeAbstract)
                .Where(type => MatchesFilter(type.Name, filter) ||
                               MatchesFilter(type.FullName, filter) ||
                               MatchesFilter(type.AssemblyQualifiedName, filter))
                .Select(type =>
                {
                    var setting = GetGamePrefabGeneralSetting(type);
                    return new Dictionary<string, object>
                    {
                        { "name", type.Name },
                        { "fullName", type.FullName },
                        { "assemblyQualifiedName", type.AssemblyQualifiedName },
                        { "isAbstract", type.IsAbstract },
                        { "isInterface", type.IsInterface },
                        { "hasDefaultConstructor", type.GetConstructor(Type.EmptyTypes) != null },
                        { "generalSetting", DescribeGeneralSetting(setting, includeGamePrefabDetails: false) }
                    };
                })
                .OrderBy(info => info["fullName"])
                .ToList();
            var types = allTypes.Skip(offset).Take(limit).ToList();

            return new Dictionary<string, object>
            {
                { "types", types },
                { "count", types.Count },
                { "total", allTypes.Count },
                { "offset", offset },
                { "limit", limit },
                { "nextOffset", offset + types.Count < allTypes.Count ? (object)(offset + types.Count) : null },
            };
        }

        [VmProjectTool(ADD_GAME_PREFAB_TOOL_NAME,
            Description = "Create or replace a VMFramework GamePrefab wrapper by id and register it to the matching GamePrefabGeneralSetting.",
            MutatesAssets = true)]
        public static VMFrameworkAddGamePrefabResult AddGamePrefab(
            VMFrameworkAddGamePrefabRequest request)
        {
            return VMFrameworkGamePrefabAuthoring.CreateOrReplace(
                new VMFrameworkGamePrefabAuthoringRequest(
                    request.Id,
                    ResolveGamePrefabType(request.GamePrefabType),
                    request.Overwrite,
                    request.AssetName,
                    request.SerializedValues));
        }

        internal static IGamePrefab CreateGamePrefab(string id, Type gamePrefabType,
            Dictionary<string, object> serializedValues, List<string> warnings)
        {
            var gamePrefab = GamePrefabWrapperCreator.CreateDefaultGamePrefab(id, gamePrefabType);
            if (gamePrefab == null)
            {
                throw new InvalidOperationException(
                    $"Could not create GamePrefab of type '{gamePrefabType.FullName}'.");
            }

            if (serializedValues != null)
                ApplySerializedValues(gamePrefab, serializedValues);

            if (gamePrefab is GamePrefab typedPrefab)
            {
                if (typedPrefab.IsIDStartsWithPrefix == false)
                    warnings.Add($"ID '{id}' does not start with expected prefix '{typedPrefab.IDPrefix}'.");
                if (typedPrefab.IsIDEndsWithSuffix == false)
                    warnings.Add($"ID '{id}' does not end with expected suffix '{typedPrefab.IDSuffix}'.");
            }

            return gamePrefab;
        }

        [VmProjectTool(FIND_GAME_PREFAB_TOOL_NAME,
            Description = "Find VMFramework GamePrefabs by id, type, wrapper path, or filter.",
            ReadOnly = true)]
        public static VMFrameworkFindGamePrefabResult FindGamePrefab(
            VMFrameworkFindGamePrefabRequest request)
        {
            string typeName = request.GamePrefabType;
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                request.Limit, 100, 5000);
            Type typeFilter = string.IsNullOrWhiteSpace(typeName)
                ? null
                : ResolveGamePrefabType(typeName, allowAbstract: true);

            List<VMFrameworkGamePrefabReference> allReferences =
                FindGamePrefabInfos(request.Id, request.Filter, typeFilter, int.MaxValue)
                    .Select(CreateGamePrefabReference)
                    .ToList();
            List<VMFrameworkGamePrefabReference> references = allReferences
                .Skip(request.Offset).Take(limit).ToList();
            int nextOffset = request.Offset + references.Count;
            return new VMFrameworkFindGamePrefabResult
            {
                GamePrefabs = references,
                Count = references.Count,
                Total = allReferences.Count,
                Offset = request.Offset,
                Limit = limit,
                NextOffset = nextOffset < allReferences.Count ? nextOffset : (int?)null,
            };
        }

        [VmProjectTool(INSPECT_GAME_PREFAB_WRAPPER_TOOL_NAME,
            Description = "Inspect VMFramework GamePrefabWrapper assets and the GamePrefabs they contain.",
            InputSchemaJson = INSPECT_GAME_PREFAB_WRAPPER_INPUT_SCHEMA_JSON,
            OutputSchemaJson = INSPECT_GAME_PREFAB_WRAPPER_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object InspectGamePrefabWrapper(Dictionary<string, object> args)
        {
            args ??= new();
            string id = GetString(args, "id");
            string wrapperPath = GetString(args, "wrapperPath");
            string filter = GetString(args, "filter");
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 50, 5000);

            var wrappers = new List<GamePrefabWrapper>();
            if (string.IsNullOrWhiteSpace(wrapperPath) == false)
            {
                var wrapper = AssetDatabase.LoadAssetAtPath<GamePrefabWrapper>(wrapperPath);
                if (wrapper == null)
                    throw new ArgumentException($"Could not load a GamePrefabWrapper at '{wrapperPath}'.");
                wrappers.Add(wrapper);
            }
            else if (string.IsNullOrWhiteSpace(id) == false)
            {
                wrappers.AddRange(FindGamePrefabInfos(id, null, null, int.MaxValue)
                    .Select(info => info.wrapper)
                    .Where(wrapper => wrapper != null)
                    .Distinct());
                if (wrappers.Count == 0)
                    throw new KeyNotFoundException($"GamePrefab '{id}' was not found.");
            }
            else
            {
                wrappers.AddRange(GetAllGamePrefabWrappers()
                    .Where(wrapper => WrapperMatches(wrapper, filter)));
            }

            var allWrappers = wrappers
                .Where(wrapper => wrapper != null)
                .Distinct()
                .OrderBy(wrapper => AssetDatabase.GetAssetPath(wrapper), StringComparer.Ordinal)
                .ToList();
            var page = allWrappers.Skip(offset).Take(limit).ToList();
            return new Dictionary<string, object>
            {
                { "wrappers", page.Select(wrapper => DescribeWrapper(wrapper, includeGamePrefabs: true)).ToList() },
                { "count", page.Count },
                { "total", allWrappers.Count },
                { "offset", offset },
                { "limit", limit },
                { "nextOffset", offset + page.Count < allWrappers.Count ? (object)(offset + page.Count) : null },
            };
        }

        [VmProjectTool(LIST_GENERAL_SETTINGS_TOOL_NAME,
            Description = "List VMFramework GeneralSetting assets currently discoverable from global settings and the general settings asset folder.",
            InputSchemaJson = LIST_GENERAL_SETTINGS_INPUT_SCHEMA_JSON,
            OutputSchemaJson = LIST_GENERAL_SETTINGS_OUTPUT_SCHEMA_JSON,
            ReadOnly = true)]
        public static object ListGeneralSettings(Dictionary<string, object> args)
        {
            args ??= new();
            string filter = GetString(args, "filter");
            bool includeDetails = GetBool(args, "includeGamePrefabDetails", false);
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 100, 5000);

            var allSettings = GetAllGeneralSettings()
                .Where(setting =>
                {
                    if (setting is not Object obj)
                    {
                        return MatchesFilter(setting?.GetType().Name, filter) ||
                               MatchesFilter(setting?.GetType().FullName, filter);
                    }

                    return MatchesFilter(obj.name, filter) ||
                           MatchesFilter(AssetDatabase.GetAssetPath(obj), filter) ||
                           MatchesFilter(setting.GetType().Name, filter) ||
                           MatchesFilter(setting.GetType().FullName, filter);
                })
                .Select(setting => DescribeGeneralSetting(setting, includeDetails))
                .OrderBy(info => info["type"])
                .ToList();
            var settings = allSettings.Skip(offset).Take(limit).ToList();

            return new Dictionary<string, object>
            {
                { "generalSettingsFolderPath", SafeGet(() => EditorSetting.GeneralSettingsAssetFolderPath) ?? ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH },
                { "settings", settings },
                { "count", settings.Count },
                { "total", allSettings.Count },
                { "offset", offset },
                { "limit", limit },
                { "nextOffset", offset + settings.Count < allSettings.Count ? (object)(offset + settings.Count) : null },
            };
        }


        internal static GamePrefabWrapper CreateWrapper(IGamePrefab gamePrefab,
            GamePrefabGeneralSetting gamePrefabGeneralSetting, string assetName)
        {
            string path = string.IsNullOrWhiteSpace(assetName)
                ? CombineAssetPath(gamePrefabGeneralSetting.GamePrefabFolderPath, ToPascalAssetName(gamePrefab.id))
                : CombineAssetPath(gamePrefabGeneralSetting.GamePrefabFolderPath, assetName);

            var wrapper = GamePrefabWrapperCreator.CreateGamePrefabWrapper(path, GamePrefabWrapperType.Single,
                gamePrefab);
            if (wrapper == null)
            {
                throw new InvalidOperationException(
                    $"Could not create GamePrefab wrapper for id '{gamePrefab.id}' at '{path}'.");
            }

            return wrapper;
        }

        internal static void RegisterWrapper(GamePrefabGeneralSetting targetSetting, GamePrefabWrapper wrapper)
        {
            foreach (var setting in GetAllGamePrefabGeneralSettings())
            {
                if (setting == targetSetting)
                {
                    continue;
                }

                if (setting.initialGamePrefabProviders.Contains(wrapper))
                {
                    setting.RemoveFromInitialGamePrefabProviders(wrapper);
                }
            }

            targetSetting.AddToInitialGamePrefabProviders(wrapper);
        }

        internal static GamePrefabWrapper SaveAndRefresh(GamePrefabWrapper wrapper,
            GamePrefabGeneralSetting gamePrefabGeneralSetting)
        {
            string wrapperPath = GetAssetPath(wrapper);

            EditorUtility.SetDirty(wrapper);
            EditorUtility.SetDirty(gamePrefabGeneralSetting);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(wrapperPath) == false)
            {
                wrapper = AssetDatabase.LoadAssetAtPath<GamePrefabWrapper>(wrapperPath) ?? wrapper;
            }

            GamePrefabWrapperInitializeUtility.Refresh();
            return wrapper;
        }

        internal static void ValidateWrapperContainsGamePrefab(GamePrefabWrapper wrapper, string id)
        {
            var gamePrefabs = GetGamePrefabs(wrapper);
            if (gamePrefabs.Any(gamePrefab => gamePrefab != null &&
                                              string.Equals(gamePrefab.id, id, StringComparison.Ordinal)))
            {
                return;
            }

            throw new InvalidOperationException(
                $"GamePrefab wrapper '{GetAssetPath(wrapper)}' was saved but does not contain GamePrefab id '{id}'.");
        }

        internal static GamePrefabGeneralSetting ResolveGamePrefabGeneralSetting(IGamePrefab gamePrefab)
        {
            var gamePrefabGeneralSetting = GetGamePrefabGeneralSetting(gamePrefab.GetType());
            if (gamePrefabGeneralSetting == null)
            {
                throw new InvalidOperationException(
                    $"Could not find GamePrefabGeneralSetting for '{gamePrefab.GetType().FullName}'.");
            }

            return gamePrefabGeneralSetting;
        }

        internal static Type ResolveGamePrefabType(string typeName, bool allowAbstract = false)
        {
            var matches = GetGamePrefabTypes(includeAbstract: allowAbstract)
                .Where(type => string.Equals(type.AssemblyQualifiedName, typeName, StringComparison.Ordinal) ||
                               string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                               string.Equals(type.Name, typeName, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                throw new ArgumentException($"Could not find GamePrefab type '{typeName}'.");
            }

            if (matches.Count > 1)
            {
                throw new ArgumentException(
                    $"GamePrefab type '{typeName}' is ambiguous: {string.Join(", ", matches.Select(type => type.FullName))}");
            }

            return matches[0];
        }

        internal static List<Type> GetGamePrefabTypes(bool includeAbstract)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => typeof(IGamePrefab).IsAssignableFrom(type))
                .Where(type => includeAbstract || (type.IsAbstract == false &&
                                                   type.IsInterface == false &&
                                                   type.GetConstructor(Type.EmptyTypes) != null))
                .OrderBy(type => type.FullName)
                .ToList();
        }

        internal static GamePrefabGeneralSetting GetGamePrefabGeneralSetting(Type gamePrefabType)
        {
            foreach (var setting in GetAllGamePrefabGeneralSettings())
            {
                if (setting.BaseGamePrefabType.IsAssignableFrom(gamePrefabType))
                {
                    return setting;
                }
            }

            return null;
        }

        internal static List<GamePrefabGeneralSetting> GetAllGamePrefabGeneralSettings()
        {
            return GetAllGeneralSettings()
                .OfType<GamePrefabGeneralSetting>()
                .Where(setting => setting != null)
                .Distinct()
                .ToList();
        }

        internal static IEnumerable<IGeneralSetting> GetAllGeneralSettings()
        {
            var seen = new HashSet<Object>();

            foreach (var setting in SafeEnumerable(GlobalSettingCollector.GetAllGeneralSettings))
            {
                if (setting is Object obj)
                {
                    if (seen.Add(obj))
                    {
                        yield return setting;
                    }
                }
                else if (setting != null)
                {
                    yield return setting;
                }
            }

            foreach (string folder in GetGeneralSettingsSearchFolders())
            {
                if (AssetDatabase.IsValidFolder(folder) == false)
                {
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset is IGeneralSetting setting && seen.Add(asset))
                    {
                        yield return setting;
                    }
                }
            }
        }

        internal static IEnumerable<string> GetGeneralSettingsSearchFolders()
        {
            var folders = new[]
            {
                SafeGet(() => EditorSetting.GeneralSettingsAssetFolderPath),
                ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH
            };

            return folders.Where(folder => string.IsNullOrWhiteSpace(folder) == false).Distinct();
        }

        internal static IEnumerable<GamePrefabWrapper> GetAllGamePrefabWrappers()
        {
            return SafeEnumerable(GamePrefabWrapperQueryTools.GetAllGamePrefabWrappers)
                .Where(wrapper => wrapper != null);
        }

        internal static List<GamePrefabInfo> FindGamePrefabInfos(string id, string filter, Type gamePrefabType, int limit)
        {
            var infos = new List<GamePrefabInfo>();
            foreach (var wrapper in GetAllGamePrefabWrappers())
            {
                var gamePrefabs = GetGamePrefabs(wrapper);
                foreach (var gamePrefab in gamePrefabs)
                {
                    if (gamePrefab == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(id) == false &&
                        string.Equals(gamePrefab.id, id, StringComparison.Ordinal) == false)
                    {
                        continue;
                    }

                    if (gamePrefabType != null && gamePrefabType.IsAssignableFrom(gamePrefab.GetType()) == false)
                    {
                        continue;
                    }

                    string wrapperPath = AssetDatabase.GetAssetPath(wrapper);
                    if (MatchesGamePrefabFilter(gamePrefab, wrapper, wrapperPath, filter) == false)
                    {
                        continue;
                    }

                    infos.Add(new GamePrefabInfo
                    {
                        wrapper = wrapper,
                        wrapperPath = wrapperPath,
                        gamePrefab = gamePrefab
                    });

                    if (infos.Count >= limit)
                    {
                        return infos;
                    }
                }
            }

            return infos;
        }

        internal static bool MatchesGamePrefabFilter(IGamePrefab gamePrefab, GamePrefabWrapper wrapper,
            string wrapperPath, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return MatchesFilter(gamePrefab.id, filter) ||
                   MatchesFilter(gamePrefab.GetType().Name, filter) ||
                   MatchesFilter(gamePrefab.GetType().FullName, filter) ||
                   MatchesFilter(wrapper.name, filter) ||
                   MatchesFilter(wrapperPath, filter);
        }

        internal static bool WrapperMatches(GamePrefabWrapper wrapper, string filter)
        {
            if (wrapper == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(wrapper);
            if (MatchesFilter(wrapper.name, filter) || MatchesFilter(path, filter))
            {
                return true;
            }

            return GetGamePrefabs(wrapper).Any(gamePrefab => MatchesGamePrefabFilter(gamePrefab, wrapper, path, filter));
        }

        internal static List<IGamePrefab> GetGamePrefabs(GamePrefabWrapper wrapper)
        {
            var gamePrefabs = new List<IGamePrefab>();
            if (wrapper == null)
            {
                return gamePrefabs;
            }

            try
            {
                wrapper.GetGamePrefabs(gamePrefabs);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to get GamePrefabs from wrapper '{wrapper.name}': {ex.Message}", wrapper);
            }

            return gamePrefabs;
        }

        internal static void AddPropertyManagers(GameObject gameObject, ICollection<PropertyManager> managers,
            bool includeChildren)
        {
            if (includeChildren)
            {
                foreach (var manager in gameObject.GetComponentsInChildren<PropertyManager>(true))
                {
                    managers.Add(manager);
                }
            }
            else if (gameObject.TryGetComponent(out PropertyManager manager))
            {
                managers.Add(manager);
            }
        }

        internal static GameObject FindSceneGameObject(string pathOrName)
        {
            foreach (var root in GetSceneRoots())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    string path = GetGameObjectPath(transform);
                    if (string.Equals(path, pathOrName, StringComparison.Ordinal) ||
                        string.Equals(path.TrimStart('/'), pathOrName.TrimStart('/'), StringComparison.Ordinal) ||
                        string.Equals(transform.name, pathOrName, StringComparison.Ordinal))
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }

        internal static IEnumerable<GameObject> GetSceneRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded == false)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    yield return root;
                }
            }
        }

        internal static Dictionary<string, object> DescribeGamePrefabInfo(GamePrefabInfo info)
        {
            return new Dictionary<string, object>
            {
                { "gamePrefab", DescribeGamePrefab(info.gamePrefab) },
                { "wrapperPath", info.wrapperPath },
                { "wrapperName", info.wrapper == null ? "" : info.wrapper.name },
                { "generalSetting", DescribeGeneralSetting(GetGamePrefabGeneralSetting(info.gamePrefab.GetType()), includeGamePrefabDetails: false) }
            };
        }

        internal static Dictionary<string, object> DescribeGamePrefab(IGamePrefab gamePrefab)
        {
            if (gamePrefab == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "id", gamePrefab.id ?? "" },
                { "name", gamePrefab.Name ?? "" },
                { "type", gamePrefab.GetType().FullName },
                { "gameItemType", gamePrefab.GameItemType == null ? "" : gamePrefab.GameItemType.FullName },
                { "isActive", gamePrefab.IsActive },
                { "isDebugging", gamePrefab.IsDebugging },
                { "gameItemPrewarmCount", gamePrefab.GameItemPrewarmCount },
                { "idPrefix", gamePrefab.IDPrefix ?? "" },
                { "idSuffix", gamePrefab.IDSuffix ?? "" }
            };
        }

        internal static Dictionary<string, object> DescribeWrapper(GamePrefabWrapper wrapper,
            bool includeGamePrefabs)
        {
            if (wrapper == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>
            {
                { "name", wrapper.name },
                { "id", wrapper.id ?? "" },
                { "type", wrapper.GetType().FullName },
                { "path", GetAssetPath(wrapper) }
            };

            if (includeGamePrefabs)
            {
                var gamePrefabs = GetGamePrefabs(wrapper)
                    .Where(gamePrefab => gamePrefab != null)
                    .Select(DescribeGamePrefab)
                    .ToList();
                result["gamePrefabs"] = gamePrefabs;
                result["gamePrefabCount"] = gamePrefabs.Count;
            }

            return result;
        }

        internal static Dictionary<string, object> DescribeGeneralSetting(IGeneralSetting setting,
            bool includeGamePrefabDetails)
        {
            if (setting == null)
            {
                return null;
            }

            var obj = setting as Object;
            var result = new Dictionary<string, object>
            {
                { "name", obj == null ? setting.GetType().Name : obj.name },
                { "type", setting.GetType().FullName },
                { "path", obj == null ? "" : GetAssetPath(obj) },
                { "isGamePrefabGeneralSetting", setting is GamePrefabGeneralSetting }
            };

            if (includeGamePrefabDetails && setting is GamePrefabGeneralSetting gamePrefabSetting)
            {
                var providers = DescribeGamePrefabProviders(gamePrefabSetting.initialGamePrefabProviders,
                    out int providerSlotCount, out int missingProviderCount);

                result["gamePrefabName"] = gamePrefabSetting.GamePrefabName;
                result["baseGamePrefabType"] = gamePrefabSetting.BaseGamePrefabType.FullName;
                result["gamePrefabFolderPath"] = gamePrefabSetting.GamePrefabFolderPath;
                result["initialGamePrefabProviderSlotCount"] = providerSlotCount;
                result["initialGamePrefabProviderCount"] = providers.Count;
                result["missingInitialGamePrefabProviderCount"] = missingProviderCount;
                result["initialGamePrefabProviders"] = providers;
            }

            return result;
        }

        internal static List<Dictionary<string, object>> DescribeGamePrefabProviders(
            IEnumerable<IGamePrefabsProvider> rawProviders, out int providerSlotCount, out int missingProviderCount)
        {
            var providers = new List<Dictionary<string, object>>();
            providerSlotCount = 0;
            missingProviderCount = 0;

            if (rawProviders == null)
            {
                return providers;
            }

            foreach (var rawProvider in rawProviders)
            {
                providerSlotCount++;
                if (rawProvider is not Object provider || provider == null)
                {
                    missingProviderCount++;
                    continue;
                }

                providers.Add(new Dictionary<string, object>
                {
                    { "name", provider.name },
                    { "type", provider.GetType().FullName },
                    { "path", GetAssetPath(provider) }
                });
            }

            return providers;
        }

        internal static Dictionary<string, object> DescribeComponent(Component component)
        {
            return new Dictionary<string, object>
            {
                { "type", component.GetType().FullName },
                { "gameObjectPath", GetGameObjectPath(component.transform) },
                { "enabled", component is Behaviour behaviour ? (object)behaviour.enabled : null }
            };
        }

        internal static Dictionary<string, object> DescribeRuntimeObject(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj is Object unityObject)
            {
                return new Dictionary<string, object>
                {
                    { "type", unityObject.GetType().FullName },
                    { "name", unityObject.name },
                    { "path", unityObject is Component component ? GetGameObjectPath(component.transform) : GetAssetPath(unityObject) },
                    { "instanceID", VmObjectId.Get(unityObject) }
                };
            }

            return new Dictionary<string, object>
            {
                { "type", obj.GetType().FullName },
                { "text", obj.ToString() }
            };
        }

        internal static object DescribeValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string || value.GetType().IsPrimitive || value is decimal)
            {
                return value;
            }

            if (value is Object unityObject)
            {
                return DescribeRuntimeObject(unityObject);
            }

            if (value is IContainer container)
            {
                return DescribeContainer(container);
            }

            if (value is IEnumerable enumerable)
            {
                var items = new List<object>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    count++;
                    if (items.Count < 20)
                    {
                        items.Add(DescribeValue(item));
                    }
                }

                return new Dictionary<string, object>
                {
                    { "type", value.GetType().FullName },
                    { "count", count },
                    { "items", items }
                };
            }

            return new Dictionary<string, object>
            {
                { "type", value.GetType().FullName },
                { "text", value.ToString() }
            };
        }

        internal static Dictionary<string, object> DescribeContainer(IContainer container)
        {
            if (container == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "type", container.GetType().FullName },
                { "id", container.id ?? "" },
                { "capacity", container.Capacity.HasValue ? (object)container.Capacity.Value : null },
                { "count", container.Count },
                { "validCount", container.ValidCount },
                { "isFull", container.IsFull },
                { "validSlotIndices", container.ValidSlotIndices.Take(100).ToArray() },
                { "validItems", container.ValidItems.Take(20).Select(DescribeRuntimeObject).ToList() }
            };
        }

        internal static Dictionary<string, object> DescribeRange(object range)
        {
            if (range == null)
            {
                return null;
            }

            var type = range.GetType();
            return new Dictionary<string, object>
            {
                { "type", type.FullName },
                { "min", ReadFieldOrProperty(range, "min") },
                { "max", ReadFieldOrProperty(range, "max") },
                { "count", ReadFieldOrProperty(range, "Count") }
            };
        }

        internal static object ReadFieldOrProperty(object target, string memberName)
        {
            var type = target.GetType();
            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(target);
            }

            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? null : property.GetValue(target);
        }

        internal static void ApplySerializedValues(object target, Dictionary<string, object> values)
        {
            foreach (var pair in values)
            {
                string memberName = pair.Key;
                object rawValue = pair.Value;
                if (memberName == nameof(GamePrefab.id) || memberName == "_id")
                {
                    throw new InvalidOperationException("Use the root id argument instead of serializedValues.id.");
                }

                if (TrySetProperty(target, memberName, rawValue))
                {
                    continue;
                }

                if (TrySetField(target, memberName, rawValue))
                {
                    continue;
                }

                throw new MissingMemberException(target.GetType().FullName, memberName);
            }
        }

        internal static bool TrySetProperty(object target, string memberName, object rawValue)
        {
            var property = target.GetType().GetProperty(memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property == null)
            {
                return false;
            }

            if (property.CanWrite == false || property.GetIndexParameters().Length != 0)
            {
                throw new InvalidOperationException(
                    $"Property '{memberName}' on '{target.GetType().FullName}' is not writable.");
            }

            property.SetValue(target, ConvertValue(rawValue, property.PropertyType, memberName));
            return true;
        }

        internal static bool TrySetField(object target, string memberName, object rawValue)
        {
            var field = target.GetType().GetField(memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
            {
                return false;
            }

            field.SetValue(target, ConvertValue(rawValue, field.FieldType, memberName));
            return true;
        }

        internal static object ConvertValue(object value, Type targetType, string memberName)
        {
            if (value == null)
            {
                return null;
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                targetType = nullableType;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(long))
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value.ToString(), true);
            }

            if (typeof(Object).IsAssignableFrom(targetType))
            {
                return ConvertUnityObject(value, targetType, memberName);
            }

            if (TryConvertStringCollection(value, targetType, out var stringCollection))
            {
                return stringCollection;
            }

            throw new InvalidOperationException(
                $"Cannot convert value for '{memberName}' to '{targetType.FullName}'.");
        }

        internal static object ConvertUnityObject(object value, Type targetType, string memberName)
        {
            if (value is not string assetPath || string.IsNullOrWhiteSpace(assetPath))
            {
                throw new InvalidOperationException(
                    $"Unity object field '{memberName}' must be set with an asset path string.");
            }

            var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Could not load asset '{assetPath}' as '{targetType.FullName}' for '{memberName}'.");
            }

            return asset;
        }

        internal static bool TryConvertStringCollection(object value, Type targetType, out object collection)
        {
            collection = null;

            if (targetType == typeof(HashSet<string>))
            {
                collection = new HashSet<string>(GetStringValues(value));
                return true;
            }

            if (targetType == typeof(List<string>))
            {
                collection = GetStringValues(value).ToList();
                return true;
            }

            return false;
        }

        internal static IEnumerable<string> GetStringValues(object value)
        {
            if (value is string str)
            {
                yield return str;
                yield break;
            }

            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException("Expected a string or string array.");
            }

            foreach (object item in enumerable)
            {
                if (item != null)
                {
                    yield return item.ToString();
                }
            }
        }

        internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        internal static IEnumerable<T> SafeEnumerable<T>(Func<IEnumerable<T>> getter)
        {
            IEnumerable<T> values;
            try
            {
                values = getter();
            }
            catch
            {
                yield break;
            }

            foreach (var value in values)
            {
                yield return value;
            }
        }

        internal static T SafeGet<T>(Func<T> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return default;
            }
        }

        internal static void RefreshGamePrefabRegistry()
        {
            AssetDatabase.Refresh();
            GamePrefabWrapperInitializeUtility.Refresh();
        }

        internal static bool MatchesFilter(string value, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   (value != null && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static string CombineAssetPath(string folderPath, string assetName)
        {
            folderPath = (folderPath ?? "").Replace("\\", "/").TrimEnd('/');
            assetName = (assetName ?? "").Replace("\\", "/").TrimStart('/');
            return $"{folderPath}/{assetName}";
        }

        internal static string ToPascalAssetName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "New GamePrefab";
            }

            var parts = id.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("", parts.Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        internal static string GetAssetPath(Object obj)
        {
            return obj == null ? "" : AssetDatabase.GetAssetPath(obj);
        }

        internal static string GetGameObjectPath(Transform transform)
        {
            if (transform == null)
            {
                return "";
            }

            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        internal static string GetRequiredString(Dictionary<string, object> args, string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{key} is required.");
            }

            return value;
        }

        internal static string GetString(Dictionary<string, object> args, string key)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return null;
            }

            return value.ToString();
        }

        internal static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return defaultValue;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        internal static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        internal static int GetOffset(Dictionary<string, object> args)
        {
            return Math.Max(0, GetInt(args, "offset", 0));
        }

        internal static Dictionary<string, object> GetDictionary(Dictionary<string, object> args, string key)
        {
            if (args.TryGetValue(key, out object value) == false || value == null)
            {
                return null;
            }

            return value as Dictionary<string, object>;
        }

        internal static VMFrameworkGamePrefabReference CreateGamePrefabReference(
            GamePrefabInfo info)
        {
            if (info == null || info.gamePrefab == null || info.wrapper == null)
                throw new ArgumentException("A complete GamePrefab info record is required.", nameof(info));
            List<GamePrefabGeneralSetting> owners = GetAllGamePrefabGeneralSettings()
                .Where(setting => setting.BaseGamePrefabType.IsAssignableFrom(
                                      info.gamePrefab.GetType()) &&
                                  setting.initialGamePrefabProviders != null &&
                                  setting.initialGamePrefabProviders.Contains(info.wrapper))
                .ToList();
            if (owners.Count == 0)
            {
                throw new InvalidOperationException(
                    $"GamePrefab '{info.gamePrefab.id}' is not registered to an authoritative GamePrefabGeneralSetting.");
            }
            if (owners.Count > 1)
            {
                throw new InvalidOperationException(
                    $"GamePrefab '{info.gamePrefab.id}' is registered to more than one GamePrefabGeneralSetting.");
            }
            GamePrefabGeneralSetting generalSetting = owners[0];
            return CreateGamePrefabReference(info.gamePrefab, info.wrapper, generalSetting);
        }

        internal static VMFrameworkGamePrefabReference CreateGamePrefabReference(
            IGamePrefab gamePrefab, GamePrefabWrapper wrapper,
            GamePrefabGeneralSetting generalSetting)
        {
            if (gamePrefab == null)
                throw new ArgumentNullException(nameof(gamePrefab));
            if (wrapper == null)
                throw new ArgumentNullException(nameof(wrapper));
            if (generalSetting == null)
            {
                throw new InvalidOperationException(
                    $"GamePrefab '{gamePrefab.id}' has no authoritative GamePrefabGeneralSetting.");
            }
            if (!generalSetting.BaseGamePrefabType.IsAssignableFrom(gamePrefab.GetType()) ||
                generalSetting.initialGamePrefabProviders == null ||
                !generalSetting.initialGamePrefabProviders.Contains(wrapper))
            {
                throw new InvalidOperationException(
                    $"GamePrefab '{gamePrefab.id}' is not registered by '{generalSetting.name}'.");
            }

            string wrapperPath = GetAssetPath(wrapper);
            string generalSettingPath = GetAssetPath(generalSetting);
            if (string.IsNullOrWhiteSpace(gamePrefab.id) ||
                string.IsNullOrWhiteSpace(gamePrefab.GetType().FullName) ||
                string.IsNullOrWhiteSpace(wrapperPath) ||
                string.IsNullOrWhiteSpace(generalSettingPath))
            {
                throw new InvalidOperationException(
                    "A GamePrefab reference requires a registered id, CLR type, wrapper asset, and GeneralSetting asset.");
            }

            return new VMFrameworkGamePrefabReference
            {
                Id = gamePrefab.id,
                FullTypeName = gamePrefab.GetType().FullName,
                WrapperPath = wrapperPath,
                GeneralSettingPath = generalSettingPath,
            };
        }

        internal static List<Dictionary<string, object>> GetDictionaryListValue(
            IReadOnlyDictionary<string, object> args, string key)
        {
            if (!args.TryGetValue(key, out object raw))
            {
                return new List<Dictionary<string, object>>();
            }

            return ((IEnumerable)raw).Cast<object>()
                .Cast<Dictionary<string, object>>()
                .ToList();
        }

        internal sealed class GamePrefabInfo
        {
            public GamePrefabWrapper wrapper;
            public string wrapperPath;
            public IGamePrefab gamePrefab;
        }

    }
}
#endif
