using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkInspectGamePrefabResult
    {
        [VmRequired]
        [VmJsonProperty("gamePrefab")]
        [Description("Verified authoritative GamePrefab reference.")]
        public VMFrameworkGamePrefabReference GamePrefab { get; set; }

        [VmRequired]
        [VmJsonProperty("serializedValue")]
        [Description("Serialized GamePrefab contents within the requested bounds.")]
        public Dictionary<string, object> SerializedValue { get; set; }

        [VmRequired]
        [VmJsonProperty("wrapper")]
        [Description("Wrapper asset identity and registration metadata.")]
        public Dictionary<string, object> Wrapper { get; set; }

        [VmRequired]
        [VmJsonProperty("generalSetting")]
        [Description("Authoritative GeneralSetting identity and registration metadata.")]
        public Dictionary<string, object> GeneralSetting { get; set; }
    }
}
