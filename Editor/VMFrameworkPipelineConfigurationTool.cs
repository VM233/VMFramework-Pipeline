#if UNITY_EDITOR
using System.Collections.Generic;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkPipelineConfigurationTool
    {
        private const string GET_CONFIGURATION_TOOL_NAME = "vmframework/get-configuration";
        private const string EMPTY_INPUT_SCHEMA =
            "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
        private const string OUTPUT_SCHEMA =
            "{\"type\":\"object\",\"properties\":{" +
            "\"precedence\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"projectSettings\":{\"type\":\"object\",\"properties\":{" +
            "\"path\":{\"type\":\"string\"},\"found\":{\"type\":\"boolean\"}," +
            "\"valid\":{\"type\":\"boolean\"},\"error\":{\"type\":\"string\"}," +
            "\"gameTagValidation\":{\"type\":\"object\",\"properties\":{" +
            "\"includeMissingTranslations\":{\"type\":\"boolean\"}," +
            "\"includeGamePrefabReferences\":{\"type\":\"boolean\"}" +
            "},\"required\":[\"includeMissingTranslations\",\"includeGamePrefabReferences\"],\"additionalProperties\":false}" +
            "},\"required\":[\"path\",\"found\",\"valid\",\"gameTagValidation\"],\"additionalProperties\":false}," +
            "\"preferences\":{\"type\":\"object\",\"properties\":{" +
            "\"path\":{\"type\":\"string\"},\"gamePrefabInspectionMaxDepth\":{\"type\":\"integer\"}," +
            "\"gamePrefabCollectionItemLimit\":{\"type\":\"integer\"}," +
            "\"includeGamePrefabUpdateSnapshots\":{\"type\":\"boolean\"}," +
            "\"propertyTraceMaxEvents\":{\"type\":\"integer\"}" +
            "},\"required\":[\"path\",\"gamePrefabInspectionMaxDepth\",\"gamePrefabCollectionItemLimit\",\"includeGamePrefabUpdateSnapshots\",\"propertyTraceMaxEvents\"],\"additionalProperties\":false}," +
            "\"sharedAutomationPreferences\":{\"type\":\"object\",\"properties\":{" +
            "\"path\":{\"type\":\"string\"},\"overrideResultDefaults\":{\"type\":\"boolean\"}," +
            "\"defaultResultLimit\":{\"type\":\"integer\"}" +
            "},\"required\":[\"path\",\"overrideResultDefaults\",\"defaultResultLimit\"],\"additionalProperties\":false}" +
            "},\"required\":[\"precedence\",\"projectSettings\",\"preferences\",\"sharedAutomationPreferences\"],\"additionalProperties\":false}";

        [VmProjectTool(GET_CONFIGURATION_TOOL_NAME,
            Description = "Read effective VMFramework Pipeline project settings, user preferences, and the shared VM Unity Automation result-budget preference.",
            InputSchemaJson = EMPTY_INPUT_SCHEMA,
            OutputSchemaJson = OUTPUT_SCHEMA,
            ReadOnly = true)]
        public static object GetConfiguration(Dictionary<string, object> args)
        {
            return VMFrameworkPipelineSettingsManager.GetConfigurationSnapshot();
        }
    }
}
#endif
