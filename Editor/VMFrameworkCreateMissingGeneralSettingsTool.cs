#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VMFramework.GameLogicArchitecture;
using VMFramework.GameLogicArchitecture.Editor;
using VMUnityAutomation.Editor;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkCreateMissingGeneralSettingsTool
    {
        private const int MaximumGlobalSettings = 64;
        private const int MaximumBindings = 256;

        [VmProjectTool("vmframework/create-missing-general-settings",
            Description = "Create and bind missing GeneralSettings through the framework authoring owner, using its configured asset folder. Preserve existing references and assets, save all changes, and verify every binding after synchronous import. Run a normal Editor initialization or domain reload afterward to initialize the newly configured modules.",
            MutatesAssets = true,
            Preconditions = new[] { "stable-edit-mode", "global-setting-files-exist" },
            CompletionEvidence = "Every global GeneralSetting field has an asset reference after save and synchronous import; pre-existing bindings retain their exact asset identity.",
            ErrorCodes = new[]
            {
                "general_settings_editor_not_idle", "general_settings_invalid_folder",
                "general_settings_missing_global_files", "general_settings_capacity_exceeded",
                "general_settings_readback_failed",
            })]
        public static VMFrameworkCreateMissingGeneralSettingsResult CreateMissingGeneralSettings(
            VMFrameworkCreateMissingGeneralSettingsRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw Failure("general_settings_editor_not_idle", "Stable Edit Mode is required.");
            }

            string folder = EditorSetting.GeneralSettingsAssetFolderPath;
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                !AssetDatabase.IsValidFolder(folder))
            {
                throw Failure("general_settings_invalid_folder", $"Invalid configured folder: '{folder}'.");
            }

            var files = GlobalSettingFileEditorManager.GetGlobalSettings().Cast<GlobalSettingFile>()
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal).ToList();
            if (files.Count == 0)
            {
                throw Failure("general_settings_missing_global_files", "No global setting files were discovered.");
            }

            int fieldCount = files.Sum(file => file.GetAllGeneralSettingsFields().Count());
            if (files.Count > MaximumGlobalSettings || fieldCount > MaximumBindings)
            {
                throw Failure("general_settings_capacity_exceeded",
                    $"Discovered {files.Count} global files and {fieldCount} fields; limits are " +
                    $"{MaximumGlobalSettings} files and {MaximumBindings} fields.");
            }

            var previousBindings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GlobalSettingFile file in files)
            {
                string path = AssetDatabase.GetAssetPath(file);
                foreach (var field in file.GetAllGeneralSettingsFields())
                {
                    if (field.GetValue(file) is Object asset && asset != null)
                    {
                        previousBindings.Add(path + ":" + field.Name,
                            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)));
                    }
                }
            }

            var previousGuids = new HashSet<string>(AssetDatabase.FindAssets("t:GeneralSetting",
                new[] { "Assets" }), StringComparer.Ordinal);
            foreach (GlobalSettingFile file in files)
            {
                file.AutoFindAndCreateSettings();
            }
            AssetDatabase.SaveAssets();

            var bindings = new List<VMFrameworkGeneralSettingBinding>(fieldCount);
            var settingPaths = new HashSet<string>(StringComparer.Ordinal);
            var createdPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (GlobalSettingFile file in files)
            {
                string path = AssetDatabase.GetAssetPath(file);
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<GlobalSettingFile>(path);
                if (reloaded == null)
                {
                    throw Failure("general_settings_readback_failed", $"Cannot reload '{path}'.");
                }

                foreach (var field in reloaded.GetAllGeneralSettingsFields())
                {
                    var asset = field.GetValue(reloaded) as Object;
                    if (asset == null)
                    {
                        throw Failure("general_settings_readback_failed",
                            $"Missing asset reference for '{path}:{field.Name}'.");
                    }
                    string settingPath = AssetDatabase.GetAssetPath(asset);
                    string guid = AssetDatabase.AssetPathToGUID(settingPath);
                    string key = path + ":" + field.Name;
                    if (string.IsNullOrEmpty(guid) ||
                        (previousBindings.TryGetValue(key, out string previousGuid) && previousGuid != guid))
                    {
                        throw Failure("general_settings_readback_failed",
                            $"Missing or changed asset identity for '{key}'.");
                    }

                    settingPaths.Add(settingPath);
                    if (!previousGuids.Contains(guid))
                    {
                        createdPaths.Add(settingPath);
                    }
                    bindings.Add(new VMFrameworkGeneralSettingBinding
                    {
                        GlobalSettingPath = path,
                        FieldName = field.Name,
                        GeneralSettingPath = settingPath,
                    });
                }
            }

            return new VMFrameworkCreateMissingGeneralSettingsResult
            {
                GeneralSettingsFolderPath = folder,
                GlobalSettingCount = files.Count,
                GeneralSettingCount = settingPaths.Count,
                CreatedGeneralSettings = createdPaths.OrderBy(path => path, StringComparer.Ordinal).ToList(),
                Bindings = bindings,
            };
        }

        private static VmProjectToolException Failure(string code, string message)
        {
            return new VmProjectToolException(code, message);
        }
    }
}
#endif
