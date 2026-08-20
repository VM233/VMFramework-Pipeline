using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    [VmDataProduct("vmframework.game-prefab-ref")]
    public sealed class VMFrameworkGamePrefabReference
    {
        [VmRequired]
        [VmJsonProperty("id")]
        [VmMinLength(1)]
        [Description("Registered VMFramework GamePrefab id.")]
        public string Id { get; set; }

        [VmRequired]
        [VmJsonProperty("fullTypeName")]
        [VmMinLength(1)]
        [Description("Full CLR type name of the GamePrefab.")]
        public string FullTypeName { get; set; }

        [VmRequired]
        [VmJsonProperty("wrapperPath")]
        [VmMinLength(1)]
        [Description("Asset path of the authoritative GamePrefab wrapper.")]
        public string WrapperPath { get; set; }

        [VmRequired]
        [VmJsonProperty("generalSettingPath")]
        [VmMinLength(1)]
        [Description("Asset path of the authoritative GamePrefabGeneralSetting.")]
        public string GeneralSettingPath { get; set; }
    }
}
