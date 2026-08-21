#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
            Description = "Validate every discoverable VMFramework GamePrefabWrapper and report null GamePrefab entries plus missing or unreadable IPrefabProvider.Prefab references.",
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
            var issues = new List<VMFrameworkGamePrefabValidationIssue>(
                Math.Min(maxIssues, 128));
            var gamePrefabs = new List<IGamePrefab>();
            int gamePrefabEntryCount = 0;
            int gamePrefabCount = 0;
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
                PrefabProviderCount = prefabProviderCount,
                MissingPrefabCount = missingPrefabCount,
                ErrorCount = errorCount,
                TotalIssues = errorCount,
                ReturnedIssues = issues.Count,
                Truncated = issues.Count < errorCount,
                Issues = issues,
            };
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
    }
}
#endif
