#if UNITY_EDITOR
using System.Collections.Generic;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkRuntimeGameItemSessionTool
    {
        private const string ToolName = "vmframework/runtime-game-item-session";
        private const string InputSchema =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\",\"enum\":[\"create\",\"inspect\",\"cleanup\"],\"description\":\"Session operation.\"}," +
            "\"gamePrefabID\":{\"type\":\"string\",\"minLength\":1,\"description\":\"GamePrefab id borrowed from GameItemManager for create.\"}," +
            "\"sessionKey\":{\"type\":\"string\",\"description\":\"Optional caller key that reuses one live session only when all create arguments match.\"}," +
            "\"factionID\":{\"type\":\"string\",\"description\":\"Optional project-domain faction id applied through the authoritative domain adapter.\"}," +
            "\"properties\":{\"type\":\"object\",\"description\":\"Writable PropertyManager values applied after borrowing.\",\"additionalProperties\":" + VMFrameworkPipelineSchemaJson.ValueReference + "}," +
            "\"position\":{\"description\":\"Optional world position as {x,y,z} or [x,y,z].\",\"anyOf\":[{\"type\":\"object\",\"properties\":{\"x\":{\"type\":\"number\",\"description\":\"World X coordinate.\"},\"y\":{\"type\":\"number\",\"description\":\"World Y coordinate.\"},\"z\":{\"type\":\"number\",\"description\":\"World Z coordinate.\"}},\"additionalProperties\":false},{\"type\":\"array\",\"minItems\":2,\"maxItems\":3,\"items\":{\"type\":\"number\"}}]}," +
            "\"parentPath\":{\"type\":\"string\",\"description\":\"Optional loaded-scene parent GameObject path.\"}," +
            "\"panelID\":{\"type\":\"string\",\"description\":\"Optional UIPanel id whose BindObjectsManager receives the GameItem.\"}," +
            "\"bindName\":{\"type\":\"string\",\"description\":\"BindObjectsManager name. Defaults to the global bind name.\"}," +
            "\"openPanel\":{\"type\":\"boolean\",\"description\":\"Open panelID before binding when needed. Defaults to true.\"}," +
            "\"closePanelOnCleanup\":{\"type\":\"boolean\",\"description\":\"Close a panel opened by this session during cleanup. Defaults to true.\"}," +
            "\"cleanupToken\":{\"type\":\"string\",\"description\":\"Unified live-session token returned by create and consumed by inspect or cleanup.\"}" +
            "},\"required\":[\"action\"],\"oneOf\":[" +
            "{\"properties\":{\"action\":{\"const\":\"create\",\"description\":\"Create or idempotently reuse a live session.\"}},\"required\":[\"gamePrefabID\"]}," +
            "{\"properties\":{\"action\":{\"const\":\"inspect\",\"description\":\"Inspect a live session.\"}},\"required\":[\"cleanupToken\"]}," +
            "{\"properties\":{\"action\":{\"const\":\"cleanup\",\"description\":\"Clean every resource owned by a live session.\"}},\"required\":[\"cleanupToken\"]}" +
            "],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{" + VMFrameworkPipelineSchemaJson.Definitions + ",\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\"}," +
            "\"cleanupToken\":{\"type\":\"string\"}," +
            "\"reused\":{\"type\":\"boolean\"}," +
            "\"session\":" + VMFrameworkPipelineSchemaJson.Map + "," +
            "\"cleanup\":" + VMFrameworkPipelineSchemaJson.Map +
            "},\"required\":[\"action\"],\"additionalProperties\":false}";

        [VmProjectTool(ToolName,
            ShortName = "vmf/runtime-item-session",
            Description = "Create, inspect, or clean one owner-scoped VMFramework runtime GameItem session that owns pool borrowing, placement, properties, optional domain faction setup, and UI binding.",
            InputSchemaJson = InputSchema,
            OutputSchemaJson = OutputSchema,
            CleanupToolName = ToolName,
            SideEffects = VmProjectToolSideEffect.ChangesRuntimeState |
                          VmProjectToolSideEffect.CreatesTemporaryObjects,
            ErrorCodes = new[]
            {
                "requires_play_mode",
                "game_item_manager_unavailable",
                "runtime_game_item_session_not_found",
                "runtime_game_item_session_key_conflict",
                "runtime_game_item_domain_adapter_not_found",
                "runtime_game_item_domain_adapter_ambiguous",
                "runtime_game_item_faction_not_found",
                "runtime_game_item_faction_property_not_found",
                "runtime_game_item_not_placeable",
                "runtime_game_item_has_no_property_manager",
                "runtime_game_item_property_not_found",
                "runtime_game_item_property_read_only",
                "ui_panel_manager_unavailable",
                "ui_panel_not_found",
                "ui_panel_has_no_bind_objects_manager",
                "runtime_game_item_session_cleanup_failed",
            },
            MutatesRuntime = true,
            RequiresPlayMode = true)]
        public static object Execute(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string action = args.TryGetValue("action", out object actionValue)
                ? actionValue?.ToString()
                : "";
            switch (action)
            {
                case "create":
                {
                    VMFrameworkRuntimeGameItemSessions.Session session =
                        VMFrameworkRuntimeGameItemSessions.Create(args, out bool reused);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "cleanupToken", session.Token },
                        { "reused", reused },
                        { "session", VMFrameworkRuntimeGameItemSessions.Describe(session) },
                    };
                }
                case "inspect":
                {
                    string token = args["cleanupToken"].ToString();
                    VMFrameworkRuntimeGameItemSessions.Session session =
                        VMFrameworkRuntimeGameItemSessions.GetRequired(token);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "session", VMFrameworkRuntimeGameItemSessions.Describe(session) },
                    };
                }
                case "cleanup":
                {
                    string token = args["cleanupToken"].ToString();
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "cleanup", VMFrameworkRuntimeGameItemSessions.Cleanup(token) },
                    };
                }
                default:
                    throw new VmProjectToolException("invalid_arguments",
                        "action must be create, inspect, or cleanup.");
            }
        }
    }
}
#endif
