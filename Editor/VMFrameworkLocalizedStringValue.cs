using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkLocalizedStringValue
    {
        [VmRequired]
        [VmJsonProperty("locale")]
        [VmMinLength(1)]
        [Description("Locale code from the String Table.")]
        public string Locale { get; set; }

        [VmRequired]
        [VmJsonProperty("value")]
        [Description("Exact localized String Table value, or an empty string when the entry is missing.")]
        public string Value { get; set; }
    }
}
