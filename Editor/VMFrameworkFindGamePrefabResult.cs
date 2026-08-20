using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkFindGamePrefabResult
    {
        [VmRequired]
        [VmJsonProperty("gamePrefabs")]
        [Description("Nominal references for the matching GamePrefabs.")]
        public List<VMFrameworkGamePrefabReference> GamePrefabs { get; set; }

        [VmRequired]
        [VmJsonProperty("count")]
        [Description("Number of references returned on this page.")]
        public int Count { get; set; }

        [VmRequired]
        [VmJsonProperty("total")]
        [Description("Total number of matching GamePrefabs.")]
        public int Total { get; set; }

        [VmRequired]
        [VmJsonProperty("offset")]
        [Description("Zero-based offset of this page.")]
        public int Offset { get; set; }

        [VmRequired]
        [VmJsonProperty("limit")]
        [Description("Maximum number of references requested for this page.")]
        public int Limit { get; set; }

        [VmJsonProperty("nextOffset")]
        [Description("Offset for the next page when more matches exist.")]
        public int? NextOffset { get; set; }
    }
}
