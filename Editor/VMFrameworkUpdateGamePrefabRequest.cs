using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkUpdateGamePrefabRequest
    {
        [VmRequired]
        [VmJsonProperty("gamePrefab")]
        [Description("Authoritative GamePrefab reference produced by a VMFramework tool.")]
        public VMFrameworkGamePrefabReference GamePrefab { get; set; }

        [VmRequired]
        [VmJsonProperty("operations")]
        [VmMinItems(1)]
        [Description("Ordered operations committed atomically.")]
        public List<VMFrameworkGamePrefabUpdateOperation> Operations { get; set; }

        [VmJsonProperty("maxDepth")]
        [VmRange(1, 16)]
        [VmDefaultSource("Preferences > VMFramework Pipeline > GamePrefab Inspection")]
        [Description("Semantic-diff and optional snapshot depth.")]
        public int? MaxDepth { get; set; }

        [VmJsonProperty("maxCollectionItems")]
        [VmRange(1, 1000)]
        [VmDefaultSource("Preferences > VMFramework Pipeline > GamePrefab Inspection")]
        [Description("Items retained per collection in the semantic diff and snapshots.")]
        public int? MaxCollectionItems { get; set; }

        [VmJsonProperty("includeSnapshots")]
        [VmDefaultSource("Preferences > VMFramework Pipeline > GamePrefab Inspection")]
        [Description("Include before and after serialized snapshots.")]
        public bool? IncludeSnapshots { get; set; }
    }
}
