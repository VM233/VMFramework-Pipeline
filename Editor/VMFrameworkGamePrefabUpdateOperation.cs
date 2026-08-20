using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkGamePrefabUpdateOperation
    {
        [VmRequired]
        [VmJsonProperty("type")]
        [Description("Atomic operation kind.")]
        public VMFrameworkGamePrefabUpdateOperationKind Type { get; set; }

        [VmRequired]
        [VmJsonProperty("path")]
        [VmMinLength(1)]
        [Description("Member path with optional collection index segments.")]
        public string Path { get; set; }

        [VmJsonProperty("value")]
        [Description("Value used by set, append, or insert.")]
        public object Value { get; set; }

        [VmJsonProperty("index")]
        [Description("Collection index used by insert or remove.")]
        public int? Index { get; set; }
    }
}
