using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkCreateMissingGeneralSettingsResult
    {
        [VmRequired, VmJsonProperty("generalSettingsFolderPath")]
        public string GeneralSettingsFolderPath { get; set; }

        [VmRequired, VmJsonProperty("globalSettingCount")]
        public int GlobalSettingCount { get; set; }

        [VmRequired, VmJsonProperty("generalSettingCount")]
        public int GeneralSettingCount { get; set; }

        [VmRequired, VmJsonProperty("createdGeneralSettings")]
        [Description("New GeneralSetting asset paths. Existing assets and assigned references are preserved.")]
        public List<string> CreatedGeneralSettings { get; set; }

        [VmRequired, VmJsonProperty("bindings")]
        [Description("Every GeneralSetting field read back from the synchronously imported global setting files.")]
        public List<VMFrameworkGeneralSettingBinding> Bindings { get; set; }
    }

    public sealed class VMFrameworkGeneralSettingBinding
    {
        [VmRequired, VmJsonProperty("globalSettingPath")]
        public string GlobalSettingPath { get; set; }

        [VmRequired, VmJsonProperty("fieldName")]
        public string FieldName { get; set; }

        [VmRequired, VmJsonProperty("generalSettingPath")]
        public string GeneralSettingPath { get; set; }
    }
}
