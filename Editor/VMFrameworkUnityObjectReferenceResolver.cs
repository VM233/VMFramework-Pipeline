using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor
{
    internal static class VMFrameworkUnityObjectReferenceResolver
    {
        internal static Object Resolve(
            object value,
            Type targetType,
            string memberPath)
        {
            if (!typeof(Object).IsAssignableFrom(targetType))
            {
                throw new ArgumentException(
                    $"'{targetType.FullName}' is not a Unity Object type.",
                    nameof(targetType));
            }

            var reference = value as Dictionary<string, object>;
            var assetPath = value is string stringPath
                ? stringPath
                : ReadString(reference, "assetPath");
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new InvalidOperationException(
                    $"Unity Object '{memberPath}' requires an asset path " +
                    "string or {assetPath} descriptor.");
            }

            ValidateGuid(reference, assetPath, memberPath);
            if (TryReadFileId(reference, memberPath, out var fileId))
            {
                return ResolveExact(
                    assetPath,
                    fileId,
                    targetType,
                    memberPath);
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != null && targetType.IsInstanceOfType(mainAsset))
            {
                return mainAsset;
            }

            var candidates = CompatibleAssets(assetPath, targetType);
            if (candidates.Length == 1) return candidates[0];
            if (candidates.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Unity Object '{memberPath}' is ambiguous at " +
                    $"'{assetPath}'; provide its guid and fileID descriptor.");
            }

            throw new InvalidOperationException(
                $"No asset at '{assetPath}' is assignable to " +
                $"'{targetType.FullName}'.");
        }

        private static Object ResolveExact(
            string assetPath,
            long fileId,
            Type targetType,
            string memberPath)
        {
            var candidates = CompatibleAssets(assetPath, targetType)
                .Where(candidate => HasFileId(candidate, fileId))
                .ToArray();
            if (candidates.Length == 1) return candidates[0];
            if (candidates.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Unity Object '{memberPath}' resolved fileID {fileId} " +
                    $"more than once at '{assetPath}'.");
            }

            throw new InvalidOperationException(
                $"No '{targetType.FullName}' with fileID {fileId} exists at " +
                $"'{assetPath}'.");
        }

        private static Object[] CompatibleAssets(
            string assetPath,
            Type targetType)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Where(targetType.IsInstanceOfType)
                .ToArray();
        }

        private static bool HasFileId(Object candidate, long expectedFileId)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                       candidate,
                       out _,
                       out long actualFileId) &&
                   actualFileId == expectedFileId;
        }

        private static void ValidateGuid(
            IReadOnlyDictionary<string, object> reference,
            string assetPath,
            string memberPath)
        {
            var expectedGuid = ReadString(reference, "guid");
            if (string.IsNullOrWhiteSpace(expectedGuid)) return;
            var actualGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.Equals(
                    expectedGuid,
                    actualGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Unity Object '{memberPath}' expected guid " +
                    $"'{expectedGuid}', but '{assetPath}' has guid " +
                    $"'{actualGuid}'.");
            }
        }

        private static bool TryReadFileId(
            IReadOnlyDictionary<string, object> reference,
            string memberPath,
            out long fileId)
        {
            fileId = 0;
            if (reference == null ||
                !reference.TryGetValue("fileID", out var rawFileId) ||
                rawFileId == null)
            {
                return false;
            }

            if (!long.TryParse(
                    Convert.ToString(rawFileId, CultureInfo.InvariantCulture),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out fileId) ||
                fileId == 0)
            {
                throw new InvalidOperationException(
                    $"Unity Object '{memberPath}' has invalid fileID " +
                    $"'{rawFileId}'.");
            }
            return true;
        }

        private static string ReadString(
            IReadOnlyDictionary<string, object> values,
            string key)
        {
            return values != null && values.TryGetValue(key, out var value)
                ? value?.ToString()
                : null;
        }
    }
}
