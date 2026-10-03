using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
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
