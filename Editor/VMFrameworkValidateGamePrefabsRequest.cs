using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkValidateGamePrefabsRequest
    {
        [VmJsonProperty("maxIssues")]
        [VmRange(1, 5000)]
        [VmDefaultSource("Preferences > VM Unity Automation > Tool Responses")]
        [Description("Maximum issue records returned. The complete scan and aggregate counts are never truncated.")]
        public int? MaxIssues { get; set; }
    }
}
