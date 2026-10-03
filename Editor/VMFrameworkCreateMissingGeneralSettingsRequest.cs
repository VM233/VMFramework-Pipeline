using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkCreateMissingGeneralSettingsRequest
    {
        [VmJsonProperty("ensureGlobalSettings")]
        [Description("Create missing configured global setting files through the framework owner and verify their Addressables registration before binding GeneralSettings. Defaults to false.")]
        public bool EnsureGlobalSettings { get; set; }
    }
}
