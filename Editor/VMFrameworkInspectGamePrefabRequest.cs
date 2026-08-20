using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkInspectGamePrefabRequest
    {
        [VmRequired]
        [VmJsonProperty("gamePrefab")]
        [Description("Authoritative GamePrefab reference produced by a VMFramework tool.")]
        public VMFrameworkGamePrefabReference GamePrefab { get; set; }

        [VmJsonProperty("maxDepth")]
        [VmRange(1, 16)]
        [VmDefaultSource("Preferences > VMFramework Pipeline > GamePrefab Inspection")]
        [Description("Maximum nested serialized-value depth.")]
        public int? MaxDepth { get; set; }

        [VmJsonProperty("maxCollectionItems")]
        [VmRange(1, 1000)]
        [VmDefaultSource("Preferences > VMFramework Pipeline > GamePrefab Inspection")]
        [Description("Maximum retained items per nested collection.")]
        public int? MaxCollectionItems { get; set; }
    }
}
