using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkFindGamePrefabRequest
    {
        [VmJsonProperty("id")]
        [VmMinLength(1)]
        [Description("Optional exact GamePrefab id.")]
        public string Id { get; set; }

        [VmJsonProperty("filter")]
        [VmMinLength(1)]
        [Description("Optional case-insensitive text matched against GamePrefab identity and type.")]
        public string Filter { get; set; }

        [VmJsonProperty("gamePrefabType")]
        [VmMinLength(1)]
        [Description("Optional GamePrefab type name, full name, or assembly-qualified name.")]
        public string GamePrefabType { get; set; }

        [VmJsonProperty("offset")]
        [VmRange(0, int.MaxValue)]
        [Description("Zero-based result offset.")]
        public int Offset { get; set; }

        [VmJsonProperty("limit")]
        [VmRange(1, 5000)]
        [VmDefaultSource("Preferences > VM Unity Automation > Tool Responses")]
        [Description("Maximum returned references.")]
        public int? Limit { get; set; }
    }
}
