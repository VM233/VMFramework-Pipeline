#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using VMUnityAutomation.Editor;
using VMFramework.GameLogicArchitecture;
using VMFramework.GameLogicArchitecture.Editor;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor
{
    [VmProjectTool(ToolName,
        ShortName = "vmf/reference-trace",
        Description = "Trace VMFramework GamePrefabs, wrappers, prefabs, components, GameTags, localization, dependencies, and optional reverse references.",
        InputSchemaJson = InputSchema,
        OutputSchemaJson = OutputSchema,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[]
        {
            "reference_trace_not_found",
            "reference_trace_ambiguous",
            "reference_trace_kind_mismatch",
            "persistent_job_required",
        },
        ReadOnly = true)]
    public sealed class VMFrameworkReferenceTraceTool : IVmPersistentProjectTool
    {
        private static readonly string[] ReverseReferenceExtensions =
        {
            ".asset",
            ".prefab",
            ".unity",
            ".controller",
            ".overrideController",
            ".playable",
            ".uxml",
            ".uss",
            ".mat",
        };

        private const string ToolName = "vmframework/reference-trace";
        private const string InputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"query\":{\"type\":\"string\",\"minLength\":1,\"description\":\"Exact GamePrefab id, wrapper path, prefab path, or asset path/name query.\"}," +
            "\"kind\":{\"type\":\"string\",\"enum\":[\"auto\",\"gamePrefab\",\"wrapper\",\"prefab\",\"asset\"],\"default\":\"auto\",\"description\":\"Semantic query owner; auto resolves exact paths and GamePrefab ids without type-name heuristics.\"}," +
            "\"propertyName\":{\"type\":\"string\",\"description\":\"Optional serialized property-name/path filter on prefab components.\"}," +
            "\"includeReverseReferences\":{\"type\":\"boolean\",\"default\":true,\"description\":\"Scan project assets that directly depend on resolved assets; requires runAsJob=true.\"}," +
            "\"maxReverseReferences\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":500,\"default\":100,\"description\":\"Maximum returned reverse-reference assets.\"}," +
            "\"maxComponents\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000,\"default\":300,\"description\":\"Maximum returned components across each traced prefab.\"}," +
            "\"reverseReferencesPerStep\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000,\"default\":100,\"description\":\"Candidate assets scanned per persistent Job step.\"}" +
            "},\"required\":[\"query\"],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"query\":{\"type\":\"string\"}," +
            "\"kind\":{\"type\":\"string\"}," +
            "\"resolved\":{\"type\":\"object\",\"properties\":{" +
            "\"wrapperPaths\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"gamePrefabs\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"prefabPaths\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"assetPath\":{\"type\":\"string\"}," +
            "\"assetType\":{\"type\":\"string\"}" +
            "},\"required\":[\"wrapperPaths\",\"gamePrefabs\",\"prefabPaths\",\"assetPath\",\"assetType\"],\"additionalProperties\":false}," +
            "\"graph\":{\"type\":\"object\",\"properties\":{" +
            "\"nodes\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"edges\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}" +
            "},\"required\":[\"nodes\",\"edges\"],\"additionalProperties\":false}," +
            "\"prefabs\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"tags\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"localization\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"dependencies\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"reverseReferences\":{\"type\":\"object\",\"properties\":{" +
            "\"scanned\":{\"type\":\"boolean\"}," +
            "\"scannedAssetCount\":{\"type\":\"integer\"}," +
            "\"targetPaths\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"total\":{\"type\":\"integer\"}," +
            "\"returned\":{\"type\":\"integer\"}," +
            "\"truncated\":{\"type\":\"boolean\"}," +
            "\"assets\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}" +
            "},\"required\":[\"scanned\",\"scannedAssetCount\",\"targetPaths\",\"total\",\"returned\",\"truncated\",\"assets\"],\"additionalProperties\":false}," +
            "\"actualSideEffects\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"uniqueItems\":true}" +
            "},\"required\":[\"query\",\"kind\",\"resolved\",\"graph\",\"prefabs\",\"tags\",\"localization\",\"dependencies\",\"reverseReferences\",\"actualSideEffects\"],\"additionalProperties\":false}";

        public object Execute(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            if (GetBool(args, "includeReverseReferences", true))
            {
                throw new VmProjectToolException("persistent_job_required",
                    "Reverse-reference scanning must use runAsJob=true so it can yield and be canceled.");
            }

            Dictionary<string, object> result = BuildBaseTrace(args,
                out HashSet<string> targetPaths);
            result["reverseReferences"] = BuildReverseReferenceResult(
                scanned: false, scannedAssetCount: 0, targetPaths,
                total: 0, new List<Dictionary<string, object>>());
            result["actualSideEffects"] = new List<string>
            {
                "readsProjectState",
            };
            return result;
        }

        public VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
            Dictionary<string, object> state)
        {
            args ??= new Dictionary<string, object>();
            state ??= new Dictionary<string, object>();
            if (!GetBool(args, "includeReverseReferences", true))
                return VmProjectToolJobStep.Complete(Execute(args));

            Dictionary<string, object> trace;
            List<string> candidates;
            HashSet<string> targetPaths;
            List<Dictionary<string, object>> references;
            int index;
            int total;
            if (!state.TryGetValue("trace", out object traceValue))
            {
                trace = BuildBaseTrace(args, out targetPaths);
                candidates = GetReverseReferenceCandidates(targetPaths);
                references = new List<Dictionary<string, object>>();
                index = 0;
                total = 0;
            }
            else
            {
                trace = ToDictionary(traceValue);
                candidates = ToStringList(state["candidates"]);
                targetPaths = ToStringList(state["targetPaths"])
                    .ToHashSet(StringComparer.Ordinal);
                references = ToDictionaryList(state["references"]);
                index = Convert.ToInt32(state["index"],
                    CultureInfo.InvariantCulture);
                total = Convert.ToInt32(state["total"],
                    CultureInfo.InvariantCulture);
            }

            int maxReferences = GetInt(args, "maxReverseReferences", 100);
            int batchSize = GetInt(args, "reverseReferencesPerStep", 100);
            int end = Math.Min(candidates.Count, index + batchSize);
            for (; index < end; index++)
            {
                string candidate = candidates[index];
                List<string> matched = AssetDatabase.GetDependencies(
                        candidate, false)
                    .Where(targetPaths.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();
                if (matched.Count == 0)
                    continue;

                total++;
                if (references.Count < maxReferences)
                {
                    references.Add(new Dictionary<string, object>
                    {
                        { "assetPath", candidate },
                        { "assetType", AssetDatabase
                            .GetMainAssetTypeAtPath(candidate)?.FullName ?? "" },
                        { "references", matched },
                    });
                }
            }

            if (index >= candidates.Count)
            {
                trace["reverseReferences"] = BuildReverseReferenceResult(
                    scanned: true, candidates.Count, targetPaths, total, references);
                trace["actualSideEffects"] = new List<string>
                {
                    "readsProjectState",
                    "waitsAcrossEditorFrames",
                };
                return VmProjectToolJobStep.Complete(trace);
            }

            return VmProjectToolJobStep.Pending(
                new Dictionary<string, object>
                {
                    { "trace", trace },
                    { "candidates", candidates },
                    { "targetPaths", targetPaths.OrderBy(path => path,
                        StringComparer.Ordinal).ToList() },
                    { "references", references },
                    { "index", index },
                    { "total", total },
                },
                candidates.Count == 0 ? 1 : (double)index / candidates.Count,
                $"Scanned {index} of {candidates.Count} candidate assets for reverse references.",
                delayMilliseconds: 0);
        }

        private static Dictionary<string, object> BuildBaseTrace(
            IReadOnlyDictionary<string, object> args,
            out HashSet<string> targetPaths)
        {
            string query = GetRequiredString(args, "query");
            string kind = GetString(args, "kind", "auto");
            string propertyName = GetString(args, "propertyName");
            int maxComponents = GetInt(args, "maxComponents", 300);
            TraceRoot root = ResolveRoot(query, kind);

            var nodes = new List<Dictionary<string, object>>();
            var edges = new List<Dictionary<string, object>>();
            var tags = new List<Dictionary<string, object>>();
            var localizations = new List<Dictionary<string, object>>();
            targetPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (GamePrefabWrapper wrapper in root.Wrappers)
            {
                string wrapperPath = AssetDatabase.GetAssetPath(wrapper);
                if (string.IsNullOrWhiteSpace(wrapperPath))
                    continue;
                targetPaths.Add(wrapperPath);
                AddNode(nodes, WrapperNode(wrapperPath), wrapperPath, wrapper.name,
                    wrapper.GetType().FullName);
            }

            if (root.Asset != null && !string.IsNullOrWhiteSpace(root.AssetPath))
            {
                targetPaths.Add(root.AssetPath);
                AddNode(nodes, AssetNode(root.AssetPath), root.AssetPath,
                    root.Asset.name, root.Asset.GetType().FullName);
            }

            foreach (GamePrefabResolution resolution in root.GamePrefabs)
            {
                IGamePrefab gamePrefab = resolution.GamePrefab;
                string gamePrefabNode = GamePrefabNode(gamePrefab.id);
                AddNode(nodes, gamePrefabNode, resolution.WrapperPath,
                    gamePrefab.id, gamePrefab.GetType().FullName);
                if (!string.IsNullOrWhiteSpace(resolution.WrapperPath))
                {
                    AddEdge(edges, WrapperNode(resolution.WrapperPath),
                        "contains", gamePrefabNode);
                }

                foreach (string tag in (gamePrefab.GameTags ??
                             Array.Empty<string>())
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
                {
                    Dictionary<string, object> tagInfo = ResolveTag(tag);
                    if (tags.All(existing => !string.Equals(
                            GetNestedString(existing, "id"), tag,
                            StringComparison.Ordinal)))
                    {
                        tags.Add(tagInfo);
                    }
                    string tagNode = TagNode(tag);
                    AddNode(nodes, tagNode,
                        GetNestedString(tagInfo, "groupPath"), tag, "GameTag");
                    AddEdge(edges, gamePrefabNode, "tagged-with", tagNode);
                }

                if (gamePrefab is LocalizedGamePrefab localized)
                {
                    AddLocalization(localizations, nodes, edges,
                        gamePrefabNode, "name", localized.name);
                    if (localized.hasDescription)
                    {
                        AddLocalization(localizations, nodes, edges,
                            gamePrefabNode, "description",
                            localized.description);
                    }
                }
            }

            var prefabByPath = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
            foreach (GameObject prefab in root.Prefabs.Where(value => value != null))
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                if (!string.IsNullOrWhiteSpace(path))
                    prefabByPath[path] = prefab;
            }
            foreach (GamePrefabResolution resolution in root.GamePrefabs)
            {
                GameObject prefab = GetPrefab(resolution.GamePrefab);
                string path = AssetDatabase.GetAssetPath(prefab);
                if (prefab != null && !string.IsNullOrWhiteSpace(path))
                {
                    prefabByPath[path] = prefab;
                    AddEdge(edges, GamePrefabNode(resolution.GamePrefab.id),
                        "uses-prefab", PrefabNode(path));
                }
            }

            var prefabTraces = new List<Dictionary<string, object>>();
            foreach ((string path, GameObject prefab) in prefabByPath
                         .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                targetPaths.Add(path);
                AddNode(nodes, PrefabNode(path), path, prefab.name,
                    typeof(GameObject).FullName);
                prefabTraces.Add(TracePrefab(prefab, propertyName,
                    maxComponents, nodes, edges));
            }

            List<string> dependencyPaths = targetPaths
                .SelectMany(path => AssetDatabase.GetDependencies(path, true))
                .Where(path => path.StartsWith("Assets/",
                    StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            foreach (string sourcePath in targetPaths)
            {
                foreach (string dependencyPath in AssetDatabase
                             .GetDependencies(sourcePath, true)
                             .Where(path => path.StartsWith("Assets/",
                                 StringComparison.Ordinal) &&
                                            !string.Equals(path, sourcePath,
                                                StringComparison.Ordinal)))
                {
                    AddEdge(edges, NodeForAssetPath(sourcePath),
                        "depends-on", AssetNode(dependencyPath));
                }
            }

            return new Dictionary<string, object>
            {
                { "query", query },
                { "kind", kind },
                { "resolved", DescribeRoot(root, prefabByPath.Keys) },
                { "graph", new Dictionary<string, object>
                    {
                        { "nodes", nodes },
                        { "edges", edges },
                    }
                },
                { "prefabs", prefabTraces },
                { "tags", tags },
                { "localization", localizations },
                { "dependencies", dependencyPaths },
            };
        }

        private static TraceRoot ResolveRoot(string query, string rawKind)
        {
            string kind = string.IsNullOrWhiteSpace(rawKind)
                ? "auto"
                : rawKind;
            TraceIndex index = TraceIndex.Build();
            string path = NormalizeAssetPath(query);
            Object pathAsset = path.StartsWith("Assets/",
                    StringComparison.Ordinal)
                ? AssetDatabase.LoadMainAssetAtPath(path)
                : null;

            if (kind == "gamePrefab")
                return ResolveGamePrefab(query, index);
            if (kind == "wrapper")
                return ResolveWrapper(query, pathAsset, index);
            if (kind == "prefab")
                return ResolvePrefab(query, pathAsset, index);
            if (kind == "asset")
                return ResolveAsset(query, pathAsset);
            if (kind != "auto")
            {
                throw new VmProjectToolException("invalid_arguments",
                    $"Unsupported reference-trace kind '{kind}'.");
            }

            if (pathAsset is GamePrefabWrapper)
                return ResolveWrapper(query, pathAsset, index);
            if (pathAsset is GameObject)
                return ResolvePrefab(query, pathAsset, index);
            if (pathAsset != null)
                return ResolveAsset(query, pathAsset);
            if (index.GamePrefabsByID.TryGetValue(query,
                    out List<GamePrefabResolution> exactGamePrefabs))
            {
                return FromGamePrefabs(exactGamePrefabs);
            }

            string[] candidates = AssetDatabase.FindAssets(query,
                    new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Take(21)
                .ToArray();
            if (candidates.Length == 1)
            {
                Object found = AssetDatabase.LoadMainAssetAtPath(candidates[0]);
                if (found is GamePrefabWrapper)
                    return ResolveWrapper(candidates[0], found, index);
                if (found is GameObject)
                    return ResolvePrefab(candidates[0], found, index);
                return ResolveAsset(candidates[0], found);
            }

            throw new VmProjectToolException(
                candidates.Length == 0
                    ? "reference_trace_not_found"
                    : "reference_trace_ambiguous",
                candidates.Length == 0
                    ? $"No GamePrefab or asset matched '{query}'."
                    : $"Query '{query}' is ambiguous. First matches: " +
                      string.Join(", ", candidates.Take(20)));
        }

        private static TraceRoot ResolveGamePrefab(string query,
            TraceIndex index)
        {
            if (!index.GamePrefabsByID.TryGetValue(query,
                    out List<GamePrefabResolution> matches))
            {
                throw new VmProjectToolException("reference_trace_not_found",
                    $"No GamePrefab has the exact id '{query}'.");
            }
            if (matches.Count > 1)
            {
                throw new VmProjectToolException("reference_trace_ambiguous",
                    $"GamePrefab id '{query}' is declared by {matches.Count} wrappers.",
                    details: new Dictionary<string, object>
                    {
                        { "wrapperPaths", matches.Select(match =>
                            match.WrapperPath).ToList() },
                    });
            }
            return FromGamePrefabs(matches);
        }

        private static TraceRoot ResolveWrapper(string query, Object pathAsset,
            TraceIndex index)
        {
            GamePrefabWrapper wrapper = pathAsset as GamePrefabWrapper;
            if (wrapper == null)
            {
                List<GamePrefabWrapper> matches = index.Wrappers
                    .Where(value =>
                    {
                        string path = AssetDatabase.GetAssetPath(value);
                        return string.Equals(path, query,
                                   StringComparison.Ordinal) ||
                               string.Equals(value.name, query,
                                   StringComparison.Ordinal);
                    })
                    .ToList();
                if (matches.Count == 0)
                {
                    throw new VmProjectToolException(
                        "reference_trace_kind_mismatch",
                        $"'{query}' did not resolve to a GamePrefabWrapper.");
                }
                if (matches.Count > 1)
                {
                    throw new VmProjectToolException(
                        "reference_trace_ambiguous",
                        $"Wrapper query '{query}' matched {matches.Count} assets.");
                }
                wrapper = matches[0];
            }

            string wrapperPath = AssetDatabase.GetAssetPath(wrapper);
            return new TraceRoot
            {
                Wrappers = new List<GamePrefabWrapper> { wrapper },
                GamePrefabs = index.AllGamePrefabs
                    .Where(match => string.Equals(match.WrapperPath,
                        wrapperPath, StringComparison.Ordinal))
                    .ToList(),
            };
        }

        private static TraceRoot ResolvePrefab(string query, Object pathAsset,
            TraceIndex index)
        {
            GameObject prefab = pathAsset as GameObject;
            if (prefab == null)
            {
                string normalized = NormalizeAssetPath(query);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(normalized);
            }
            if (prefab == null)
            {
                throw new VmProjectToolException(
                    "reference_trace_kind_mismatch",
                    $"'{query}' did not resolve to a prefab GameObject asset.");
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            return new TraceRoot
            {
                Wrappers = index.AllGamePrefabs
                    .Where(match => string.Equals(
                        AssetDatabase.GetAssetPath(
                            GetPrefab(match.GamePrefab)), prefabPath,
                        StringComparison.Ordinal))
                    .Select(match => match.Wrapper)
                    .Where(value => value != null)
                    .Distinct()
                    .ToList(),
                GamePrefabs = index.AllGamePrefabs
                    .Where(match => string.Equals(
                        AssetDatabase.GetAssetPath(
                            GetPrefab(match.GamePrefab)), prefabPath,
                        StringComparison.Ordinal))
                    .ToList(),
                Prefabs = new List<GameObject> { prefab },
            };
        }

        private static TraceRoot ResolveAsset(string query, Object pathAsset)
        {
            Object asset = pathAsset;
            string path = NormalizeAssetPath(query);
            if (asset == null && path.StartsWith("Assets/",
                    StringComparison.Ordinal))
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);
            }
            if (asset == null)
            {
                throw new VmProjectToolException("reference_trace_not_found",
                    $"Asset '{query}' was not found.");
            }
            return new TraceRoot
            {
                Asset = asset,
                AssetPath = AssetDatabase.GetAssetPath(asset),
            };
        }

        private static TraceRoot FromGamePrefabs(
            IReadOnlyCollection<GamePrefabResolution> matches)
        {
            return new TraceRoot
            {
                Wrappers = matches.Select(match => match.Wrapper)
                    .Where(value => value != null)
                    .Distinct()
                    .ToList(),
                GamePrefabs = matches.ToList(),
            };
        }

        private static Dictionary<string, object> TracePrefab(GameObject prefab,
            string propertyName, int maxComponents,
            ICollection<Dictionary<string, object>> nodes,
            ICollection<Dictionary<string, object>> edges)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            Component[] allComponents =
                prefab.GetComponentsInChildren<Component>(true);
            var components = new List<Dictionary<string, object>>();
            int missingScripts = prefab.GetComponentsInChildren<Transform>(true)
                .Sum(transform => GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject));
            int totalComponents = 0;

            foreach (Component component in allComponents)
            {
                if (component == null)
                    continue;
                totalComponents++;
                if (components.Count >= maxComponents)
                    continue;

                string hierarchyPath = GetHierarchyPath(
                    component.transform, prefab.transform);
                string componentNode = ComponentNode(prefabPath,
                    hierarchyPath, component.GetType().FullName);
                var references = new List<Dictionary<string, object>>();
                var propertyMatches = new List<Dictionary<string, object>>();
                try
                {
                    var serializedObject = new SerializedObject(component);
                    SerializedProperty iterator =
                        serializedObject.GetIterator();
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (iterator.propertyType ==
                            SerializedPropertyType.ObjectReference &&
                            iterator.objectReferenceValue != null)
                        {
                            string referencePath = AssetDatabase.GetAssetPath(
                                iterator.objectReferenceValue);
                            if (!string.IsNullOrWhiteSpace(referencePath))
                            {
                                references.Add(
                                    new Dictionary<string, object>
                                    {
                                        { "property", iterator.propertyPath },
                                        { "assetPath", referencePath },
                                        { "type", iterator
                                            .objectReferenceValue.GetType()
                                            .FullName },
                                    });
                                AddEdge(edges, componentNode, "references",
                                    AssetNode(referencePath));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(propertyName) &&
                            (string.Equals(iterator.name, propertyName,
                                 StringComparison.OrdinalIgnoreCase) ||
                             iterator.propertyPath.IndexOf(propertyName,
                                 StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            propertyMatches.Add(
                                new Dictionary<string, object>
                                {
                                    { "path", iterator.propertyPath },
                                    { "type", iterator.propertyType.ToString() },
                                    { "value", GetSerializedPropertyValue(iterator) },
                                });
                        }
                    }
                }
                catch (Exception exception)
                {
                    references.Add(new Dictionary<string, object>
                    {
                        { "scanError", exception.GetType().Name + ": " +
                                       exception.Message },
                    });
                }

                components.Add(new Dictionary<string, object>
                {
                    { "gameObjectPath", hierarchyPath },
                    { "type", component.GetType().FullName },
                    { "enabled", GetEnabled(component) },
                    { "assetReferences", references },
                    { "propertyMatches", propertyMatches },
                });
                AddNode(nodes, componentNode, prefabPath, hierarchyPath,
                    component.GetType().FullName);
                AddEdge(edges, PrefabNode(prefabPath), "has-component",
                    componentNode);
            }

            return new Dictionary<string, object>
            {
                { "path", prefabPath },
                { "name", prefab.name },
                { "prefabAssetType", PrefabUtility
                    .GetPrefabAssetType(prefab).ToString() },
                { "variantSourcePath", AssetDatabase.GetAssetPath(
                    PrefabUtility.GetCorrespondingObjectFromSource(prefab)) ?? "" },
                { "gameObjectCount", prefab
                    .GetComponentsInChildren<Transform>(true).Length },
                { "componentCount", totalComponents },
                { "returnedComponentCount", components.Count },
                { "componentsTruncated", totalComponents > components.Count },
                { "missingScriptCount", missingScripts },
                { "components", components },
            };
        }

        private static Dictionary<string, object> ResolveTag(string tag)
        {
            var args = new Dictionary<string, object>
            {
                { "id", tag },
                { "includeLocalizations", true },
                { "limit", 2 },
            };
            Dictionary<string, object> result =
                VMFrameworkPipelineGameTagTools.ListGameTags(args) as
                    Dictionary<string, object>;
            if (result != null &&
                result.TryGetValue("tags", out object tagsValue) &&
                tagsValue is IEnumerable enumerable)
            {
                foreach (object value in enumerable)
                {
                    Dictionary<string, object> candidate =
                        ToDictionary(value);
                    if (string.Equals(GetNestedString(candidate, "id"), tag,
                            StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }
            return new Dictionary<string, object>
            {
                { "id", tag },
                { "missing", true },
            };
        }

        private static void AddLocalization(
            ICollection<Dictionary<string, object>> localizations,
            ICollection<Dictionary<string, object>> nodes,
            ICollection<Dictionary<string, object>> edges,
            string ownerNode, string field, LocalizedString reference)
        {
            Dictionary<string, object> description =
                DescribeLocalizedString(reference);
            description["field"] = field;
            if (localizations.All(existing =>
                    !string.Equals(GetNestedString(existing, "field"),
                        field, StringComparison.Ordinal) ||
                    !string.Equals(GetNestedString(existing, "table"),
                        GetNestedString(description, "table"),
                        StringComparison.Ordinal) ||
                    !string.Equals(GetNestedString(existing, "key"),
                        GetNestedString(description, "key"),
                        StringComparison.Ordinal)))
            {
                localizations.Add(description);
            }
            string table = GetNestedString(description, "table");
            string key = GetNestedString(description, "key");
            string node = LocalizationNode(table, key);
            AddNode(nodes, node, "", table + "/" + key,
                typeof(LocalizedString).FullName);
            AddEdge(edges, ownerNode, "localized-" + field, node);
        }

        private static Dictionary<string, object> DescribeLocalizedString(
            LocalizedString reference)
        {
            if (reference == null)
            {
                return new Dictionary<string, object>
                {
                    { "table", "" },
                    { "key", "" },
                };
            }
            string table = reference.TableReference.TableCollectionName;
            if (string.IsNullOrWhiteSpace(table))
            {
                table = reference.TableReference.TableCollectionNameGuid
                    .ToString();
            }
            string key = reference.TableEntryReference.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = reference.TableEntryReference.KeyId
                    .ToString(CultureInfo.InvariantCulture);
            }
            return new Dictionary<string, object>
            {
                { "table", table ?? "" },
                { "key", key ?? "" },
            };
        }

        private static List<string> GetReverseReferenceCandidates(
            IReadOnlyCollection<string> targetPaths)
        {
            var targetSet = new HashSet<string>(
                targetPaths, StringComparer.Ordinal);
            return AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/",
                                   StringComparison.Ordinal) &&
                               !targetSet.Contains(path) &&
                               ReverseReferenceExtensions.Contains(
                                   Path.GetExtension(path),
                                   StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<string, object> BuildReverseReferenceResult(
            bool scanned, int scannedAssetCount,
            IEnumerable<string> targetPaths, int total,
            IReadOnlyCollection<Dictionary<string, object>> assets)
        {
            return new Dictionary<string, object>
            {
                { "scanned", scanned },
                { "scannedAssetCount", scannedAssetCount },
                { "targetPaths", targetPaths.OrderBy(path => path,
                    StringComparer.Ordinal).ToList() },
                { "total", total },
                { "returned", assets.Count },
                { "truncated", total > assets.Count },
                { "assets", assets.ToList() },
            };
        }

        private static Dictionary<string, object> DescribeRoot(TraceRoot root,
            IEnumerable<string> prefabPaths)
        {
            return new Dictionary<string, object>
            {
                { "wrapperPaths", root.Wrappers
                    .Select(AssetDatabase.GetAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList() },
                { "gamePrefabs", root.GamePrefabs
                    .OrderBy(match => match.GamePrefab.id,
                        StringComparer.Ordinal)
                    .ThenBy(match => match.WrapperPath,
                        StringComparer.Ordinal)
                    .Select(match => new Dictionary<string, object>
                    {
                        { "id", match.GamePrefab.id },
                        { "type", match.GamePrefab.GetType().FullName },
                        { "wrapperPath", match.WrapperPath },
                    }).ToList() },
                { "prefabPaths", prefabPaths
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList() },
                { "assetPath", root.AssetPath ?? "" },
                { "assetType", root.Asset?.GetType().FullName ?? "" },
            };
        }

        private static GameObject GetPrefab(IGamePrefab gamePrefab)
        {
            if (gamePrefab == null)
                return null;
            for (Type type = gamePrefab.GetType();
                 type != null;
                 type = type.BaseType)
            {
                FieldInfo field = type.GetField("prefab",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field?.GetValue(gamePrefab) is GameObject fieldPrefab)
                    return fieldPrefab;

                PropertyInfo property = type.GetProperty("Prefab",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (property?.GetValue(gamePrefab) is GameObject propertyPrefab)
                    return propertyPrefab;
            }
            return null;
        }

        private static object GetSerializedPropertyValue(
            SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Integer => property.longValue,
                SerializedPropertyType.Boolean => property.boolValue,
                SerializedPropertyType.Float => property.doubleValue,
                SerializedPropertyType.Enum =>
                    property.enumDisplayNames.Length >
                    property.enumValueIndex &&
                    property.enumValueIndex >= 0
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex,
                SerializedPropertyType.ObjectReference =>
                    AssetDatabase.GetAssetPath(
                        property.objectReferenceValue) ?? "",
                _ => property.propertyType.ToString(),
            };
        }

        private static object GetEnabled(Component component)
        {
            return component switch
            {
                Behaviour behaviour => behaviour.enabled,
                Renderer renderer => renderer.enabled,
                Collider collider => collider.enabled,
                _ => null,
            };
        }

        private static string GetHierarchyPath(Transform transform,
            Transform root)
        {
            var segments = new List<string>();
            for (Transform current = transform;
                 current != null;
                 current = current.parent)
            {
                segments.Add(current.name);
                if (current == root)
                    break;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string NormalizeAssetPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            string path = value.Replace('\\', '/');
            string projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."))
                .Replace('\\', '/')
                .TrimEnd('/');
            if (Path.IsPathRooted(value))
            {
                string fullPath = Path.GetFullPath(value)
                    .Replace('\\', '/');
                if (fullPath.StartsWith(projectRoot + "/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(projectRoot.Length + 1);
                }
            }
            return path;
        }

        private static string GetRequiredString(
            IReadOnlyDictionary<string, object> args, string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new VmProjectToolException("invalid_arguments",
                    $"{key} is required.");
            }
            return value;
        }

        private static string GetString(
            IReadOnlyDictionary<string, object> args, string key,
            string fallback = "")
        {
            return args != null &&
                   args.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : fallback;
        }

        private static bool GetBool(
            IReadOnlyDictionary<string, object> args, string key,
            bool fallback)
        {
            return args != null &&
                   args.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                : fallback;
        }

        private static int GetInt(
            IReadOnlyDictionary<string, object> args, string key,
            int fallback)
        {
            return args != null &&
                   args.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : fallback;
        }

        private static Dictionary<string, object> ToDictionary(object value)
        {
            if (value is Dictionary<string, object> dictionary)
                return dictionary;
            if (value is IReadOnlyDictionary<string, object> readOnly)
                return readOnly.ToDictionary(pair => pair.Key,
                    pair => pair.Value, StringComparer.Ordinal);
            if (value is IDictionary legacy)
            {
                var result = new Dictionary<string, object>(
                    StringComparer.Ordinal);
                foreach (DictionaryEntry entry in legacy)
                {
                    if (entry.Key != null)
                        result[entry.Key.ToString()] = entry.Value;
                }
                return result;
            }
            throw new VmProjectToolException("project_tool_state_invalid",
                "Persistent reference-trace state contained a non-object value.");
        }

        private static List<Dictionary<string, object>> ToDictionaryList(
            object value)
        {
            return value is IEnumerable enumerable
                ? enumerable.Cast<object>().Select(ToDictionary).ToList()
                : new List<Dictionary<string, object>>();
        }

        private static List<string> ToStringList(object value)
        {
            return value is IEnumerable enumerable && value is not string
                ? enumerable.Cast<object>()
                    .Where(item => item != null)
                    .Select(item => item.ToString())
                    .ToList()
                : new List<string>();
        }

        private static string GetNestedString(
            IReadOnlyDictionary<string, object> dictionary, string key)
        {
            return dictionary != null &&
                   dictionary.TryGetValue(key, out object value)
                ? value?.ToString() ?? ""
                : "";
        }

        private static void AddNode(
            ICollection<Dictionary<string, object>> nodes,
            string id, string assetPath, string name, string type)
        {
            if (nodes.Any(node => string.Equals(
                    GetNestedString(node, "id"), id,
                    StringComparison.Ordinal)))
            {
                return;
            }
            nodes.Add(new Dictionary<string, object>
            {
                { "id", id },
                { "name", name ?? "" },
                { "type", type ?? "" },
                { "assetPath", assetPath ?? "" },
            });
        }

        private static void AddEdge(
            ICollection<Dictionary<string, object>> edges,
            string from, string relation, string to)
        {
            if (edges.Any(edge =>
                    string.Equals(GetNestedString(edge, "from"), from,
                        StringComparison.Ordinal) &&
                    string.Equals(GetNestedString(edge, "relation"), relation,
                        StringComparison.Ordinal) &&
                    string.Equals(GetNestedString(edge, "to"), to,
                        StringComparison.Ordinal)))
            {
                return;
            }
            edges.Add(new Dictionary<string, object>
            {
                { "from", from },
                { "relation", relation },
                { "to", to },
            });
        }

        private static string NodeForAssetPath(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) is GamePrefabWrapper)
                return WrapperNode(path);
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".prefab",
                    StringComparison.OrdinalIgnoreCase))
                return PrefabNode(path);
            return AssetNode(path);
        }

        private static string WrapperNode(string path) => "wrapper:" + path;
        private static string GamePrefabNode(string id) => "game-prefab:" + id;
        private static string PrefabNode(string path) => "prefab:" + path;
        private static string AssetNode(string path) => "asset:" + path;
        private static string TagNode(string id) => "tag:" + id;
        private static string LocalizationNode(string table, string key) =>
            "localization:" + table + "/" + key;
        private static string ComponentNode(string prefabPath,
            string hierarchyPath, string type) =>
            "component:" + prefabPath + ":" + hierarchyPath + "/" + type;

        private sealed class TraceRoot
        {
            internal List<GamePrefabWrapper> Wrappers = new();
            internal List<GamePrefabResolution> GamePrefabs = new();
            internal List<GameObject> Prefabs = new();
            internal Object Asset;
            internal string AssetPath;
        }

        private sealed class GamePrefabResolution
        {
            internal GamePrefabWrapper Wrapper;
            internal string WrapperPath;
            internal IGamePrefab GamePrefab;
        }

        private sealed class TraceIndex
        {
            internal List<GamePrefabWrapper> Wrappers { get; } = new();
            internal List<GamePrefabResolution> AllGamePrefabs { get; } = new();
            internal Dictionary<string, List<GamePrefabResolution>>
                GamePrefabsByID { get; } =
                    new(StringComparer.Ordinal);

            internal static TraceIndex Build()
            {
                var index = new TraceIndex();
                IEnumerable<GamePrefabWrapper> wrappers;
                try
                {
                    wrappers = GamePrefabWrapperQueryTools
                        .GetAllGamePrefabWrappers()
                        .Where(wrapper => wrapper != null)
                        .OrderBy(AssetDatabase.GetAssetPath,
                            StringComparer.Ordinal)
                        .ToList();
                }
                catch (Exception exception)
                {
                    throw new VmProjectToolException(
                        "reference_trace_not_found",
                        "GamePrefab wrappers could not be enumerated: " +
                        exception.Message);
                }

                foreach (GamePrefabWrapper wrapper in wrappers)
                {
                    index.Wrappers.Add(wrapper);
                    string path = AssetDatabase.GetAssetPath(wrapper);
                    var gamePrefabs = new List<IGamePrefab>();
                    wrapper.GetGamePrefabs(gamePrefabs);
                    foreach (IGamePrefab gamePrefab in gamePrefabs
                                 .Where(value => value != null))
                    {
                        var resolution = new GamePrefabResolution
                        {
                            Wrapper = wrapper,
                            WrapperPath = path,
                            GamePrefab = gamePrefab,
                        };
                        index.AllGamePrefabs.Add(resolution);
                        if (!index.GamePrefabsByID.TryGetValue(gamePrefab.id,
                                out List<GamePrefabResolution> matches))
                        {
                            matches = new List<GamePrefabResolution>();
                            index.GamePrefabsByID.Add(gamePrefab.id, matches);
                        }
                        matches.Add(resolution);
                    }
                }
                return index;
            }
        }
    }
}
#endif
