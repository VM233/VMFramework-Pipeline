using System.Collections.Generic;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkSerializationSnapshotResult
    {
        [VmRequired, VmJsonProperty("assetCount")]
        public int AssetCount { get; set; }

        [VmRequired, VmJsonProperty("snapshotFiles")]
        public List<string> SnapshotFiles { get; set; } = new();

        [VmRequired, VmJsonProperty("verifiedAssetPaths")]
        public List<string> VerifiedAssetPaths { get; set; } = new();
    }
}
