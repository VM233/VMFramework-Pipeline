#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkInspectRuntimeGameItemTool
    {
        private const string ToolName = "vmframework/inspect-runtime-game-item";
        private const string InputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"cleanupToken\":{\"type\":\"string\",\"description\":\"Runtime GameItem session token.\"}," +
            "\"objectID\":{\"type\":\"string\",\"description\":\"Unity object id of a controller GameItem component or GameObject.\"}," +
            "\"gameObjectPath\":{\"type\":\"string\",\"description\":\"Loaded-scene GameObject path or name.\"}" +
            "},\"oneOf\":[{\"required\":[\"cleanupToken\"]},{\"required\":[\"objectID\"]},{\"required\":[\"gameObjectPath\"]}],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"identity\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"gameTags\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"properties\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"containers\":{\"type\":\"array\",\"items\":" + VMFrameworkPipelineSchemaJson.Map + "}," +
            "\"abilities\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"faction\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"lifecycle\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"domain\":" + VMFrameworkPipelineSchemaJson.Map +
            "},\"required\":[\"identity\",\"gameTags\",\"properties\",\"containers\",\"abilities\",\"faction\",\"lifecycle\",\"domain\"],\"additionalProperties\":false}";

        [VmProjectTool(ToolName,
            ShortName = "vmf/inspect-runtime-item",
            Description = "Inspect one live VMFramework GameItem in a single response: identity, GameTags, Properties, Containers, project-domain Abilities and Faction, lifecycle, and pool state.",
            InputSchemaJson = InputSchema,
            OutputSchemaJson = OutputSchema,
            SideEffects = VmProjectToolSideEffect.ReadsProjectState,
            ErrorCodes = new[]
            {
                "requires_play_mode",
                "runtime_game_item_not_found",
                "runtime_game_item_session_not_found",
                "runtime_game_item_domain_adapter_ambiguous",
            },
            ReadOnly = true,
            RequiresPlayMode = true)]
        public static object Execute(Dictionary<string, object> args)
        {
            if (!Application.isPlaying)
            {
                throw new VmProjectToolException("requires_play_mode",
                    "Runtime GameItem inspection requires Play Mode.");
            }
            return VMFrameworkRuntimeGameItemInspector.Describe(
                VMFrameworkRuntimeGameItemInspector.Resolve(
                    args ?? new Dictionary<string, object>()));
        }
    }
}
#endif
