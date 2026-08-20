#if UNITY_EDITOR
namespace VMFramework.Pipeline.Editor
{
    /// <summary>
    /// Shared recursive JSON contract for the deliberately dynamic leaves exposed by
    /// runtime inspection tools. Tool result roots remain closed and route-specific.
    /// </summary>
    internal static class VMFrameworkPipelineSchemaJson
    {
        internal const string ValueReference =
            "{\"$ref\":\"#/$defs/vmJsonValue\"}";

        internal const string Map =
            "{\"type\":\"object\",\"additionalProperties\":" + ValueReference + "}";

        internal const string NullableMap =
            "{\"oneOf\":[" + Map + ",{\"type\":\"null\"}]}";

        internal const string MapArray =
            "{\"type\":\"array\",\"items\":" + Map + "}";

        internal const string Definitions =
            "\"$defs\":{\"vmJsonValue\":{\"oneOf\":[" +
            "{\"type\":\"null\"}," +
            "{\"type\":\"boolean\"}," +
            "{\"type\":\"number\"}," +
            "{\"type\":\"string\"}," +
            "{\"type\":\"array\",\"items\":" + ValueReference + "}," +
            Map +
            "]}}";
    }
}
#endif
