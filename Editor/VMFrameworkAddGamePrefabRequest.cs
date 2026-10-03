using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkAddGamePrefabRequest
    {
        [VmRequired]
        [VmJsonProperty("id")]
        [VmMinLength(1)]
        [Description("GamePrefab id to create or replace.")]
        public string Id { get; set; }

        [VmRequired]
        [VmJsonProperty("gamePrefabType")]
        [VmMinLength(1)]
        [Description("Instantiable GamePrefab type name, full name, or assembly-qualified name.")]
        public string GamePrefabType { get; set; }

        [VmJsonProperty("overwrite")]
        [Description("Replace the existing single-wrapper GamePrefab with the same id.")]
        public bool Overwrite { get; set; }

        [VmJsonProperty("assetName")]
        [VmMinLength(1)]
        [Description("Optional wrapper asset file name when creating a GamePrefab.")]
        public string AssetName { get; set; }

        [VmJsonProperty("serializedValues")]
        [Description("Serialized field or property values using the same conversion as GamePrefab updates: nested objects, arrays and collections, optional assignable $type descriptors, and Unity Object asset-path or {assetPath, guid, fileID} references. Identity is supplied only by the root id argument.")]
        public Dictionary<string, object> SerializedValues { get; set; }
    }
}
