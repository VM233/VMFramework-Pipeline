using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkLocalizedStringSnapshot
    {
        [VmRequired]
        [VmJsonProperty("table")]
        [Description("String Table Collection name or GUID stored by the LocalizedString.")]
        public string Table { get; set; }

        [VmRequired]
        [VmJsonProperty("key")]
        [Description("Resolved String Table entry key.")]
        public string Key { get; set; }

        [VmJsonProperty("values")]
        [Description("Requested locale values. Omitted when locale values were not requested.")]
        public List<VMFrameworkLocalizedStringValue> Values { get; set; }
    }
}
