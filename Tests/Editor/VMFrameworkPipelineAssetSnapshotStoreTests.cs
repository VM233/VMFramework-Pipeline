#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class VMFrameworkPipelineAssetSnapshotStoreTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(),
                "VMFrameworkPipelineSnapshotTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            VMFrameworkPipelineAssetSnapshotStore.FaultInjector = null;
        }

        [TearDown]
        public void TearDown()
        {
            VMFrameworkPipelineAssetSnapshotStore.FaultInjector = null;
            string resolved = Path.GetFullPath(testDirectory ?? "");
            string allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
                "VMFrameworkPipelineSnapshotTests"));
            if (resolved.StartsWith(allowedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
                Directory.Delete(resolved, true);
        }

        [Test]
        public void RestoreAndVerify_AtomicallyRestoresAssetAndMetaBytes()
        {
            string path = Path.Combine(testDirectory, "wrapper.asset");
            File.WriteAllText(path, "asset baseline");
            File.WriteAllText(path + ".meta", "meta baseline");
            VMFrameworkPipelineAssetSnapshotStore.Snapshot snapshot =
                VMFrameworkPipelineAssetSnapshotStore.Capture(path);
            File.WriteAllText(path, "mutated asset");
            File.WriteAllText(path + ".meta", "mutated meta");

            var errors = VMFrameworkPipelineAssetSnapshotStore.RestoreAndVerify(snapshot);

            Assert.That(errors, Is.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo("asset baseline"));
            Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo("meta baseline"));
        }

        [Test]
        public void RestoreFailure_IsReportedSeparatelyFromReadback()
        {
            string path = Path.Combine(testDirectory, "wrapper.asset");
            File.WriteAllText(path, "asset baseline");
            VMFrameworkPipelineAssetSnapshotStore.Snapshot snapshot =
                VMFrameworkPipelineAssetSnapshotStore.Capture(path);
            File.WriteAllText(path, "mutated asset");
            VMFrameworkPipelineAssetSnapshotStore.FaultInjector = boundary =>
                boundary == "before-asset-restore"
                    ? new IOException("injected atomic restore failure")
                    : null;

            var errors = VMFrameworkPipelineAssetSnapshotStore.RestoreAndVerify(snapshot);

            Assert.That(errors, Is.Not.Empty);
            Assert.That(string.Join("\n", errors), Does.Contain("injected atomic restore failure"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("mutated asset"));
        }
    }
}
#endif
