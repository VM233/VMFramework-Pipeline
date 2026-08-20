#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Localization;
using VMUnityAutomation.Editor;
using VMFramework.GameLogicArchitecture;
using VMFramework.GameLogicArchitecture.Editor;
using static VMFramework.Pipeline.Editor.VMFrameworkPipelineTools;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkPipelineGamePrefabQueryTools
    {
        private const string QueryGamePrefabConfigsToolName =
            "vmframework/query-game-prefab-configs";
        private const int MaximumGeneralSettings = 256;
        private const int MaximumRegisteredProviders = 10000;
        private const int MaximumWrappers = 10000;
        private const int MaximumGamePrefabs = 10000;
        private const int MaximumTagFiltersPerMode = 64;
        private const int MaximumLocales = 16;
        private const int MaximumPageSize = 500;

        [VmProjectTool(QueryGamePrefabConfigsToolName,
            Description = "Query registered VMFramework GamePrefab configs by identity, assignable type, GameTags, and localized-text availability, returning canonical references plus explicitly projected tags or localized text.",
            ReadOnly = true)]
        public static VMFrameworkQueryGamePrefabConfigsResult QueryGamePrefabConfigs(
            VMFrameworkQueryGamePrefabConfigsRequest request)
        {
            Type gamePrefabType = string.IsNullOrWhiteSpace(request.GamePrefabType)
                ? null
                : ResolveGamePrefabType(request.GamePrefabType, allowAbstract: true);
            HashSet<string> gameTagsAll = NormalizeTagFilter(
                request.GameTagsAll, "gameTagsAll");
            HashSet<string> gameTagsAny = NormalizeTagFilter(
                request.GameTagsAny, "gameTagsAny");
            HashSet<string> gameTagsNone = NormalizeTagFilter(
                request.GameTagsNone, "gameTagsNone");
            HashSet<VMFrameworkGamePrefabConfigField> fields =
                NormalizeFields(request.Fields);
            HashSet<string> locales = NormalizeLocales(request.Locales, fields);
            int limit = VMFrameworkPipelineSettingsManager.ResolveResultLimit(
                request.Limit, 100, MaximumPageSize);

            List<QueryCandidate> allCandidates = FindCandidates(request, gamePrefabType,
                gameTagsAll, gameTagsAny, gameTagsNone);
            List<QueryCandidate> page = allCandidates
                .Skip(request.Offset)
                .Take(limit)
                .ToList();
            var localizedStringReader = new VMFrameworkLocalizedStringReader();
            List<VMFrameworkGamePrefabConfigRecord> records = page
                .Select(candidate => ProjectCandidate(candidate, fields, locales,
                    localizedStringReader))
                .ToList();
            int nextOffset = request.Offset + records.Count;
            return new VMFrameworkQueryGamePrefabConfigsResult
            {
                GamePrefabs = records,
                Count = records.Count,
                Total = allCandidates.Count,
                Offset = request.Offset,
                Limit = limit,
                NextOffset = nextOffset < allCandidates.Count ? nextOffset : (int?)null,
            };
        }

        private static List<QueryCandidate> FindCandidates(
            VMFrameworkQueryGamePrefabConfigsRequest request, Type gamePrefabType,
            ISet<string> gameTagsAll, ISet<string> gameTagsAny,
            ISet<string> gameTagsNone)
        {
            List<GamePrefabGeneralSetting> generalSettings =
                GetAllGamePrefabGeneralSettings();
            if (generalSettings.Count > MaximumGeneralSettings)
            {
                throw new InvalidOperationException(
                    $"The project has {generalSettings.Count} GamePrefabGeneralSettings; " +
                    $"the query capacity is {MaximumGeneralSettings}.");
            }

            Dictionary<GamePrefabWrapper, List<GamePrefabGeneralSetting>> ownersByWrapper =
                IndexOwners(generalSettings);
            List<GamePrefabWrapper> wrappers = GamePrefabWrapperQueryTools
                .GetAllGamePrefabWrappers()
                .ToList();
            if (wrappers.Count > MaximumWrappers)
            {
                throw new InvalidOperationException(
                    $"The project has {wrappers.Count} GamePrefab wrappers; " +
                    $"the query capacity is {MaximumWrappers}.");
            }

            var candidates = new List<QueryCandidate>();
            int gamePrefabCount = 0;
            foreach (GamePrefabWrapper wrapper in wrappers)
            {
                string wrapperPath = AssetDatabase.GetAssetPath(wrapper);
                var gamePrefabs = new List<IGamePrefab>();
                wrapper.GetGamePrefabs(gamePrefabs);
                foreach (IGamePrefab gamePrefab in gamePrefabs)
                {
                    gamePrefabCount++;
                    if (gamePrefabCount > MaximumGamePrefabs)
                    {
                        throw new InvalidOperationException(
                            $"The project has more than {MaximumGamePrefabs} GamePrefab configs, " +
                            "which exceeds the query capacity.");
                    }
                    if (gamePrefab == null)
                    {
                        throw new InvalidOperationException(
                            $"GamePrefab wrapper '{wrapperPath}' contains a null config.");
                    }
                    if (!MatchesRequest(request, gamePrefabType, gameTagsAll,
                            gameTagsAny, gameTagsNone, gamePrefab, wrapper, wrapperPath))
                    {
                        continue;
                    }

                    if (!ownersByWrapper.TryGetValue(wrapper,
                            out List<GamePrefabGeneralSetting> owners) || owners.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"GamePrefab '{gamePrefab.id}' is not registered to an authoritative " +
                            "GamePrefabGeneralSetting.");
                    }
                    if (owners.Count > 1)
                    {
                        throw new InvalidOperationException(
                            $"GamePrefab '{gamePrefab.id}' is registered to more than one " +
                            $"GamePrefabGeneralSetting: {string.Join(", ", owners.Select(owner => owner.name))}.");
                    }

                    candidates.Add(new QueryCandidate
                    {
                        GamePrefab = gamePrefab,
                        Reference = CreateGamePrefabReference(gamePrefab, wrapper, owners[0]),
                    });
                }
            }

            return candidates
                .OrderBy(candidate => candidate.Reference.FullTypeName,
                    StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Reference.Id, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Reference.WrapperPath,
                    StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<GamePrefabWrapper, List<GamePrefabGeneralSetting>>
            IndexOwners(IEnumerable<GamePrefabGeneralSetting> generalSettings)
        {
            var ownersByWrapper =
                new Dictionary<GamePrefabWrapper, List<GamePrefabGeneralSetting>>();
            int providerCount = 0;
            foreach (GamePrefabGeneralSetting generalSetting in generalSettings)
            {
                if (generalSetting.initialGamePrefabProviders == null)
                {
                    throw new InvalidOperationException(
                        $"GamePrefabGeneralSetting '{generalSetting.name}' has no provider collection.");
                }
                foreach (IGamePrefabsProvider provider in
                         generalSetting.initialGamePrefabProviders)
                {
                    providerCount++;
                    if (providerCount > MaximumRegisteredProviders)
                    {
                        throw new InvalidOperationException(
                            $"The project has more than {MaximumRegisteredProviders} registered " +
                            "GamePrefab providers, which exceeds the query capacity.");
                    }
                    if (!(provider is GamePrefabWrapper wrapper))
                        continue;
                    if (!ownersByWrapper.TryGetValue(wrapper,
                            out List<GamePrefabGeneralSetting> owners))
                    {
                        owners = new List<GamePrefabGeneralSetting>();
                        ownersByWrapper.Add(wrapper, owners);
                    }
                    if (!owners.Contains(generalSetting))
                        owners.Add(generalSetting);
                }
            }
            return ownersByWrapper;
        }

        private static bool MatchesRequest(
            VMFrameworkQueryGamePrefabConfigsRequest request, Type gamePrefabType,
            ISet<string> gameTagsAll, ISet<string> gameTagsAny,
            ISet<string> gameTagsNone, IGamePrefab gamePrefab,
            GamePrefabWrapper wrapper, string wrapperPath)
        {
            if (string.IsNullOrWhiteSpace(request.Id) == false &&
                string.Equals(gamePrefab.id, request.Id, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (gamePrefabType != null &&
                !gamePrefabType.IsAssignableFrom(gamePrefab.GetType()))
            {
                return false;
            }
            if (!MatchesGamePrefabFilter(gamePrefab, wrapper, wrapperPath,
                    request.Filter))
            {
                return false;
            }
            if (!MatchesGameTags(gamePrefab, gameTagsAll, gameTagsAny,
                    gameTagsNone))
            {
                return false;
            }
            if (request.HasName.HasValue &&
                request.HasName.Value != HasLocalizedName(gamePrefab))
            {
                return false;
            }
            return !request.HasDescription.HasValue ||
                   request.HasDescription.Value == HasLocalizedDescription(gamePrefab);
        }

        private static bool MatchesGameTags(IGamePrefab gamePrefab,
            ISet<string> gameTagsAll, ISet<string> gameTagsAny,
            ISet<string> gameTagsNone)
        {
            ICollection<string> gameTags = gamePrefab.GameTags;
            if (gameTags == null)
            {
                throw new InvalidOperationException(
                    $"GamePrefab '{gamePrefab.id}' has no GameTag collection.");
            }
            return gameTagsAll.All(gameTags.Contains) &&
                   (gameTagsAny.Count == 0 || gameTagsAny.Any(gameTags.Contains)) &&
                   gameTagsNone.All(tag => !gameTags.Contains(tag));
        }

        private static VMFrameworkGamePrefabConfigRecord ProjectCandidate(
            QueryCandidate candidate,
            ISet<VMFrameworkGamePrefabConfigField> fields,
            ISet<string> locales,
            VMFrameworkLocalizedStringReader localizedStringReader)
        {
            var record = new VMFrameworkGamePrefabConfigRecord
            {
                GamePrefab = candidate.Reference,
            };
            if (fields.Contains(VMFrameworkGamePrefabConfigField.GameTags))
            {
                record.GameTags = candidate.GamePrefab.GameTags
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToList();
            }
            if (fields.Contains(VMFrameworkGamePrefabConfigField.Name) &&
                HasLocalizedName(candidate.GamePrefab))
            {
                var nameOwner = (ILocalizedNameOwner)candidate.GamePrefab;
                LocalizedString nameReference = nameOwner.NameReference;
                record.Name = localizedStringReader.Read(nameReference,
                    true, locales, MaximumLocales);
            }
            if (fields.Contains(VMFrameworkGamePrefabConfigField.Description) &&
                HasLocalizedDescription(candidate.GamePrefab))
            {
                if (!(candidate.GamePrefab is ILocalizedDescriptionOwner descriptionOwner))
                {
                    throw new InvalidOperationException(
                        $"GamePrefab '{candidate.GamePrefab.id}' enables a description without " +
                        "implementing ILocalizedDescriptionOwner.");
                }
                LocalizedString descriptionReference =
                    descriptionOwner.DescriptionReference;
                if (descriptionReference == null)
                {
                    throw new InvalidOperationException(
                        $"GamePrefab '{candidate.GamePrefab.id}' has a null description reference.");
                }
                record.Description = localizedStringReader.Read(descriptionReference,
                    true, locales, MaximumLocales);
            }
            return record;
        }

        private static bool HasLocalizedDescription(IGamePrefab gamePrefab)
        {
            if (gamePrefab is LocalizedGamePrefab localizedGamePrefab)
                return localizedGamePrefab.hasDescription;
            return gamePrefab is ILocalizedDescriptionOwner;
        }

        private static bool HasLocalizedName(IGamePrefab gamePrefab)
        {
            return gamePrefab is ILocalizedNameOwner nameOwner &&
                   nameOwner.NameReference != null;
        }

        private static HashSet<string> NormalizeTagFilter(
            IReadOnlyCollection<string> values, string propertyName)
        {
            if (values == null)
                return new HashSet<string>(StringComparer.Ordinal);
            if (values.Count > MaximumTagFiltersPerMode)
            {
                throw new ArgumentException(
                    $"{propertyName} accepts at most {MaximumTagFiltersPerMode} values.");
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"{propertyName} cannot contain an empty GameTag id.");
                result.Add(value);
            }
            return result;
        }

        private static HashSet<VMFrameworkGamePrefabConfigField> NormalizeFields(
            IReadOnlyCollection<VMFrameworkGamePrefabConfigField> values)
        {
            if (values == null)
                return new HashSet<VMFrameworkGamePrefabConfigField>();
            var fields = new HashSet<VMFrameworkGamePrefabConfigField>(values);
            if (fields.Count != values.Count)
                throw new ArgumentException("fields cannot contain duplicate values.");
            return fields;
        }

        private static HashSet<string> NormalizeLocales(IReadOnlyCollection<string> values,
            ISet<VMFrameworkGamePrefabConfigField> fields)
        {
            if (values == null)
                return null;
            if (!fields.Contains(VMFrameworkGamePrefabConfigField.Name) &&
                !fields.Contains(VMFrameworkGamePrefabConfigField.Description))
            {
                throw new ArgumentException(
                    "locales requires the name or description field projection.");
            }
            if (values.Count > MaximumLocales)
            {
                throw new ArgumentException(
                    $"locales accepts at most {MaximumLocales} values.");
            }

            var locales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("locales cannot contain an empty locale code.");
                if (!locales.Add(value))
                    throw new ArgumentException($"locales contains duplicate code '{value}'.");
            }
            return locales;
        }

        private sealed class QueryCandidate
        {
            internal IGamePrefab GamePrefab { get; set; }
            internal VMFrameworkGamePrefabReference Reference { get; set; }
        }
    }
}
#endif
