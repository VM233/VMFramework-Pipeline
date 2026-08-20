using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkGamePrefabConfigRecord
    {
        [VmRequired]
        [VmJsonProperty("gamePrefab")]
        [Description("Canonical reference for the registered GamePrefab config.")]
        public VMFrameworkGamePrefabReference GamePrefab { get; set; }

        [VmJsonProperty("gameTags")]
        [Description("Sorted exact GameTag ids when requested.")]
        public List<string> GameTags { get; set; }

        [VmJsonProperty("name")]
        [Description("Localized name reference and selected table values when requested and supported by the config.")]
        public VMFrameworkLocalizedStringSnapshot Name { get; set; }

        [VmJsonProperty("description")]
        [Description("Enabled localized description reference and selected table values when requested.")]
        public VMFrameworkLocalizedStringSnapshot Description { get; set; }
    }
}
