#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace VMFramework.Pipeline.Editor
{
    /// <summary>
    /// Publishes complete wrapper snapshots through same-directory atomic replacement and proves
    /// byte-for-byte restoration. Temporary files are always private and deleted in finally.
    /// </summary>
    internal static class VMFrameworkPipelineAssetSnapshotStore
    {
        internal static Func<string, Exception> FaultInjector;

        internal static Snapshot Capture(string assetPath)
        {
            string absolutePath = Path.GetFullPath(assetPath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Wrapper asset file was not found.", absolutePath);
            string metaPath = absolutePath + ".meta";
            byte[] assetBytes = File.ReadAllBytes(absolutePath);
            byte[] metaBytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null;
            return new Snapshot(assetPath, absolutePath, assetBytes, metaBytes,
                Hash(assetBytes), metaBytes == null ? "" : Hash(metaBytes));
        }

        internal static List<string> RestoreAndVerify(Snapshot snapshot)
        {
            var errors = new List<string>();
            try
            {
                ThrowInjected("before-asset-restore");
                AtomicWrite(snapshot.AbsolutePath, snapshot.AssetBytes);
                ThrowInjected("after-asset-restore");
            }
            catch (Exception exception)
            {
                errors.Add("Wrapper restore failed: " + exception.GetBaseException().Message);
            }

            try
            {
                string metaPath = snapshot.AbsolutePath + ".meta";
                if (snapshot.MetaBytes != null)
                    AtomicWrite(metaPath, snapshot.MetaBytes);
                else if (File.Exists(metaPath))
                    File.Delete(metaPath);
            }
            catch (Exception exception)
            {
                errors.Add("Wrapper meta restore failed: " + exception.GetBaseException().Message);
            }

            try
            {
                if (!File.Exists(snapshot.AbsolutePath) ||
                    !string.Equals(Hash(File.ReadAllBytes(snapshot.AbsolutePath)),
                        snapshot.AssetSha256, StringComparison.OrdinalIgnoreCase))
                    errors.Add("Wrapper byte readback does not match the prepared snapshot.");
                string metaPath = snapshot.AbsolutePath + ".meta";
                bool metaExists = File.Exists(metaPath);
                if (metaExists != (snapshot.MetaBytes != null))
                    errors.Add("Wrapper meta existence does not match the prepared snapshot.");
                else if (metaExists && !string.Equals(Hash(File.ReadAllBytes(metaPath)),
                             snapshot.MetaSha256, StringComparison.OrdinalIgnoreCase))
                    errors.Add("Wrapper meta byte readback does not match the prepared snapshot.");
                ThrowInjected("after-byte-readback");
            }
            catch (Exception exception)
            {
                errors.Add("Wrapper readback failed: " + exception.GetBaseException().Message);
            }
            return errors;
        }

        private static void AtomicWrite(string path, byte[] bytes)
        {
            string absolutePath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = absolutePath + ".vmframework-pipeline-" +
                                   Guid.NewGuid().ToString("N") + ".tmp";
            Exception failure = null;
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                           FileAccess.Write, FileShare.None))
                {
                    byte[] snapshot = bytes ?? Array.Empty<byte>();
                    stream.Write(snapshot, 0, snapshot.Length);
                    stream.Flush(true);
                }
                if (File.Exists(absolutePath))
                    File.Replace(temporaryPath, absolutePath, null, true);
                else
                    File.Move(temporaryPath, absolutePath);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                failure = failure == null
                    ? cleanupException
                    : new AggregateException(failure, cleanupException);
            }
            if (failure != null)
                throw failure;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes ?? Array.Empty<byte>()))
                .Replace("-", "").ToLowerInvariant();
        }

        private static void ThrowInjected(string boundary)
        {
            Exception exception = FaultInjector?.Invoke(boundary);
            if (exception != null) throw exception;
        }

        internal sealed class Snapshot
        {
            internal string AssetPath { get; }
            internal string AbsolutePath { get; }
            internal byte[] AssetBytes { get; }
            internal byte[] MetaBytes { get; }
            internal string AssetSha256 { get; }
            internal string MetaSha256 { get; }

            internal Snapshot(string assetPath, string absolutePath, byte[] assetBytes,
                byte[] metaBytes, string assetSha256, string metaSha256)
            {
                AssetPath = assetPath;
                AbsolutePath = absolutePath;
                AssetBytes = assetBytes;
                MetaBytes = metaBytes;
                AssetSha256 = assetSha256;
                MetaSha256 = metaSha256;
            }
        }
    }
}
#endif
