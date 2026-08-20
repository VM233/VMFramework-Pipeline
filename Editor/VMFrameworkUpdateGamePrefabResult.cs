using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkUpdateGamePrefabResult
    {
        [VmRequired]
        [VmJsonProperty("terminalState")]
        [Description("Verified terminal transaction state. Successful updates return committed.")]
        public string TerminalState { get; set; }

        [VmRequired]
        [VmJsonProperty("commitEvidence")]
        [Description("Wrapper byte hashes and semantic readback evidence for the committed update.")]
        public Dictionary<string, object> CommitEvidence { get; set; }

        [VmRequired]
        [VmJsonProperty("gamePrefab")]
        [Description("Authoritative reference after the committed update.")]
        public VMFrameworkGamePrefabReference GamePrefab { get; set; }

        [VmJsonProperty("previousId")]
        [Description("Previous id when the update renamed the GamePrefab.")]
        public string PreviousId { get; set; }

        [VmRequired]
        [VmJsonProperty("operationCount")]
        [Description("Number of operations committed atomically.")]
        public int OperationCount { get; set; }

        [VmRequired]
        [VmJsonProperty("operations")]
        [Description("Normalized committed operations in execution order.")]
        public List<Dictionary<string, object>> Operations { get; set; }

        [VmRequired]
        [VmJsonProperty("diff")]
        [Description("Semantic differences produced by the committed operations.")]
        public List<Dictionary<string, object>> Diff { get; set; }

        [VmJsonProperty("before")]
        [Description("Optional serialized snapshot before the update.")]
        public object Before { get; set; }

        [VmJsonProperty("after")]
        [Description("Optional serialized snapshot after the update.")]
        public object After { get; set; }
    }
}
