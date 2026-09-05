using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VMUnityAutomation.Editor;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkSerializationSnapshotTool
    {
        [VmProjectTool("vmframework/capture-serialization-snapshots",
            Description = "Capture complete authoring graphs before a serialization schema migration, preserving types, shared values, Unity asset references, and source hashes. Processes at most 32 explicit .asset paths.",
            MutatesProjectFiles = true, ErrorCodes = new[] { "tool_execution_failed" })]
        public static VMFrameworkSerializationSnapshotResult Capture(VMFrameworkSerializationSnapshotRequest request)
        {
            string directory = Validate(request);
            Directory.CreateDirectory(directory);
            var result = new VMFrameworkSerializationSnapshotResult();
            foreach (string path in request.AssetPaths)
            {
                string snapshotPath = SnapshotPath(directory, path);
                if (File.Exists(snapshotPath)) throw new IOException($"Snapshot already exists: {snapshotPath}.");
                Object asset = Load(path);
                var snapshot = new JObject { ["assetPath"] = path,
                    ["sourceHash"] = Hash(File.ReadAllBytes(path)),
                    ["graph"] = new VMFrameworkSerializationGraph().Capture(asset) };
                File.WriteAllText(snapshotPath, snapshot.ToString(Formatting.Indented));
                result.SnapshotFiles.Add(snapshotPath);
                result.AssetCount++;
            }
            return result;
        }

        [VmProjectTool("vmframework/apply-serialization-snapshots",
            Description = "Apply captured authoring graphs to their current Unity serialization schema, then save, unload, reload, and compare every field and Unity reference. Each asset rolls back its bytes if readback differs.",
            MutatesAssets = true, ErrorCodes = new[] { "serialization_source_changed", "serialization_migration_failed" },
            TransactionScope = "one-asset-at-a-time",
            TransactionAtomicity = "verified-single-asset-rollback",
            TransactionIsolation = "source-content-hash",
            TransactionDurability = "disk",
            TransactionRollbackKind = "byte-snapshot",
            TransactionCommitEvidence = new[] { "complete-authoring-graph-readback", "no-odin-payload" })]
        public static VMFrameworkSerializationSnapshotResult Apply(VMFrameworkSerializationSnapshotRequest request)
        {
            string directory = Validate(request);
            var result = new VMFrameworkSerializationSnapshotResult();
            foreach (string path in request.AssetPaths)
            {
                string snapshotPath = SnapshotPath(directory, path);
                JObject snapshot = JObject.Parse(File.ReadAllText(snapshotPath));
                byte[] before = File.ReadAllBytes(path);
                if ((string)snapshot["assetPath"] != path || (string)snapshot["sourceHash"] != Hash(before))
                {
                    throw new VmProjectToolException("serialization_source_changed", $"Source changed since capture: {path}.");
                }
                Object asset = Load(path);
                try
                {
                    new VMFrameworkSerializationGraph().Restore(asset, (JObject)snapshot["graph"]);
                    JObject expected = new VMFrameworkSerializationGraph().Capture(asset);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    Resources.UnloadAsset(asset);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    Object reloaded = Load(path);
                    JObject actual = new VMFrameworkSerializationGraph().Capture(reloaded);
                    if (!JToken.DeepEquals(expected, actual))
                    {
                        File.WriteAllText(snapshotPath + ".expected.json", expected.ToString());
                        File.WriteAllText(snapshotPath + ".actual.json", actual.ToString());
                        throw new InvalidOperationException($"Native serialized graph differs after reload: {path}.");
                    }
                    if (File.ReadAllText(path).Contains("serializationData:"))
                    {
                        throw new InvalidOperationException($"The asset still owns an Odin payload: {path}.");
                    }
                    File.WriteAllText(snapshotPath + ".verified.json", actual.ToString());
                    result.VerifiedAssetPaths.Add(path);
                    result.SnapshotFiles.Add(snapshotPath);
                    result.AssetCount++;
                }
                catch (Exception exception)
                {
                    File.WriteAllBytes(path, before);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    throw new VmProjectToolException("serialization_migration_failed", exception.ToString(),
                        details: new Dictionary<string, object> { ["assetPath"] = path,
                            ["verifiedAssetPaths"] = result.VerifiedAssetPaths, ["snapshotPath"] = snapshotPath });
                }
            }
            return result;
        }

        private static string Validate(VMFrameworkSerializationSnapshotRequest request)
        {
            if (request.AssetPaths == null || request.AssetPaths.Count < 1 || request.AssetPaths.Count > 32 ||
                request.AssetPaths.Distinct(StringComparer.Ordinal).Count() != request.AssetPaths.Count)
            {
                throw new ArgumentException("Provide one to 32 distinct asset paths.");
            }
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.GetFullPath(Path.Combine(project, request.SnapshotDirectory));
            string allowedRoot = Path.GetFullPath(Path.Combine(project, "Temp")) + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Snapshots must be stored beneath the project's Temp directory.");
            }
            foreach (string path in request.AssetPaths)
            {
                string absolute = Path.GetFullPath(path);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") ||
                    !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                    !absolute.StartsWith(Application.dataPath.Replace('/', Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    new FileInfo(path).Length > 2 * 1024 * 1024)
                {
                    throw new ArgumentException($"Unsupported snapshot asset path or size: {path}.");
                }
            }
            return directory;
        }

        private static Object Load(string path)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!(asset is ScriptableObject) || asset.GetType().Assembly.GetName().Name.StartsWith("Sirenix.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Expected a project-owned ScriptableObject asset: {path}.");
            }
            return asset;
        }

        private static string SnapshotPath(string directory, string path) =>
            Path.Combine(directory, AssetDatabase.AssetPathToGUID(path) + ".json");

        private static string Hash(byte[] bytes)
        {
            using SHA256 algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
        }
    }
}
