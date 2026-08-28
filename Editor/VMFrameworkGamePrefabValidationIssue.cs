using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkGamePrefabValidationIssue
    {
        [VmRequired]
        [VmJsonProperty("code")]
        [VmMinLength(1)]
        [Description("Stable machine-readable validation issue code.")]
        public string Code { get; set; }

        [VmRequired]
        [VmJsonProperty("severity")]
        [VmMinLength(1)]
        [Description("Validation severity. GamePrefab registration and Prefab contract failures are errors.")]
        public string Severity { get; set; }

        [VmRequired]
        [VmJsonProperty("gamePrefabId")]
        [Description("GamePrefab id, or an empty string when the wrapper entry itself is null or unreadable.")]
        public string GamePrefabId { get; set; }

        [VmRequired]
        [VmJsonProperty("fullTypeName")]
        [Description("Full GamePrefab CLR type name, or an empty string when no config instance is available.")]
        public string FullTypeName { get; set; }

        [VmRequired]
        [VmJsonProperty("wrapperPath")]
        [Description("Asset path of the GamePrefabWrapper that owns the invalid config, or the provider source when wrapper data is unavailable.")]
        public string WrapperPath { get; set; }

        [VmRequired]
        [VmJsonProperty("member")]
        [VmMinLength(1)]
        [Description("Framework contract member that failed validation.")]
        public string Member { get; set; }

        [VmRequired]
        [VmJsonProperty("message")]
        [VmMinLength(1)]
        [Description("Human-readable validation failure.")]
        public string Message { get; set; }
    }
}
