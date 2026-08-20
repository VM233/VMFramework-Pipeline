using System.Collections.Generic;
using System.ComponentModel;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkQueryGamePrefabConfigsRequest
    {
        [VmJsonProperty("id")]
        [VmMinLength(1)]
        [Description("Optional exact GamePrefab id.")]
        public string Id { get; set; }

        [VmJsonProperty("filter")]
        [VmMinLength(1)]
        [Description("Optional case-insensitive text matched against GamePrefab identity, type, wrapper name, and wrapper path.")]
        public string Filter { get; set; }

        [VmJsonProperty("gamePrefabType")]
        [VmMinLength(1)]
        [Description("Optional GamePrefab type name, full name, or assembly-qualified name. Derived types are included.")]
        public string GamePrefabType { get; set; }

        [VmJsonProperty("gameTagsAll")]
        [VmMinItems(1)]
        [Description("Up to 64 exact GameTag ids that every match must contain.")]
        public List<string> GameTagsAll { get; set; }

        [VmJsonProperty("gameTagsAny")]
        [VmMinItems(1)]
        [Description("Up to 64 exact GameTag ids; every match must contain at least one.")]
        public List<string> GameTagsAny { get; set; }

        [VmJsonProperty("gameTagsNone")]
        [VmMinItems(1)]
        [Description("Up to 64 exact GameTag ids that no match may contain.")]
        public List<string> GameTagsNone { get; set; }

        [VmJsonProperty("hasDescription")]
        [Description("Optional filter for GamePrefabs with an enabled localized description.")]
        public bool? HasDescription { get; set; }

        [VmJsonProperty("hasName")]
        [Description("Optional filter for GamePrefabs with a configured localized name.")]
        public bool? HasName { get; set; }

        [VmJsonProperty("fields")]
        [VmMinItems(1)]
        [Description("Optional fields projected beside the canonical GamePrefab reference. Omit for identity-only results.")]
        public List<VMFrameworkGamePrefabConfigField> Fields { get; set; }

        [VmJsonProperty("locales")]
        [VmMinItems(1)]
        [Description("Up to 16 locale codes retained in name and description values. Omit to include every locale in each referenced table.")]
        public List<string> Locales { get; set; }

        [VmJsonProperty("offset")]
        [VmRange(0, int.MaxValue)]
        [Description("Zero-based result offset.")]
        public int Offset { get; set; }

        [VmJsonProperty("limit")]
        [VmRange(1, 500)]
        [VmDefaultSource("Preferences > VM Unity Automation > Tool Responses")]
        [Description("Maximum returned config records.")]
        public int? Limit { get; set; }
    }
}
