using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public enum VMFrameworkGamePrefabUpdateOperationKind
    {
        [VmJsonEnumValue("set")]
        Set,

        [VmJsonEnumValue("append")]
        Append,

        [VmJsonEnumValue("insert")]
        Insert,

        [VmJsonEnumValue("remove")]
        Remove,

        [VmJsonEnumValue("clear")]
        Clear,
    }
}
