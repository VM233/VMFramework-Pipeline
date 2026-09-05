using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkSerializationSnapshotRequest
    {
        [VmRequired, VmJsonProperty("assetPaths")]
        [Description("One to 32 exact .asset paths to capture or migrate.")]
        public List<string> AssetPaths { get; set; }

        [VmRequired, VmJsonProperty("snapshotDirectory")]
        [Description("Project-relative snapshot directory beneath Temp.")]
        public string SnapshotDirectory { get; set; }
    }
}
