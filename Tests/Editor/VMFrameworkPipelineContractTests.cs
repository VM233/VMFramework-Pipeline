#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public class VMFrameworkPipelineContractTests
    {
        private static readonly string[] ExpectedToolNames =
        {
            "vmframework/add-game-prefab",
            "vmframework/find-game-prefab",
            "vmframework/get-configuration",
            "vmframework/get-property",
            "vmframework/get-property-trace",
            "vmframework/inspect-bind-objects",
            "vmframework/inspect-container-panel",
            "vmframework/inspect-game-prefab",
            "vmframework/inspect-game-prefab-wrapper",
            "vmframework/inspect-property-manager",
            "vmframework/inspect-runtime-game-item",
            "vmframework/inspect-ui-panel",
            "vmframework/list-game-prefab-types",
            "vmframework/list-game-tags",
            "vmframework/list-general-settings",
            "vmframework/logic-tick-control",
            "vmframework/procedure-state",
            "vmframework/query-game-prefab-configs",
            "vmframework/reference-trace",
            "vmframework/runtime-game-item-session",
            "vmframework/runtime-ui-panel",
            "vmframework/set-property",
            "vmframework/start-property-trace",
            "vmframework/stop-property-trace",
            "vmframework/update-game-prefab",
            "vmframework/upsert-game-tag",
            "vmframework/validate-game-tags",
            "vmframework/validate-game-prefabs",
            "vmframework/validate-visual-element-paths",
        };

        private static readonly HashSet<string> ExpectedCustomErrorToolNames =
            new(StringComparer.Ordinal)
            {
                "vmframework/inspect-runtime-game-item",
                "vmframework/logic-tick-control",
                "vmframework/procedure-state",
                "vmframework/reference-trace",
                "vmframework/runtime-game-item-session",
                "vmframework/runtime-ui-panel",
                "vmframework/validate-game-prefabs",
            };

        [Test]
        public void ProjectToolCatalog_IsCompleteStrictAndCanonical()
        {
            var tools = VmProjectToolRegistry.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .OrderBy(tool => GetString(tool, "toolName"), StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEqual(
                ExpectedToolNames.OrderBy(name => name, StringComparer.Ordinal),
                tools.Select(tool => GetString(tool, "toolName")));

            foreach (var tool in tools)
            {
                string toolName = GetString(tool, "toolName");
                foreach (string retiredKey in new[]
                         {
                             "readOnly", "mutatesAssets", "mutatesRuntime", "dangerous",
                             "longRunning", "mayReloadDomain", "requiresPlayMode",
                             "firstClass", "cleanupAvailable", "incrementalJob",
                             "hasOutputSchema", "enforcesInputSchema",
                             "enforcesOutputSchema", "valid",
                         })
                {
                    Assert.That(tool.ContainsKey(retiredKey), Is.False,
                        $"{toolName} still exposes legacy boolean metadata '{retiredKey}'.");
                }
                Assert.That(HasTag(tool, "invalid"), Is.False, toolName);
                Assert.That(tool["executeRoute"],
                    Is.EqualTo(VmProjectToolRegistry.GetDirectRoute(toolName)), toolName);
                Assert.That(tool["moduleId"], Is.EqualTo("vmframework"), toolName);
                Assert.That(tool["capability"].ToString(), Is.Not.Empty, toolName);
                Assert.That(tool["operationKind"].ToString(), Is.Not.Empty, toolName);

                int operationKinds =
                    (HasTag(tool, "readOnly") ? 1 : 0) +
                    (HasSideEffect(tool, "writesAssets") ||
                     HasSideEffect(tool, "writesScene") ? 1 : 0) +
                    (HasSideEffect(tool, "changesRuntimeState") ? 1 : 0);
                Assert.That(operationKinds, Is.EqualTo(1), toolName);

                var inputSchema = RequireDictionary(tool["inputSchema"]);
                Assert.That(inputSchema["additionalProperties"], Is.EqualTo(false),
                    $"{toolName} must reject unknown business arguments.");
                AssertExactSchema(inputSchema, $"{toolName}.inputSchema");

                Assert.That(HasTag(tool, "outputSchema"), Is.True,
                    $"{toolName} must provide and enforce outputSchema.");
                var outputSchema = RequireDictionary(tool["outputSchema"]);
                Assert.That(outputSchema["type"], Is.EqualTo("object"), toolName);
                AssertExactSchema(outputSchema, $"{toolName}.outputSchema");
                Assert.That(tool["sideEffects"], Is.InstanceOf<IList>(), toolName);
                Assert.That(((IList)tool["sideEffects"]).Count,
                    Is.GreaterThan(0), toolName);

                if (ExpectedCustomErrorToolNames.Contains(toolName))
                {
                    Assert.That(tool["errorCodes"], Is.InstanceOf<IList>(), toolName);
                    Assert.That(((IList)tool["errorCodes"]).Count,
                        Is.GreaterThan(0), toolName);
                }
            }
        }

        [Test]
        public void NewRuntimeAndWaitTools_ExposeLifecycleAndIncrementalContracts()
        {
            var details = VmProjectToolRegistry.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            var session = details["vmframework/runtime-game-item-session"];
            Assert.That(HasTag(session, "cleanup"), Is.True);
            Assert.That(session["cleanupToolName"],
                Is.EqualTo("vmframework/runtime-game-item-session"));
            CollectionAssert.Contains((IList)session["sideEffects"],
                "createsTemporaryObjects");
            CollectionAssert.Contains((IList)session["sideEffects"],
                "changesRuntimeState");

            foreach (string toolName in new[]
                     {
                         "vmframework/logic-tick-control",
                         "vmframework/procedure-state",
                         "vmframework/reference-trace",
                         "vmframework/runtime-ui-panel",
                     })
            {
                Assert.That(HasTag(details[toolName], "incrementalJob"),
                    Is.True, toolName);
            }

            CollectionAssert.Contains(
                (IList)details["vmframework/logic-tick-control"]["sideEffects"],
                "advancesLogicTicks");
            CollectionAssert.Contains(
                (IList)details["vmframework/reference-trace"]["sideEffects"],
                "readsProjectState");
        }

        [Test]
        public void RuntimePropertyTools_DeclareAccurateOperationMetadataAndSchemas()
        {
            var details = VmProjectToolRegistry.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            var setProperty = details["vmframework/set-property"];
            Assert.That(HasSideEffect(setProperty, "changesRuntimeState"), Is.True);
            Assert.That(HasTag(setProperty, "requiresPlayMode"), Is.True);

            var startTrace = details["vmframework/start-property-trace"];
            Assert.That(HasSideEffect(startTrace, "changesRuntimeState"), Is.True);
            var startProperties = RequireDictionary(
                RequireDictionary(startTrace["inputSchema"])["properties"]);
            Assert.That(startProperties.ContainsKey("maxEvents"), Is.True);
            Assert.That(startProperties.ContainsKey("clear"), Is.False);

            var getTrace = details["vmframework/get-property-trace"];
            Assert.That(HasTag(getTrace, "readOnly"), Is.True);
            var readProperties = RequireDictionary(
                RequireDictionary(getTrace["inputSchema"])["properties"]);
            Assert.That(readProperties.Keys, Is.EquivalentTo(new[] { "offset", "limit" }));

            var stopTrace = details["vmframework/stop-property-trace"];
            Assert.That(HasSideEffect(stopTrace, "changesRuntimeState"), Is.True);
            Assert.That(
                RequireDictionary(RequireDictionary(stopTrace["inputSchema"])["properties"]).Keys,
                Is.EquivalentTo(new[] { "offset", "limit" }));
        }

        [Test]
        public void GamePrefabTools_PublishOneNominalReferenceAcrossTheAuthoringChain()
        {
            var details = VmProjectToolRegistry.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            Dictionary<string, object> addReference = GetPropertySchema(
                RequireDictionary(details["vmframework/add-game-prefab"]["outputSchema"]),
                "gamePrefab");
            Dictionary<string, object> findReferences = GetPropertySchema(
                RequireDictionary(details["vmframework/find-game-prefab"]["outputSchema"]),
                "gamePrefabs");
            Dictionary<string, object> findReference =
                RequireDictionary(findReferences["items"]);
            Dictionary<string, object> inspectInput = GetPropertySchema(
                RequireDictionary(details["vmframework/inspect-game-prefab"]["inputSchema"]),
                "gamePrefab");
            Dictionary<string, object> updateInput = GetPropertySchema(
                RequireDictionary(details["vmframework/update-game-prefab"]["inputSchema"]),
                "gamePrefab");
            Dictionary<string, object> updateOutput = GetPropertySchema(
                RequireDictionary(details["vmframework/update-game-prefab"]["outputSchema"]),
                "gamePrefab");

            foreach (Dictionary<string, object> referenceSchema in new[]
                     {
                         addReference, findReference, inspectInput, updateInput, updateOutput,
                     })
            {
                Assert.That(referenceSchema[VmJsonContract.DataProductKeyword],
                    Is.EqualTo("vmframework.game-prefab-ref"));
                Assert.That(referenceSchema["additionalProperties"], Is.EqualTo(false));
                Assert.That(RequireDictionary(referenceSchema["properties"]).Keys,
                    Is.EquivalentTo(new[]
                    {
                        "id", "fullTypeName", "wrapperPath", "generalSettingPath",
                    }));
            }

            Dictionary<string, object> updateSchema =
                RequireDictionary(details["vmframework/update-game-prefab"]["inputSchema"]);
            Dictionary<string, object> operations =
                GetPropertySchema(updateSchema, "operations");
            Assert.That(operations["minItems"], Is.EqualTo(1));
            Dictionary<string, object> operation =
                RequireDictionary(operations["items"]);
            Dictionary<string, object> operationType =
                GetPropertySchema(operation, "type");
            CollectionAssert.AreEqual(
                new[] { "set", "append", "insert", "remove", "clear" },
                (IList)operationType["enum"]);

            Dictionary<string, object> transaction =
                RequireDictionary(details["vmframework/update-game-prefab"]["transaction"]);
            Assert.That(transaction.Keys, Is.EquivalentTo(new[]
            {
                "scope", "atomicity", "isolation", "durability",
                "rollbackKind", "commitEvidence",
            }));
            CollectionAssert.Contains((IList)transaction["commitEvidence"],
                "game-prefab-semantic-readback");
            CollectionAssert.Contains(
                (IList)details["vmframework/update-game-prefab"]["errorCodes"],
                "rollback_failed");
        }

        [Test]
        public void GamePrefabConfigQuery_ExposesComposableFiltersAndExplicitProjections()
        {
            var details = VmProjectToolRegistry.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));
            Dictionary<string, object> query =
                details["vmframework/query-game-prefab-configs"];
            Dictionary<string, object> input =
                RequireDictionary(query["inputSchema"]);
            Dictionary<string, object> inputProperties =
                RequireDictionary(input["properties"]);

            Assert.That(inputProperties.Keys, Is.EquivalentTo(new[]
            {
                "id", "filter", "gamePrefabType", "gameTagsAll",
                "gameTagsAny", "gameTagsNone", "hasName", "hasDescription",
                "fields", "locales", "offset", "limit",
            }));
            Dictionary<string, object> fields =
                RequireDictionary(inputProperties["fields"]);
            CollectionAssert.AreEqual(new[] { "gameTags", "name", "description" },
                (IList)RequireDictionary(fields["items"])["enum"]);
            Assert.That(RequireDictionary(inputProperties["limit"])["maximum"],
                Is.EqualTo(500d));

            Dictionary<string, object> output =
                RequireDictionary(query["outputSchema"]);
            Dictionary<string, object> records = GetPropertySchema(output,
                "gamePrefabs");
            Dictionary<string, object> record =
                RequireDictionary(records["items"]);
            Assert.That(RequireDictionary(record["properties"]).Keys,
                Is.EquivalentTo(new[]
                {
                    "gamePrefab", "gameTags", "name", "description",
                }));
            Dictionary<string, object> gamePrefab = GetPropertySchema(record,
                "gamePrefab");
            Assert.That(gamePrefab[VmJsonContract.DataProductKeyword],
                Is.EqualTo("vmframework.game-prefab-ref"));
        }

        [Test]
        public void PanelTools_RequireAnUnambiguousSelector_AndValidationSupportsAllPanels()
        {
            var details = VmProjectToolRegistry.GetToolDetails(false)
                .Where(tool => GetString(tool, "toolName").StartsWith(
                    "vmframework/", StringComparison.Ordinal))
                .ToDictionary(tool => GetString(tool, "toolName"));

            foreach (string toolName in new[]
                     {
                         "vmframework/inspect-ui-panel",
                         "vmframework/inspect-bind-objects",
                         "vmframework/inspect-container-panel"
                     })
            {
                var schema = RequireDictionary(details[toolName]["inputSchema"]);
                Assert.That(schema["oneOf"], Is.InstanceOf<IList>(), toolName);
                Assert.That(((IList)schema["oneOf"]).Count, Is.EqualTo(2), toolName);
            }

            var validationSchema = RequireDictionary(
                details["vmframework/validate-visual-element-paths"]["inputSchema"]);
            var validationProperties = RequireDictionary(validationSchema["properties"]);
            Assert.That(validationProperties.ContainsKey("allPanels"), Is.True);
            Assert.That(validationSchema["oneOf"], Is.InstanceOf<IList>());
            Assert.That(((IList)validationSchema["oneOf"]).Count, Is.EqualTo(3));

            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.InspectUIPanel(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.InspectBindObjects(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.InspectContainerPanel(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.ValidateVisualElementPaths(new Dictionary<string, object>()));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.InspectUIPanel(new Dictionary<string, object>
                {
                    { "panelID", "panel" },
                    { "prefabPath", "Assets/Panel.prefab" }
                }));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.ValidateVisualElementPaths(new Dictionary<string, object>
                {
                    { "allPanels", true },
                    { "panelID", "panel" }
                }));
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkUIPanelPipelineTools.ValidateVisualElementPaths(new Dictionary<string, object>
                {
                    { "allPanels", false },
                    { "panelID", "panel" }
                }));
        }

        [Test]
        public void ValidateVisualElementPaths_AllPanels_ReturnsBoundedAggregate()
        {
            var result = RequireDictionary(
                VMFrameworkUIPanelPipelineTools.ValidateVisualElementPaths(new Dictionary<string, object>
                {
                    { "allPanels", true },
                    { "limit", 1 }
                }));

            Assert.That(result["mode"], Is.EqualTo("allPanels"));
            Assert.That(Convert.ToInt32(result["panelCount"]), Is.GreaterThanOrEqualTo(0));
            Assert.That(Convert.ToInt32(result["count"]), Is.LessThanOrEqualTo(1));
            Assert.That(result.ContainsKey("missingPrefabCount"), Is.True);
            Assert.That(result.ContainsKey("missingVisualTreeCount"), Is.True);
            Assert.That(result.ContainsKey("invalidPathCount"), Is.True);
            Assert.That(result["paths"], Is.InstanceOf<IList>());
            Assert.That(((IList)result["paths"]).Count, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ProjectConfiguration_RoundTripsTeamOwnedValidationCoverage()
        {
            string path = Path.GetFullPath(
                "ProjectSettings/VMFrameworkPipelineSettings.json");
            bool existed = File.Exists(path);
            string original = existed ? File.ReadAllText(path) : null;

            Type manager = typeof(VMFrameworkPipelineTools).Assembly.GetType(
                "VMFramework.Pipeline.Editor.VMFrameworkPipelineSettingsManager", true);
            PropertyInfo missingTranslations = manager.GetProperty(
                "IncludeMissingGameTagTranslations",
                BindingFlags.Static | BindingFlags.NonPublic);
            PropertyInfo prefabReferences = manager.GetProperty(
                "IncludeGamePrefabTagReferences",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo reload = manager.GetMethod(
                "ReloadProjectConfiguration",
                BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                File.WriteAllText(path,
                    "{\n" +
                    "  \"schemaVersion\": 1,\n" +
                    "  \"gameTagValidation\": {\n" +
                    "    \"includeMissingTranslations\": false,\n" +
                    "    \"includeGamePrefabReferences\": true\n" +
                    "  }\n" +
                    "}\n");
                reload.Invoke(null, null);

                Assert.That(missingTranslations.GetValue(null), Is.EqualTo(false));
                Assert.That(prefabReferences.GetValue(null), Is.EqualTo(true));
                var snapshot = RequireDictionary(
                    VMFrameworkPipelineConfigurationTool.GetConfiguration(
                        new Dictionary<string, object>()));
                var projectSettings = RequireDictionary(snapshot["projectSettings"]);
                Assert.That(projectSettings.ContainsKey("error"), Is.False);

                prefabReferences.SetValue(null, false);
                reload.Invoke(null, null);
                Assert.That(prefabReferences.GetValue(null), Is.EqualTo(false));
                Assert.That(File.ReadAllText(path),
                    Does.Contain("\"includeGamePrefabReferences\": false"));
            }
            finally
            {
                if (existed)
                    File.WriteAllText(path, original);
                else if (File.Exists(path))
                    File.Delete(path);
                reload.Invoke(null, null);
            }
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out object value)
                ? value?.ToString() ?? ""
                : "";
        }

        private static bool HasTag(Dictionary<string, object> metadata, string tag)
        {
            return HasString(metadata, "tags", tag);
        }

        private static bool HasSideEffect(Dictionary<string, object> metadata, string sideEffect)
        {
            return HasString(metadata, "sideEffects", sideEffect);
        }

        private static bool HasString(Dictionary<string, object> metadata,
            string key, string expected)
        {
            return metadata.TryGetValue(key, out object value) &&
                   value is IEnumerable values &&
                   values.Cast<object>().Any(item =>
                       string.Equals(item?.ToString(), expected, StringComparison.Ordinal));
        }

        private static Dictionary<string, object> RequireDictionary(object value)
        {
            Assert.That(value, Is.InstanceOf<Dictionary<string, object>>());
            return (Dictionary<string, object>)value;
        }

        private static Dictionary<string, object> GetPropertySchema(
            Dictionary<string, object> schema, string propertyName)
        {
            return RequireDictionary(
                RequireDictionary(schema["properties"])[propertyName]);
        }

        private static void AssertExactSchema(Dictionary<string, object> schema,
            string context)
        {
            Assert.That(schema, Is.Not.Empty, $"{context} must not be empty.");
            if (schema.TryGetValue("$defs", out object definitionsValue))
            {
                var definitions = RequireDictionary(definitionsValue);
                foreach (KeyValuePair<string, object> definition in definitions)
                {
                    AssertExactSchemaNode(RequireDictionary(definition.Value),
                        $"{context}.$defs.{definition.Key}", schema);
                }
            }

            AssertExactSchemaNode(schema, context, schema);
        }

        private static void AssertExactSchemaNode(Dictionary<string, object> schema,
            string context, Dictionary<string, object> root, bool allowConstraintOnly = false)
        {
            Assert.That(schema.TryGetValue("x-vmAutomationOpaque", out object opaque) &&
                        Equals(opaque, true), Is.False,
                $"{context} must not be opaque.");

            var declaredTypes = new HashSet<string>(StringComparer.Ordinal);
            if (schema.TryGetValue("type", out object typeValue))
            {
                if (typeValue is string type)
                    declaredTypes.Add(type);
                else if (typeValue is IEnumerable types)
                {
                    foreach (object item in types)
                        declaredTypes.Add(item?.ToString() ?? "");
                }
            }

            bool hasValueShape = declaredTypes.Count > 0 ||
                                 schema.ContainsKey("$ref") ||
                                 schema.ContainsKey("const") ||
                                 schema.ContainsKey("enum") ||
                                 schema.ContainsKey("allOf") ||
                                 schema.ContainsKey("anyOf") ||
                                 schema.ContainsKey("oneOf");
            Assert.That(hasValueShape || allowConstraintOnly, Is.True,
                $"{context} must declare an exact value shape.");

            if (schema.TryGetValue("$ref", out object referenceValue))
            {
                string reference = referenceValue?.ToString() ?? "";
                const string prefix = "#/$defs/";
                Assert.That(reference, Does.StartWith(prefix), context);
                string definitionName = reference.Substring(prefix.Length);
                Assert.That(definitionName, Does.Not.Contain("/"), context);
                var definitions = RequireDictionary(root["$defs"]);
                Assert.That(definitions.ContainsKey(definitionName), Is.True,
                    $"{context} references missing definition '{definitionName}'.");
            }

            if (declaredTypes.Contains("object"))
            {
                Assert.That(schema.ContainsKey("additionalProperties"), Is.True,
                    $"{context} must declare additionalProperties.");
                object additionalProperties = schema["additionalProperties"];
                Assert.That(Equals(additionalProperties, true), Is.False,
                    $"{context} must not accept arbitrary properties.");
                if (additionalProperties is Dictionary<string, object> mapValueSchema)
                {
                    Assert.That(mapValueSchema, Is.Not.Empty, context);
                    AssertExactSchemaNode(mapValueSchema,
                        $"{context}.additionalProperties", root);
                }
                else
                {
                    Assert.That(additionalProperties, Is.EqualTo(false), context);
                }

                if (schema.TryGetValue("properties", out object propertiesValue))
                {
                    foreach (KeyValuePair<string, object> property in
                             RequireDictionary(propertiesValue))
                    {
                        AssertExactSchemaNode(RequireDictionary(property.Value),
                            $"{context}.properties.{property.Key}", root);
                    }
                }
            }

            if (declaredTypes.Contains("array"))
            {
                Assert.That(schema.ContainsKey("items"), Is.True, context);
                var items = RequireDictionary(schema["items"]);
                Assert.That(items, Is.Not.Empty, context);
                AssertExactSchemaNode(items, $"{context}.items", root);
            }

            foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
            {
                if (schema.TryGetValue(keyword, out object variantsValue) == false)
                    continue;
                Assert.That(variantsValue, Is.InstanceOf<IList>(), context);
                var variants = (IList)variantsValue;
                Assert.That(variants.Count, Is.GreaterThan(0), context);
                for (int index = 0; index < variants.Count; index++)
                {
                    AssertExactSchemaNode(RequireDictionary(variants[index]),
                        $"{context}.{keyword}[{index}]", root, true);
                }
            }

            if (schema.TryGetValue("not", out object notValue))
            {
                AssertExactSchemaNode(RequireDictionary(notValue),
                    $"{context}.not", root, true);
            }
        }
    }
}
#endif
