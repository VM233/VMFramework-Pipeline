#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    internal static class VMFrameworkPipelineSettingsProvider
    {
        private const string UserPreferencesPath = "Preferences/VMFramework Pipeline";
        private const string ProjectSettingsPath = "Project/VMFramework Pipeline";

        private static string projectWriteError = "";

        [SettingsProvider]
        public static SettingsProvider CreateUserPreferencesProvider()
        {
            return new SettingsProvider(UserPreferencesPath, SettingsScope.User)
            {
                label = "VMFramework Pipeline",
                guiHandler = _ => DrawUserPreferences(),
                keywords = new HashSet<string>
                {
                    "VMFramework", "CLI", "Pipeline", "GamePrefab", "inspection", "depth",
                    "collection", "snapshot", "property trace", "result limit",
                },
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectSettingsProvider()
        {
            return new SettingsProvider(ProjectSettingsPath, SettingsScope.Project)
            {
                label = "VMFramework Pipeline",
                guiHandler = _ => DrawProjectSettings(),
                keywords = new HashSet<string>
                {
                    "VMFramework", "CLI", "Pipeline", "GameTag", "validation", "translation",
                    "GamePrefab", "references", "ProjectSettings",
                },
            };
        }

        private static void DrawUserPreferences()
        {
            EditorGUILayout.LabelField("GamePrefab Inspection", EditorStyles.boldLabel);

            int depth = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Default Max Depth",
                    "Default nested GamePrefab inspection depth when maxDepth is omitted. Explicit tool arguments win."),
                VMFrameworkPipelineSettingsManager.GamePrefabInspectionMaxDepth, 1, 16);
            if (depth != VMFrameworkPipelineSettingsManager.GamePrefabInspectionMaxDepth)
                VMFrameworkPipelineSettingsManager.GamePrefabInspectionMaxDepth = depth;

            int items = EditorGUILayout.IntField(
                new GUIContent(
                    "Collection Item Limit",
                    "Default items retained per inspected collection when maxCollectionItems is omitted. Explicit tool arguments win."),
                VMFrameworkPipelineSettingsManager.GamePrefabCollectionItemLimit);
            items = Mathf.Clamp(items, 1, 1000);
            if (items != VMFrameworkPipelineSettingsManager.GamePrefabCollectionItemLimit)
                VMFrameworkPipelineSettingsManager.GamePrefabCollectionItemLimit = items;

            bool snapshots = EditorGUILayout.Toggle(
                new GUIContent(
                    "Include Update Snapshots",
                    "Include complete before and after GamePrefab snapshots when includeSnapshots is omitted. Disabled by default; operation summaries and the semantic diff are still returned."),
                VMFrameworkPipelineSettingsManager.IncludeGamePrefabUpdateSnapshots);
            if (snapshots != VMFrameworkPipelineSettingsManager.IncludeGamePrefabUpdateSnapshots)
                VMFrameworkPipelineSettingsManager.IncludeGamePrefabUpdateSnapshots = snapshots;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Property Trace", EditorStyles.boldLabel);

            int traceEvents = EditorGUILayout.IntField(
                new GUIContent(
                    "Retained Event Limit",
                    "Default trace ring-buffer capacity when maxEvents is omitted. Explicit tool arguments win."),
                VMFrameworkPipelineSettingsManager.PropertyTraceMaxEvents);
            traceEvents = Mathf.Clamp(traceEvents, 1, 10000);
            if (traceEvents != VMFrameworkPipelineSettingsManager.PropertyTraceMaxEvents)
                VMFrameworkPipelineSettingsManager.PropertyTraceMaxEvents = traceEvents;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Shared Result Budget", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                VmAutomationSettings.OverrideDefaultResultLimit
                    ? $"VM Unity Automation currently overrides single-collection defaults with {VmAutomationSettings.DefaultResultLimit} results."
                    : "Single-collection tools use their package defaults. Enable the shared override in VM Unity Automation preferences to use one personal result budget.",
                MessageType.None);
            if (GUILayout.Button("Open VM Unity Automation Tool Response Preferences"))
                SettingsService.OpenUserPreferences("Preferences/VM Unity Automation");

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset User Preferences to Defaults") &&
                EditorUtility.DisplayDialog(
                    "Reset User Preferences",
                    "Reset VMFramework Pipeline user preferences to defaults?",
                    "Reset",
                    "Cancel"))
            {
                VMFrameworkPipelineSettingsManager.ResetUserPreferencesToDefaults();
            }
        }

        private static void DrawProjectSettings()
        {
            var configuration = VMFrameworkPipelineSettingsManager.GetProjectConfiguration();
            if (!configuration.Valid)
            {
                EditorGUILayout.HelpBox(
                    $"{VMFrameworkPipelineProjectConfiguration.ConfigPath}: {configuration.Error}",
                    MessageType.Error);
                if (GUILayout.Button("Replace Invalid Project Settings with Defaults") &&
                    EditorUtility.DisplayDialog(
                        "Replace Invalid Project Settings",
                        $"Replace {VMFrameworkPipelineProjectConfiguration.ConfigPath} with VMFramework Pipeline defaults?",
                        "Replace",
                        "Cancel"))
                {
                    TryWrite(VMFrameworkPipelineSettingsManager.ResetProjectSettingsToDefaults);
                }
                return;
            }

            EditorGUILayout.HelpBox(
                configuration.Found
                    ? $"Team settings are stored in {VMFrameworkPipelineProjectConfiguration.ConfigPath}."
                    : $"Changing a team setting will create {VMFrameworkPipelineProjectConfiguration.ConfigPath}.",
                MessageType.None);

            EditorGUILayout.LabelField("GameTag Validation", EditorStyles.boldLabel);
            bool missingTranslations = EditorGUILayout.Toggle(
                new GUIContent(
                    "Missing Translations",
                    "Default validation coverage for missing or empty locale values. Explicit tool arguments win."),
                VMFrameworkPipelineSettingsManager.IncludeMissingGameTagTranslations);
            if (missingTranslations != VMFrameworkPipelineSettingsManager.IncludeMissingGameTagTranslations)
            {
                TryWrite(() =>
                    VMFrameworkPipelineSettingsManager.IncludeMissingGameTagTranslations = missingTranslations);
            }

            bool prefabReferences = EditorGUILayout.Toggle(
                new GUIContent(
                    "GamePrefab References",
                    "Default validation coverage for GamePrefab tags that are not registered. Explicit tool arguments win."),
                VMFrameworkPipelineSettingsManager.IncludeGamePrefabTagReferences);
            if (prefabReferences != VMFrameworkPipelineSettingsManager.IncludeGamePrefabTagReferences)
            {
                TryWrite(() =>
                    VMFrameworkPipelineSettingsManager.IncludeGamePrefabTagReferences = prefabReferences);
            }

            if (!string.IsNullOrEmpty(projectWriteError))
                EditorGUILayout.HelpBox(projectWriteError, MessageType.Error);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset Project Settings to Defaults") &&
                EditorUtility.DisplayDialog(
                    "Reset Project Settings",
                    "Reset VMFramework Pipeline project settings to defaults?",
                    "Reset",
                    "Cancel"))
            {
                TryWrite(VMFrameworkPipelineSettingsManager.ResetProjectSettingsToDefaults);
            }
        }

        private static void TryWrite(Action write)
        {
            try
            {
                write();
                projectWriteError = "";
            }
            catch (Exception ex)
            {
                projectWriteError = ex.Message;
            }
        }
    }
}
#endif
