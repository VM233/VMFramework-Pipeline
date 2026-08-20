using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkAddGamePrefabResult
    {
        [VmRequired]
        [VmJsonProperty("gamePrefab")]
        [Description("Authoritative reference to the created or replaced GamePrefab.")]
        public VMFrameworkGamePrefabReference GamePrefab { get; set; }

        [VmRequired]
        [VmJsonProperty("created")]
        [Description("True when a new wrapper was created.")]
        public bool Created { get; set; }

        [VmRequired]
        [VmJsonProperty("replaced")]
        [Description("True when an existing wrapper was replaced.")]
        public bool Replaced { get; set; }

        [VmRequired]
        [VmJsonProperty("registered")]
        [Description("True when the wrapper is registered by its authoritative GeneralSetting.")]
        public bool Registered { get; set; }

        [VmRequired]
        [VmJsonProperty("warnings")]
        [Description("Authoring warnings that do not change the committed result.")]
        public List<string> Warnings { get; set; }
    }
}
