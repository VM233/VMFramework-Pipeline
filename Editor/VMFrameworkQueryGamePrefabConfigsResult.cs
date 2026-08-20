using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkQueryGamePrefabConfigsResult
    {
        [VmRequired]
        [VmJsonProperty("gamePrefabs")]
        [Description("Matching GamePrefab config records for this page.")]
        public List<VMFrameworkGamePrefabConfigRecord> GamePrefabs { get; set; }

        [VmRequired]
        [VmJsonProperty("count")]
        [Description("Number of config records returned on this page.")]
        public int Count { get; set; }

        [VmRequired]
        [VmJsonProperty("total")]
        [Description("Total number of matching GamePrefab configs.")]
        public int Total { get; set; }

        [VmRequired]
        [VmJsonProperty("offset")]
        [Description("Zero-based offset of this page.")]
        public int Offset { get; set; }

        [VmRequired]
        [VmJsonProperty("limit")]
        [Description("Maximum number of config records requested for this page.")]
        public int Limit { get; set; }

        [VmJsonProperty("nextOffset")]
        [Description("Offset for the next page when more matches exist.")]
        public int? NextOffset { get; set; }
    }
}
