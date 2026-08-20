#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using VMUnityAutomation.Editor;
using VMFramework.Core;
using VMFramework.GameLogicArchitecture;
using static VMFramework.Pipeline.Editor.VMFrameworkPipelineTools;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkPipelineGameTagTools
    {
        private const string LIST_GAME_TAGS_TOOL_NAME = "vmframework/list-game-tags";
        private const string UPSERT_GAME_TAG_TOOL_NAME = "vmframework/upsert-game-tag";
        private const string VALIDATE_GAME_TAGS_TOOL_NAME = "vmframework/validate-game-tags";

        private const string LIST_GAME_TAGS_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"Optional exact GameTag id.\"}," +
            "\"filter\":{\"type\":\"string\",\"description\":\"Optional id, group name, group path, or localization key filter.\"}," +
            "\"groupPath\":{\"type\":\"string\",\"description\":\"Optional exact GameTagGroup asset path.\"}," +
            "\"includeLocalizations\":{\"type\":\"boolean\",\"description\":\"Include potentially large localized values for every table locale. Defaults to false and remains request-owned.\"}," +
            "\"offset\":{\"type\":\"integer\",\"minimum\":0,\"description\":\"Result offset. Defaults to 0.\"}," +
            "\"limit\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned tags. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 500.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string UPSERT_GAME_TAG_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"groupPath\":{\"type\":\"string\",\"description\":\"Exact target GameTagGroup asset path.\"}," +
            "\"groupName\":{\"type\":\"string\",\"description\":\"Exact target GameTagGroup asset name when groupPath is omitted.\"}," +
            "\"id\":{\"type\":\"string\",\"description\":\"GameTag id to create or update.\"}," +
            "\"hasName\":{\"type\":\"boolean\",\"description\":\"Whether the tag has a localized name. Inferred when omitted.\"}," +
            "\"hasDescription\":{\"type\":\"boolean\",\"description\":\"Whether the tag has a localized description. Inferred when omitted.\"}," +
            "\"tableName\":{\"type\":\"string\",\"description\":\"String Table Collection. Defaults to GameTagGeneralSetting.defaultLocalizationTableName.\"}," +
            "\"nameKey\":{\"type\":\"string\",\"description\":\"Localized name key. Defaults to <PascalId>TagName.\"}," +
            "\"descriptionKey\":{\"type\":\"string\",\"description\":\"Localized description key. Defaults to <PascalId>TagDescription.\"}," +
            "\"localizations\":{\"type\":\"array\",\"description\":\"Localized values by locale.\",\"items\":{\"type\":\"object\",\"properties\":{" +
            "\"locale\":{\"type\":\"string\",\"description\":\"Locale identifier or unique locale name.\"}," +
            "\"name\":{\"type\":\"string\",\"description\":\"Localized tag name for this locale.\"}," +
            "\"description\":{\"type\":\"string\",\"description\":\"Localized tag description for this locale.\"}" +
            "},\"required\":[\"locale\"],\"additionalProperties\":false}}," +
            "\"registerGroup\":{\"type\":\"boolean\",\"description\":\"Register an unregistered target group in GameTagGeneralSetting. Defaults to true.\"}," +
            "\"dryRun\":{\"type\":\"boolean\",\"description\":\"Validate and return the mutation plan without changing assets. Defaults to false.\"}," +
            "\"includeValidation\":{\"type\":\"boolean\",\"description\":\"Run and include a global GameTag validation after mutation. Defaults to false; call validate-game-tags for normal audits.\"}," +
            "\"maxIssues\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum issues returned when includeValidation is true. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 500.\"}" +
            "},\"required\":[\"id\"],\"additionalProperties\":false}";

        private const string VALIDATE_GAME_TAGS_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"includeMissingTranslations\":{\"type\":\"boolean\",\"description\":\"Report missing or empty locale values. Uses Project Settings > VMFramework Pipeline when omitted; initially true.\",\"x-vmAutomationDefaultSource\":\"Project Settings > VMFramework Pipeline > GameTag Validation\",\"x-vmAutomationExplicitValueWins\":true}," +
            "\"includeGamePrefabReferences\":{\"type\":\"boolean\",\"description\":\"Check GamePrefab gameTags against registered tags. Uses Project Settings > VMFramework Pipeline when omitted; initially true.\",\"x-vmAutomationDefaultSource\":\"Project Settings > VMFramework Pipeline > GameTag Validation\",\"x-vmAutomationExplicitValueWins\":true}," +
            "\"maxIssues\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":5000,\"description\":\"Maximum returned issues. Uses the shared VM Unity Automation result preference when omitted; otherwise defaults to 500.\",\"x-vmAutomationDefaultSource\":\"Preferences > VM Unity Automation > Tool Responses\",\"x-vmAutomationExplicitValueWins\":true}" +
            "},\"additionalProperties\":false}";

        private const string LIST_GAME_TAGS_OUTPUT_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"generalSettingPath\":{\"type\":\"string\"}," +
            "\"tags\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"count\":{\"type\":\"integer\"},\"total\":{\"type\":\"integer\"}," +
            "\"offset\":{\"type\":\"integer\"},\"limit\":{\"type\":\"integer\"}," +
            "\"nextOffset\":{\"type\":[\"integer\",\"null\"]}" +
            "},\"required\":[\"generalSettingPath\",\"tags\",\"count\",\"total\",\"offset\",\"limit\",\"nextOffset\"],\"additionalProperties\":false}";

        private const string UPSERT_GAME_TAG_OUTPUT_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\"},\"groupPath\":{\"type\":\"string\"}," +
            "\"generalSettingPath\":{\"type\":\"string\"},\"created\":{\"type\":\"boolean\"}," +
            "\"updated\":{\"type\":\"boolean\"},\"groupRegistered\":{\"type\":\"boolean\"}," +
            "\"wouldRegisterGroup\":{\"type\":\"boolean\"},\"hasName\":{\"type\":\"boolean\"}," +
            "\"nameReference\":" + VMFrameworkPipelineSchemaJson.Map + ",\"hasDescription\":{\"type\":\"boolean\"}," +
            "\"descriptionReference\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"localizations\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"dryRun\":{\"type\":\"boolean\"},\"localizationResult\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"readback\":" + VMFrameworkPipelineSchemaJson.Map + ",\"validation\":" + VMFrameworkPipelineSchemaJson.Map +
            "},\"required\":[\"id\",\"groupPath\",\"generalSettingPath\",\"created\",\"updated\",\"groupRegistered\",\"wouldRegisterGroup\",\"hasName\",\"nameReference\",\"hasDescription\",\"descriptionReference\",\"localizations\",\"dryRun\"],\"additionalProperties\":false}";

        private const string VALIDATE_GAME_TAGS_OUTPUT_SCHEMA =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"valid\":{\"type\":\"boolean\"},\"generalSettingPath\":{\"type\":\"string\"}," +
            "\"coverage\":{\"type\":\"object\",\"properties\":{" +
            "\"includeMissingTranslations\":{\"type\":\"boolean\"}," +
            "\"includeGamePrefabReferences\":{\"type\":\"boolean\"}" +
            "},\"required\":[\"includeMissingTranslations\",\"includeGamePrefabReferences\"],\"additionalProperties\":false}," +
            "\"tagCount\":{\"type\":\"integer\"},\"errorCount\":{\"type\":\"integer\"}," +
            "\"warningCount\":{\"type\":\"integer\"},\"totalIssues\":{\"type\":\"integer\"}," +
            "\"returnedIssues\":{\"type\":\"integer\"},\"truncated\":{\"type\":\"boolean\"}," +
            "\"issues\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}" +
            "},\"required\":[\"valid\",\"generalSettingPath\",\"coverage\",\"tagCount\",\"errorCount\",\"warningCount\",\"totalIssues\",\"returnedIssues\",\"truncated\",\"issues\"],\"additionalProperties\":false}";

        [VmProjectTool(LIST_GAME_TAGS_TOOL_NAME,
            Description = "List registered VMFramework GameTags with source groups and localized references.",
            InputSchemaJson = LIST_GAME_TAGS_SCHEMA,
            OutputSchemaJson = LIST_GAME_TAGS_OUTPUT_SCHEMA,
            ReadOnly = true)]
        public static object ListGameTags(Dictionary<string, object> args)
        {
            args ??= new();
            var setting = ResolveGameTagGeneralSetting();
            string exactID = GetString(args, "id");
            string filter = GetString(args, "filter");
            string groupPath = GetString(args, "groupPath");
            bool includeLocalizations = GetBool(args, "includeLocalizations", false);
            int offset = GetOffset(args);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                args, "limit", 500, 5000);
            var localizedStringReader = new VMFrameworkLocalizedStringReader();

            var allSources = GetRegisteredGameTagSources(setting)
                .Where(source => string.IsNullOrWhiteSpace(exactID) ||
                                 string.Equals(source.Info?.id, exactID, StringComparison.Ordinal))
                .Where(source => string.IsNullOrWhiteSpace(groupPath) ||
                                 string.Equals(source.GroupPath, groupPath, StringComparison.Ordinal))
                .Where(source => MatchesGameTagFilter(source, filter,
                    localizedStringReader))
                .ToList();
            var sources = allSources
                .Skip(offset)
                .Take(limit)
                .Select(source => DescribeGameTagSource(source, includeLocalizations,
                    localizedStringReader))
                .ToList();

            return new Dictionary<string, object>
            {
                { "generalSettingPath", AssetDatabase.GetAssetPath(setting) },
                { "tags", sources },
                { "count", sources.Count },
                { "total", allSources.Count },
                { "offset", offset },
                { "limit", limit },
                { "nextOffset", offset + sources.Count < allSources.Count ? (object)(offset + sources.Count) : null },
            };
        }

        [VmProjectTool(UPSERT_GAME_TAG_TOOL_NAME,
            Description = "Create or update a VMFramework GameTag in a GameTagGroup, maintain localized strings, register the group, and verify deserialized readback.",
            InputSchemaJson = UPSERT_GAME_TAG_SCHEMA,
            OutputSchemaJson = UPSERT_GAME_TAG_OUTPUT_SCHEMA,
            MutatesAssets = true)]
        public static object UpsertGameTag(Dictionary<string, object> args)
        {
            args ??= new();
            string id = GetRequiredString(args, "id").Trim();
            if (id.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("id cannot contain whitespace.");
            }

            var setting = ResolveGameTagGeneralSetting();
            var group = ResolveGameTagGroup(GetString(args, "groupPath"), GetString(args, "groupName"));
            string groupPath = AssetDatabase.GetAssetPath(group);
            bool groupRegistered = IsGameTagGroupRegistered(setting, group);
            bool registerGroup = GetBool(args, "registerGroup", true);
            bool dryRun = GetBool(args, "dryRun", false);
            var localizationPlans = ParseGameTagLocalizationPlans(args);

            var targetMatches = group.gameTagInfos
                .Where(info => info != null && string.Equals(info.id, id, StringComparison.Ordinal))
                .ToList();
            if (targetMatches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"GameTag id '{id}' appears {targetMatches.Count} times in target group '{groupPath}'.");
            }

            GameTagInfo existing = targetMatches.SingleOrDefault();
            var localizedStringReader = new VMFrameworkLocalizedStringReader();
            var duplicateSources = GetRegisteredGameTagSources(setting)
                .Where(source => source.Info != null &&
                                 string.Equals(source.Info.id, id, StringComparison.Ordinal) &&
                                 ReferenceEquals(source.Info, existing) == false)
                .ToList();
            if (duplicateSources.Count > 0)
            {
                throw new InvalidOperationException(
                    $"GameTag id '{id}' already exists outside the target entry: " +
                    string.Join(", ", duplicateSources.Select(source => source.SourceLabel)));
            }

            bool hasName = args.ContainsKey("hasName")
                ? GetBool(args, "hasName", false)
                : existing?.hasName == true || localizationPlans.Any(plan => plan.HasName);
            bool hasDescription = args.ContainsKey("hasDescription")
                ? GetBool(args, "hasDescription", false)
                : existing?.hasDescription == true || localizationPlans.Any(plan => plan.HasDescription);
            if (hasName == false && localizationPlans.Any(plan => plan.HasName))
            {
                throw new ArgumentException("localizations contains name values while hasName is false.");
            }
            if (hasDescription == false && localizationPlans.Any(plan => plan.HasDescription))
            {
                throw new ArgumentException("localizations contains description values while hasDescription is false.");
            }

            string tableName = GetString(args, "tableName");
            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = setting.defaultLocalizationTableName;
            }
            if ((hasName || hasDescription) && string.IsNullOrWhiteSpace(tableName))
            {
                throw new InvalidOperationException("No GameTag localization table name is configured.");
            }

            string nameKey = hasName
                ? FirstNotEmpty(GetString(args, "nameKey"),
                    localizedStringReader.GetKey(existing?.name),
                    BuildDefaultGameTagKey(id, "TagName"))
                : "";
            string descriptionKey = hasDescription
                ? FirstNotEmpty(GetString(args, "descriptionKey"),
                    localizedStringReader.GetKey(existing?.description),
                    BuildDefaultGameTagKey(id, "TagDescription"))
                : "";

            var localizationPreflight = PreflightGameTagLocalizations(tableName, localizationPlans);
            var planResult = new Dictionary<string, object>
            {
                { "id", id },
                { "groupPath", groupPath },
                { "generalSettingPath", AssetDatabase.GetAssetPath(setting) },
                { "created", existing == null },
                { "updated", existing != null },
                { "groupRegistered", groupRegistered },
                { "wouldRegisterGroup", groupRegistered == false && registerGroup },
                { "hasName", hasName },
                { "nameReference", DescribeGameTagLocalizationReference(tableName, nameKey) },
                { "hasDescription", hasDescription },
                { "descriptionReference", DescribeGameTagLocalizationReference(tableName, descriptionKey) },
                { "localizations", localizationPreflight }
            };
            if (dryRun)
            {
                planResult["dryRun"] = true;
                return planResult;
            }

            if (groupRegistered == false)
            {
                if (registerGroup == false)
                {
                    throw new InvalidOperationException(
                        $"Target group '{groupPath}' is not registered in GameTagGeneralSetting.");
                }
                EnsureGameTagGroupRegistered(setting, group);
            }

            Undo.RecordObject(group, $"Upsert GameTag {id}");
            GameTagInfo tag = UpsertGameTagInfo(group, id, hasName, hasDescription, tableName, nameKey,
                descriptionKey);
            EditorUtility.SetDirty(group);

            var localizationResult = ApplyGameTagLocalizations(tableName, nameKey, descriptionKey,
                localizationPlans);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(groupPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            setting = ResolveGameTagGeneralSetting();
            setting.InitGameTags();
            var reloadedGroup = AssetDatabase.LoadAssetAtPath<GameTagGroup>(groupPath);
            var readback = reloadedGroup?.gameTagInfos
                .Where(info => info != null && string.Equals(info.id, id, StringComparison.Ordinal))
                .ToList() ?? new List<GameTagInfo>();
            if (readback.Count != 1)
            {
                throw new InvalidOperationException(
                    $"GameTag '{id}' was saved but deserialized readback found {readback.Count} entries.");
            }
            if (GameTag.TryGetTag(id, out GameTagInfo runtimeTag) == false || runtimeTag == null)
            {
                throw new InvalidOperationException(
                    $"GameTag '{id}' was saved but was not registered in the refreshed GameTag registry.");
            }

            planResult["dryRun"] = false;
            planResult["groupRegistered"] = true;
            planResult["localizationResult"] = localizationResult;
            planResult["readback"] = DescribeGameTagSource(new GameTagSource
            {
                Info = readback[0],
                Group = reloadedGroup,
                GroupIndex = reloadedGroup.gameTagInfos.IndexOf(readback[0])
            }, false, localizedStringReader);
            if (GetBool(args, "includeValidation", false))
            {
                planResult["validation"] = BuildGameTagValidation(
                    setting,
                    VMFrameworkPipelineSettingsManager.IncludeMissingGameTagTranslations,
                    VMFrameworkPipelineSettingsManager.IncludeGamePrefabTagReferences,
                    VMFrameworkPipelineSettingsManager.ResolveResultLimit(args, "maxIssues", 500, 5000));
            }
            return planResult;
        }

        [VmProjectTool(VALIDATE_GAME_TAGS_TOOL_NAME,
            Description = "Validate registered VMFramework GameTags for empty or duplicate ids, invalid localized references, missing translations, and undefined GamePrefab tag references.",
            InputSchemaJson = VALIDATE_GAME_TAGS_SCHEMA,
            OutputSchemaJson = VALIDATE_GAME_TAGS_OUTPUT_SCHEMA,
            ReadOnly = true)]
        public static object ValidateGameTags(Dictionary<string, object> args)
        {
            args ??= new();
            return BuildGameTagValidation(ResolveGameTagGeneralSetting(),
                VMFrameworkPipelineSettingsManager.ResolveProjectBool(
                    args, "includeMissingTranslations",
                    VMFrameworkPipelineSettingsManager.IncludeMissingGameTagTranslations),
                VMFrameworkPipelineSettingsManager.ResolveProjectBool(
                    args, "includeGamePrefabReferences",
                    VMFrameworkPipelineSettingsManager.IncludeGamePrefabTagReferences),
                VMFrameworkPipelineSettingsManager.ResolveResultLimit(args, "maxIssues", 500, 5000));
        }

        private static GameTagGeneralSetting ResolveGameTagGeneralSetting()
        {
            try
            {
                if (CoreSetting.GameTagGeneralSetting != null)
                {
                    return CoreSetting.GameTagGeneralSetting;
                }
            }
            catch
            {
                // Fall back to asset discovery when framework initialization has not run yet.
            }

            var settings = AssetDatabase.FindAssets("t:GameTagGeneralSetting")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameTagGeneralSetting>)
                .Where(setting => setting != null)
                .Distinct()
                .ToList();
            if (settings.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one GameTagGeneralSetting asset but found {settings.Count}.");
            }
            return settings[0];
        }

        private static GameTagGroup ResolveGameTagGroup(string groupPath, string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupPath) == false)
            {
                var exact = AssetDatabase.LoadAssetAtPath<GameTagGroup>(groupPath);
                if (exact == null)
                {
                    throw new ArgumentException($"GameTagGroup was not found at '{groupPath}'.");
                }
                return exact;
            }
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("groupPath or groupName is required.");
            }

            var matches = AssetDatabase.FindAssets("t:GameTagGroup")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameTagGroup>)
                .Where(group => group != null && string.Equals(group.name, groupName, StringComparison.Ordinal))
                .ToList();
            if (matches.Count != 1)
            {
                throw new ArgumentException(
                    $"Expected one GameTagGroup named '{groupName}' but found {matches.Count}.");
            }
            return matches[0];
        }

        private static List<GameTagGroupBase> GetRegisteredGameTagGroups(GameTagGeneralSetting setting)
        {
            FieldInfo field = typeof(GameTagGeneralSetting).GetField("gameTagGroups",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(setting) is IEnumerable values
                ? values.Cast<object>().OfType<GameTagGroupBase>().ToList()
                : new List<GameTagGroupBase>();
        }

        private static List<GameTagInfo> GetInlineGameTags(GameTagGeneralSetting setting)
        {
            FieldInfo field = typeof(GameTagGeneralSetting).GetField("gameTagInfos",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(setting) is IEnumerable values
                ? values.Cast<object>().OfType<GameTagInfo>().ToList()
                : new List<GameTagInfo>();
        }

        private static List<GameTagSource> GetRegisteredGameTagSources(GameTagGeneralSetting setting)
        {
            var sources = new List<GameTagSource>();
            foreach (var group in GetRegisteredGameTagGroups(setting))
            {
                int index = 0;
                foreach (var info in group.GetGameTagInfos() ?? Enumerable.Empty<GameTagInfo>())
                {
                    sources.Add(new GameTagSource { Info = info, Group = group, GroupIndex = index++ });
                }
            }
            var inline = GetInlineGameTags(setting);
            for (int i = 0; i < inline.Count; i++)
            {
                sources.Add(new GameTagSource { Info = inline[i], Inline = true, GroupIndex = i });
            }
            return sources;
        }

        private static bool IsGameTagGroupRegistered(GameTagGeneralSetting setting, GameTagGroupBase group)
        {
            return GetRegisteredGameTagGroups(setting).Contains(group);
        }

        private static void EnsureGameTagGroupRegistered(GameTagGeneralSetting setting, GameTagGroupBase group)
        {
            if (IsGameTagGroupRegistered(setting, group))
            {
                return;
            }
            Undo.RecordObject(setting, "Register GameTagGroup");
            var serializedSetting = new SerializedObject(setting);
            SerializedProperty groups = serializedSetting.FindProperty("gameTagGroups");
            if (groups == null)
            {
                throw new InvalidOperationException("GameTagGeneralSetting.gameTagGroups was not found.");
            }
            int index = groups.arraySize;
            groups.InsertArrayElementAtIndex(index);
            groups.GetArrayElementAtIndex(index).objectReferenceValue = group;
            serializedSetting.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
        }

        private static GameTagInfo UpsertGameTagInfo(GameTagGroup group, string id, bool hasName,
            bool hasDescription, string tableName, string nameKey, string descriptionKey)
        {
            GameTagInfo info = group.gameTagInfos.FirstOrDefault(tag => tag != null &&
                string.Equals(tag.id, id, StringComparison.Ordinal));
            if (info == null)
            {
                info = new GameTagInfo();
                group.gameTagInfos.Add(info);
            }
            info.id = id;
            info.hasName = hasName;
            info.name ??= new LocalizedString();
            if (hasName)
            {
                info.name.SetReference(tableName, nameKey);
            }
            else
            {
                info.name = new LocalizedString();
            }
            info.hasDescription = hasDescription;
            info.description ??= new LocalizedString();
            if (hasDescription)
            {
                info.description.SetReference(tableName, descriptionKey);
            }
            else
            {
                info.description = new LocalizedString();
            }
            return info;
        }

        private static List<GameTagLocalizationPlan> ParseGameTagLocalizationPlans(
            IReadOnlyDictionary<string, object> args)
        {
            var plans = new List<GameTagLocalizationPlan>();
            var seenLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, object> raw in GetDictionaryListValue(args, "localizations"))
            {
                string localeCode = GetRequiredString(raw, "locale");
                if (seenLocales.Add(localeCode) == false)
                {
                    throw new ArgumentException($"Duplicate localization locale '{localeCode}'.");
                }
                bool hasName = raw.ContainsKey("name");
                bool hasDescription = raw.ContainsKey("description");
                if (hasName == false && hasDescription == false)
                {
                    throw new ArgumentException(
                        $"Localization '{localeCode}' must contain name or description.");
                }
                plans.Add(new GameTagLocalizationPlan
                {
                    Locale = ResolveGameTagLocale(localeCode),
                    HasName = hasName,
                    Name = GetString(raw, "name") ?? "",
                    HasDescription = hasDescription,
                    Description = GetString(raw, "description") ?? ""
                });
            }
            return plans;
        }

        private static Locale ResolveGameTagLocale(string code)
        {
            var matches = LocalizationEditorSettings.GetLocales()
                .Where(locale => locale != null &&
                    (string.Equals(locale.Identifier.Code, code, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(locale.name, code, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (matches.Count != 1)
            {
                throw new ArgumentException($"Expected one Locale '{code}' but found {matches.Count}.");
            }
            return matches[0];
        }

        private static List<Dictionary<string, object>> PreflightGameTagLocalizations(string tableName,
            IEnumerable<GameTagLocalizationPlan> plans)
        {
            var collection = string.IsNullOrWhiteSpace(tableName)
                ? null
                : LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null && plans.Any())
            {
                throw new InvalidOperationException(
                    $"String Table Collection '{tableName}' was not found.");
            }
            return plans.Select(plan => new Dictionary<string, object>
            {
                { "locale", plan.Locale.Identifier.Code },
                { "name", plan.HasName ? plan.Name : null },
                { "description", plan.HasDescription ? plan.Description : null },
                { "tableExists", collection?.GetTable(plan.Locale.Identifier) != null }
            }).ToList();
        }

        private static Dictionary<string, object> ApplyGameTagLocalizations(string tableName, string nameKey,
            string descriptionKey, IEnumerable<GameTagLocalizationPlan> plans)
        {
            var planList = plans.ToList();
            if (planList.Count == 0)
            {
                return new Dictionary<string, object>
                {
                    { "collection", tableName ?? "" }, { "createdTables", 0 },
                    { "createdEntries", 0 }, { "updatedEntries", 0 }
                };
            }
            var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
            {
                throw new InvalidOperationException(
                    $"String Table Collection '{tableName}' was not found.");
            }

            int createdTables = 0;
            int createdEntries = 0;
            int updatedEntries = 0;
            var results = new List<Dictionary<string, object>>();
            foreach (var plan in planList)
            {
                var table = collection.GetTable(plan.Locale.Identifier) as StringTable;
                if (table == null)
                {
                    table = collection.AddNewTable(plan.Locale.Identifier) as StringTable;
                    createdTables++;
                }
                if (table == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create StringTable for locale '{plan.Locale.Identifier.Code}'.");
                }

                if (plan.HasName)
                {
                    UpsertGameTagStringEntry(table, nameKey, plan.Name, ref createdEntries,
                        ref updatedEntries);
                }
                if (plan.HasDescription)
                {
                    UpsertGameTagStringEntry(table, descriptionKey, plan.Description, ref createdEntries,
                        ref updatedEntries);
                }
                EditorUtility.SetDirty(table);
                results.Add(new Dictionary<string, object>
                {
                    { "locale", plan.Locale.Identifier.Code },
                    { "nameUpdated", plan.HasName },
                    { "descriptionUpdated", plan.HasDescription }
                });
            }
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            return new Dictionary<string, object>
            {
                { "collection", collection.TableCollectionName },
                { "createdTables", createdTables },
                { "createdEntries", createdEntries },
                { "updatedEntries", updatedEntries },
                { "locales", results }
            };
        }

        private static void UpsertGameTagStringEntry(StringTable table, string key, string value,
            ref int createdEntries, ref int updatedEntries)
        {
            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
            {
                table.AddEntry(key, value);
                createdEntries++;
            }
            else
            {
                entry.Value = value;
                updatedEntries++;
            }
        }

        private static Dictionary<string, object> BuildGameTagValidation(GameTagGeneralSetting setting,
            bool includeMissingTranslations, bool includeGamePrefabReferences, int maxIssues)
        {
            var sources = GetRegisteredGameTagSources(setting);
            var localizedStringReader = new VMFrameworkLocalizedStringReader();
            var issues = new List<Dictionary<string, object>>();
            int totalIssues = 0;
            int errorCount = 0;
            int warningCount = 0;

            foreach (GameTagSource source in sources.Where(source =>
                         source.Info == null || string.IsNullOrWhiteSpace(source.Info.id)))
            {
                AddGameTagIssue(issues, maxIssues, ref totalIssues, ref errorCount, ref warningCount,
                    "empty-id", "error", source.SourceLabel, "GameTag id is empty.");
            }

            foreach (var duplicate in sources.Where(source => source.Info != null &&
                                                               string.IsNullOrWhiteSpace(source.Info.id) == false)
                         .GroupBy(source => source.Info.id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                AddGameTagIssue(issues, maxIssues, ref totalIssues, ref errorCount, ref warningCount,
                    "duplicate-id", "error", duplicate.Key,
                    "GameTag appears in: " + string.Join(", ", duplicate.Select(source => source.SourceLabel)));
            }

            foreach (GameTagSource source in sources.Where(source => source.Info != null))
            {
                if (source.Info.hasName)
                {
                    ValidateGameTagLocalizedReference(source, source.Info.name, "name",
                        includeMissingTranslations, issues, maxIssues, ref totalIssues, ref errorCount,
                        ref warningCount, localizedStringReader);
                }
                if (source.Info.hasDescription)
                {
                    ValidateGameTagLocalizedReference(source, source.Info.description, "description",
                        includeMissingTranslations, issues, maxIssues, ref totalIssues, ref errorCount,
                        ref warningCount, localizedStringReader);
                }
            }

            if (includeGamePrefabReferences)
            {
                var defined = sources.Where(source => source.Info != null &&
                                                      string.IsNullOrWhiteSpace(source.Info.id) == false)
                    .Select(source => source.Info.id)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (GamePrefabWrapper wrapper in GetAllGamePrefabWrappers())
                {
                    string wrapperPath = AssetDatabase.GetAssetPath(wrapper);
                    foreach (IGamePrefab gamePrefab in GetGamePrefabs(wrapper))
                    {
                        foreach (string referencedTag in GetGamePrefabTagIDs(gamePrefab))
                        {
                            if (string.IsNullOrWhiteSpace(referencedTag) || defined.Contains(referencedTag))
                            {
                                continue;
                            }
                            AddGameTagIssue(issues, maxIssues, ref totalIssues, ref errorCount,
                                ref warningCount, "undefined-game-prefab-reference", "error",
                                gamePrefab?.id ?? wrapperPath,
                                $"GamePrefab references undefined GameTag '{referencedTag}' in '{wrapperPath}'.");
                        }
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "valid", errorCount == 0 },
                { "generalSettingPath", AssetDatabase.GetAssetPath(setting) },
                {
                    "coverage",
                    new Dictionary<string, object>
                    {
                        { "includeMissingTranslations", includeMissingTranslations },
                        { "includeGamePrefabReferences", includeGamePrefabReferences },
                    }
                },
                { "tagCount", sources.Count },
                { "errorCount", errorCount },
                { "warningCount", warningCount },
                { "totalIssues", totalIssues },
                { "returnedIssues", issues.Count },
                { "truncated", totalIssues > issues.Count },
                { "issues", issues }
            };
        }

        private static void ValidateGameTagLocalizedReference(GameTagSource source, LocalizedString reference,
            string field, bool includeMissingTranslations, List<Dictionary<string, object>> issues,
            int maxIssues, ref int totalIssues, ref int errorCount, ref int warningCount,
            VMFrameworkLocalizedStringReader localizedStringReader)
        {
            string tableName = localizedStringReader.GetTableName(reference);
            string key = localizedStringReader.GetKey(reference);
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(key))
            {
                AddGameTagIssue(issues, maxIssues, ref totalIssues, ref errorCount, ref warningCount,
                    "invalid-localized-reference", "error", source.Info.id,
                    $"{field} has an empty table or key reference ({source.SourceLabel}).");
                return;
            }
            var collection = localizedStringReader.GetCollection(tableName);
            if (collection == null || collection.SharedData.GetEntry(key) == null)
            {
                AddGameTagIssue(issues, maxIssues, ref totalIssues, ref errorCount, ref warningCount,
                    "missing-localization-key", "error", source.Info.id,
                    $"{field} references missing key '{tableName}/{key}'.");
                return;
            }
            if (includeMissingTranslations == false)
            {
                return;
            }
            foreach (StringTable table in collection.StringTables)
            {
                StringTableEntry entry = table.GetEntry(key);
                if (entry != null && string.IsNullOrWhiteSpace(entry.Value) == false)
                {
                    continue;
                }
                AddGameTagIssue(issues, maxIssues, ref totalIssues, ref errorCount, ref warningCount,
                    "missing-translation", "warning", source.Info.id,
                    $"{field} key '{key}' is empty for locale '{table.LocaleIdentifier.Code}'.");
            }
        }

        private static IEnumerable<string> GetGamePrefabTagIDs(IGamePrefab gamePrefab)
        {
            if (gamePrefab == null)
            {
                yield break;
            }
            FieldInfo field = null;
            for (Type type = gamePrefab.GetType(); type != null && field == null; type = type.BaseType)
            {
                field = type.GetField("gameTags", BindingFlags.Instance | BindingFlags.Public |
                                                 BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            if (field?.GetValue(gamePrefab) is not IEnumerable values)
            {
                yield break;
            }
            foreach (object value in values)
            {
                if (value is string tagID)
                {
                    yield return tagID;
                }
            }
        }

        private static void AddGameTagIssue(List<Dictionary<string, object>> issues, int maxIssues,
            ref int totalIssues, ref int errorCount, ref int warningCount, string code, string severity,
            string target, string message)
        {
            totalIssues++;
            if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                errorCount++;
            }
            else
            {
                warningCount++;
            }
            if (issues.Count < maxIssues)
            {
                issues.Add(new Dictionary<string, object>
                {
                    { "code", code }, { "severity", severity }, { "target", target },
                    { "message", message }
                });
            }
        }

        private static bool MatchesGameTagFilter(GameTagSource source, string filter,
            VMFrameworkLocalizedStringReader localizedStringReader)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }
            return MatchesFilter(source.Info?.id, filter) || MatchesFilter(source.GroupName, filter) ||
                   MatchesFilter(source.GroupPath, filter) ||
                   MatchesFilter(localizedStringReader.GetKey(source.Info?.name), filter) ||
                   MatchesFilter(localizedStringReader.GetKey(source.Info?.description), filter);
        }

        private static Dictionary<string, object> DescribeGameTagSource(GameTagSource source,
            bool includeLocalizations,
            VMFrameworkLocalizedStringReader localizedStringReader)
        {
            var result = new Dictionary<string, object>
            {
                { "id", source.Info?.id ?? "" },
                { "source", source.Inline ? "inline" : "group" },
                { "groupName", source.GroupName },
                { "groupPath", source.GroupPath },
                { "index", source.GroupIndex },
                { "hasName", source.Info?.hasName == true },
                {
                    "name", VmJsonContract.ToTransportValue(localizedStringReader.Read(
                        source.Info?.name, includeLocalizations, null, int.MaxValue))
                },
                { "hasDescription", source.Info?.hasDescription == true },
                {
                    "description", VmJsonContract.ToTransportValue(localizedStringReader.Read(
                        source.Info?.description, includeLocalizations, null, int.MaxValue))
                }
            };
            return result;
        }

        private static Dictionary<string, object> DescribeGameTagLocalizationReference(string tableName,
            string key)
        {
            return new Dictionary<string, object>
            {
                { "table", tableName ?? "" }, { "key", key ?? "" }
            };
        }


        private static string BuildDefaultGameTagKey(string id, string suffix)
        {
            var segments = id.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(segments.Select(segment => segment.Length == 0
                ? ""
                : char.ToUpperInvariant(segment[0]) + segment.Substring(1))) + suffix;
        }

        private static string FirstNotEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => string.IsNullOrWhiteSpace(value) == false) ?? "";
        }

        private sealed class GameTagSource
        {
            public GameTagInfo Info;
            public GameTagGroupBase Group;
            public bool Inline;
            public int GroupIndex;
            public string GroupPath => Group == null ? "" : AssetDatabase.GetAssetPath(Group);
            public string GroupName => Group == null ? "" : Group.name;
            public string SourceLabel => Inline ? $"inline[{GroupIndex}]" : $"{GroupPath}[{GroupIndex}]";
        }

        private sealed class GameTagLocalizationPlan
        {
            public Locale Locale;
            public bool HasName;
            public string Name;
            public bool HasDescription;
            public string Description;
        }
    }
}
#endif
