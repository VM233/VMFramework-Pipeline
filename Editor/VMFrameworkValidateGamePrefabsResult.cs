using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkValidateGamePrefabsResult
    {
        [VmRequired]
        [VmJsonProperty("passed")]
        [Description("True only when the complete bounded scan found no errors.")]
        public bool Passed { get; set; }

        [VmRequired]
        [VmJsonProperty("wrapperCount")]
        [Description("Number of distinct GamePrefabWrapper assets scanned.")]
        public int WrapperCount { get; set; }

        [VmRequired]
        [VmJsonProperty("gamePrefabCount")]
        [Description("Number of non-null GamePrefab configs scanned.")]
        public int GamePrefabCount { get; set; }

        [VmRequired]
        [VmJsonProperty("registeredGamePrefabCount")]
        [Description("Number of discoverable GamePrefab configs reachable through the runtime GlobalSettingCollector provider graph.")]
        public int RegisteredGamePrefabCount { get; set; }

        [VmRequired]
        [VmJsonProperty("unregisteredGamePrefabCount")]
        [Description("Number of discoverable GamePrefab configs unreachable through the runtime GlobalSettingCollector provider graph.")]
        public int UnregisteredGamePrefabCount { get; set; }

        [VmRequired]
        [VmJsonProperty("prefabProviderCount")]
        [Description("Number of GamePrefab configs implementing IPrefabProvider.")]
        public int PrefabProviderCount { get; set; }

        [VmRequired]
        [VmJsonProperty("missingPrefabCount")]
        [Description("Number of IPrefabProvider configs whose Prefab reference is null or destroyed.")]
        public int MissingPrefabCount { get; set; }

        [VmRequired]
        [VmJsonProperty("prefabNameMismatchCount")]
        [Description("Number of IPrefabProvider configs whose Prefab names do not align with their GamePrefab ids and declared id suffixes.")]
        public int PrefabNameMismatchCount { get; set; }

        [VmRequired]
        [VmJsonProperty("errorCount")]
        [Description("Total errors found across the complete scan.")]
        public int ErrorCount { get; set; }

        [VmRequired]
        [VmJsonProperty("totalIssues")]
        [Description("Total validation issues found across the complete scan.")]
        public int TotalIssues { get; set; }

        [VmRequired]
        [VmJsonProperty("returnedIssues")]
        [Description("Number of issue records retained in this response.")]
        public int ReturnedIssues { get; set; }

        [VmRequired]
        [VmJsonProperty("truncated")]
        [Description("True when issue records were capped even though aggregate counts cover the complete scan.")]
        public bool Truncated { get; set; }

        [VmRequired]
        [VmJsonProperty("issues")]
        [Description("Bounded validation issue records in deterministic wrapper and config order.")]
        public List<VMFrameworkGamePrefabValidationIssue> Issues { get; set; }
    }
}
