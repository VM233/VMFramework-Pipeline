#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using VMFramework.Core;
using VMFramework.GameLogicArchitecture;
using VMUnityAutomation.Editor;
using static VMFramework.Pipeline.Editor.VMFrameworkPipelineTools;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkGamePrefabValidationTool
    {
        private const string ToolName = "vmframework/validate-game-prefabs";
        private const int MaximumWrappers = 10000;
        private const int MaximumGamePrefabs = 10000;
        private const int MaximumIssues = 5000;

        [VmProjectTool(ToolName,
            Description = "Validate every discoverable VMFramework GamePrefabWrapper, including runtime registration reachability, null GamePrefab entries, and missing or unreadable IPrefabProvider.Prefab references.",
            ReadOnly = true,
            ErrorCodes = new[]
            {
                "game_prefab_validation_capacity_exceeded",
                "game_prefab_validation_scan_failed",
            })]
        public static VMFrameworkValidateGamePrefabsResult ValidateGamePrefabs(
            VMFrameworkValidateGamePrefabsRequest request)
        {
            request ??= new VMFrameworkValidateGamePrefabsRequest();
            int maxIssues = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                request.MaxIssues, 500, MaximumIssues);
            List<WrapperRecord> wrappers = CollectWrappers();
            HashSet<IGamePrefab> runtimeGamePrefabs = CollectRuntimeGamePrefabs();
            var issues = new List<VMFrameworkGamePrefabValidationIssue>(
                Math.Min(maxIssues, 128));
            var gamePrefabs = new List<IGamePrefab>();
            int gamePrefabEntryCount = 0;
            int gamePrefabCount = 0;
            int registeredGamePrefabCount = 0;
            int unregisteredGamePrefabCount = 0;
            int prefabProviderCount = 0;
            int missingPrefabCount = 0;
            int errorCount = 0;

            foreach (WrapperRecord wrapper in wrappers)
            {
                gamePrefabs.Clear();
                try
                {
                    wrapper.Wrapper.GetGamePrefabs(gamePrefabs);
                }
                catch (Exception exception)
                {
                    AddIssue(CreateWrapperReadIssue(wrapper.Path, exception), issues,
                        maxIssues, ref errorCount);
                    continue;
                }

                for (int index = 0; index < gamePrefabs.Count; index++)
                {
                    gamePrefabEntryCount++;
                    if (gamePrefabEntryCount > MaximumGamePrefabs)
                    {
                        throw CreateCapacityException("gamePrefabs",
                            MaximumGamePrefabs, gamePrefabEntryCount);
                    }

                    IGamePrefab gamePrefab = gamePrefabs[index];
                    if (gamePrefab == null)
                    {
                        AddIssue(CreateNullConfigIssue(wrapper.Path, index), issues,
                            maxIssues, ref errorCount);
                        continue;
                    }

                    gamePrefabCount++;
                    VMFrameworkGamePrefabValidationIssue registrationIssue =
                        CreateRegistrationIssue(wrapper.Path, gamePrefab,
                            runtimeGamePrefabs);
                    if (registrationIssue == null)
                    {
                        registeredGamePrefabCount++;
                    }
                    else
                    {
                        unregisteredGamePrefabCount++;
                        AddIssue(registrationIssue, issues, maxIssues,
                            ref errorCount);
                    }

                    if (!(gamePrefab is IPrefabProvider))
                    {
                        continue;
                    }

                    prefabProviderCount++;
                    VMFrameworkGamePrefabValidationIssue issue =
                        CreatePrefabReferenceIssue(wrapper.Path, gamePrefab);
                    if (issue == null)
                    {
                        continue;
                    }

                    if (string.Equals(issue.Code, "missing_prefab_reference",
                            StringComparison.Ordinal))
                    {
                        missingPrefabCount++;
                    }
                    AddIssue(issue, issues, maxIssues, ref errorCount);
                }
            }

            return new VMFrameworkValidateGamePrefabsResult
            {
                Passed = errorCount == 0,
                WrapperCount = wrappers.Count,
                GamePrefabCount = gamePrefabCount,
                RegisteredGamePrefabCount = registeredGamePrefabCount,
                UnregisteredGamePrefabCount = unregisteredGamePrefabCount,
                PrefabProviderCount = prefabProviderCount,
                MissingPrefabCount = missingPrefabCount,
                ErrorCount = errorCount,
                TotalIssues = errorCount,
                ReturnedIssues = issues.Count,
                Truncated = issues.Count < errorCount,
                Issues = issues,
            };
        }

        internal static VMFrameworkGamePrefabValidationIssue CreateRegistrationIssue(
            string wrapperPath, IGamePrefab gamePrefab,
            ISet<IGamePrefab> runtimeGamePrefabs)
        {
            if (runtimeGamePrefabs.Contains(gamePrefab))
            {
                return null;
            }

            return CreateIssue("unregistered_game_prefab", gamePrefab,
                wrapperPath, "IGamePrefabsProvider.GetGamePrefabs",
                $"GamePrefab '{gamePrefab.id}' is discoverable through wrapper " +
                $"'{wrapperPath}' but is unreachable from the runtime " +
                "GlobalSettingCollector provider graph.");
        }

        internal static VMFrameworkGamePrefabValidationIssue CreatePrefabReferenceIssue(
            string wrapperPath, IGamePrefab gamePrefab)
        {
            if (!(gamePrefab is IPrefabProvider prefabProvider))
            {
                return null;
            }

            try
            {
                if (prefabProvider.Prefab != null)
                {
                    return null;
                }

                return CreateIssue("missing_prefab_reference", gamePrefab,
                    wrapperPath, "IPrefabProvider.Prefab",
                    $"GamePrefab '{gamePrefab.id}' implements IPrefabProvider but its Prefab reference is null or destroyed.");
            }
            catch (Exception exception)
            {
                return CreateIssue("prefab_reference_read_failed", gamePrefab,
                    wrapperPath, "IPrefabProvider.Prefab",
                    $"Reading the Prefab reference of GamePrefab '{gamePrefab.id}' failed: " +
                    exception.GetBaseException().Message);
            }
        }

        private static List<WrapperRecord> CollectWrappers()
        {
            var records = new List<WrapperRecord>();
            var seen = new HashSet<GamePrefabWrapper>();
            try
            {
                foreach (GamePrefabWrapper wrapper in GetAllGamePrefabWrappers())
                {
                    if (wrapper == null || !seen.Add(wrapper))
                    {
                        continue;
                    }
                    if (records.Count >= MaximumWrappers)
                    {
                        throw CreateCapacityException("wrappers", MaximumWrappers,
                            records.Count + 1);
                    }
                    records.Add(new WrapperRecord(wrapper,
                        AssetDatabase.GetAssetPath(wrapper) ?? ""));
                }
            }
            catch (VmProjectToolException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new VmProjectToolException("game_prefab_validation_scan_failed",
                    "GamePrefab wrapper discovery failed: " +
                    exception.GetBaseException().Message, false,
                    new Dictionary<string, object>
                    {
                        { "exceptionType", exception.GetType().FullName },
                    });
            }

            records.Sort((left, right) => string.Compare(left.Path, right.Path,
                StringComparison.Ordinal));
            return records;
        }

        private static HashSet<IGamePrefab> CollectRuntimeGamePrefabs()
        {
            var collected = new List<IGamePrefab>();
            try
            {
                foreach (IGeneralSetting generalSetting in
                         GlobalSettingCollector.GetAllGeneralSettings())
                {
                    if (generalSetting is GamePrefabGeneralSetting gamePrefabSetting)
                    {
                        CollectInitialGamePrefabProviders(gamePrefabSetting,
                            collected);
                    }
                    else if (generalSetting is IGamePrefabsProvider provider)
                    {
                        provider.GetGamePrefabs(collected);
                    }

                    if (collected.Count > MaximumGamePrefabs)
                    {
                        throw CreateCapacityException("runtimeGamePrefabs",
                            MaximumGamePrefabs, collected.Count);
                    }
                }
            }
            catch (VmProjectToolException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new VmProjectToolException(
                    "game_prefab_validation_scan_failed",
                    "Runtime GamePrefab provider collection failed: " +
                    exception.GetBaseException().Message, false,
                    new Dictionary<string, object>
                    {
                        { "exceptionType", exception.GetType().FullName },
                    });
            }

            var runtimeGamePrefabs = new HashSet<IGamePrefab>(
                GamePrefabReferenceComparer.Instance);
            foreach (IGamePrefab gamePrefab in collected)
            {
                if (gamePrefab != null)
                {
                    runtimeGamePrefabs.Add(gamePrefab);
                }
            }

            return runtimeGamePrefabs;
        }

        private static void CollectInitialGamePrefabProviders(
            GamePrefabGeneralSetting setting,
            ICollection<IGamePrefab> gamePrefabs)
        {
            if (setting.initialGamePrefabProviders == null)
            {
                return;
            }

            foreach (IGamePrefabsProvider provider in
                     setting.initialGamePrefabProviders)
            {
                if (provider.IsUnityNull())
                {
                    continue;
                }

                provider.GetGamePrefabs(gamePrefabs);
                if (gamePrefabs.Count > MaximumGamePrefabs)
                {
                    throw CreateCapacityException("runtimeGamePrefabs",
                        MaximumGamePrefabs, gamePrefabs.Count);
                }
            }
        }

        private static VmProjectToolException CreateCapacityException(
            string dimension, int maximum, int observed)
        {
            return new VmProjectToolException(
                "game_prefab_validation_capacity_exceeded",
                $"GamePrefab validation exceeded the {dimension} capacity of {maximum}.",
                false, new Dictionary<string, object>
                {
                    { "dimension", dimension },
                    { "maximum", maximum },
                    { "observed", observed },
                });
        }

        private static VMFrameworkGamePrefabValidationIssue CreateNullConfigIssue(
            string wrapperPath, int index)
        {
            return new VMFrameworkGamePrefabValidationIssue
            {
                Code = "null_game_prefab_config",
                Severity = "error",
                GamePrefabId = "",
                FullTypeName = "",
                WrapperPath = wrapperPath,
                Member = $"GamePrefabWrapper[{index}]",
                Message = $"GamePrefabWrapper '{wrapperPath}' contains a null GamePrefab config at index {index}.",
            };
        }

        private static VMFrameworkGamePrefabValidationIssue CreateWrapperReadIssue(
            string wrapperPath, Exception exception)
        {
            return new VMFrameworkGamePrefabValidationIssue
            {
                Code = "game_prefab_wrapper_read_failed",
                Severity = "error",
                GamePrefabId = "",
                FullTypeName = "",
                WrapperPath = wrapperPath,
                Member = "GamePrefabWrapper.GetGamePrefabs",
                Message = $"Reading GamePrefabs from wrapper '{wrapperPath}' failed: " +
                          exception.GetBaseException().Message,
            };
        }

        private static VMFrameworkGamePrefabValidationIssue CreateIssue(string code,
            IGamePrefab gamePrefab, string wrapperPath, string member, string message)
        {
            return new VMFrameworkGamePrefabValidationIssue
            {
                Code = code,
                Severity = "error",
                GamePrefabId = gamePrefab.id ?? "",
                FullTypeName = gamePrefab.GetType().FullName ?? gamePrefab.GetType().Name,
                WrapperPath = wrapperPath ?? "",
                Member = member,
                Message = message,
            };
        }

        private static void AddIssue(VMFrameworkGamePrefabValidationIssue issue,
            ICollection<VMFrameworkGamePrefabValidationIssue> issues, int maxIssues,
            ref int errorCount)
        {
            errorCount++;
            if (issues.Count < maxIssues)
            {
                issues.Add(issue);
            }
        }

        private sealed class WrapperRecord
        {
            public GamePrefabWrapper Wrapper { get; }

            public string Path { get; }

            public WrapperRecord(GamePrefabWrapper wrapper, string path)
            {
                Wrapper = wrapper;
                Path = path;
            }
        }

        private sealed class GamePrefabReferenceComparer :
            IEqualityComparer<IGamePrefab>
        {
            public static readonly GamePrefabReferenceComparer Instance = new();

            public bool Equals(IGamePrefab left, IGamePrefab right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(IGamePrefab gamePrefab)
            {
                return RuntimeHelpers.GetHashCode(gamePrefab);
            }
        }
    }
}
#endif
