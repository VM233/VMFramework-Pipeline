using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class GamePrefabValueConversionTests
    {
        private sealed class CollectionFixture
        {
            public HashSet<string> gameTags = new();
        }

        private struct RangeFixture
        {
            public int min;
            public int max;
        }

        private sealed class NestedValueFixture
        {
            public RangeFixture monsterCountRangePerWave;
            public List<RangeFixture> ranges = new();
        }

        private sealed class LocalizedStringFixture
        {
            public LocalizedString name = new();
        }

        [Test]
        public void StructuredLocalizedString_IsConvertedBeforeEnumerableHandling()
        {
            MethodInfo convert = typeof(VMFrameworkPipelineGamePrefabTools).GetMethod("ConvertSerializedValue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(convert, Is.Not.Null);

            var rawValue = new Dictionary<string, object>
            {
                { "$type", typeof(LocalizedString).AssemblyQualifiedName },
                { "m_TableReference", new Dictionary<string, object>
                    {
                        { "m_TableCollectionName", "Item" },
                    }
                },
                { "m_TableEntryReference", new Dictionary<string, object>
                    {
                        { "m_KeyId", 0L },
                        { "m_Key", "FlameIngotItemName" },
                    }
                },
            };

            var localizedString = (LocalizedString)convert.Invoke(null,
                new object[] { rawValue, typeof(LocalizedString), "name" });

            Assert.That(localizedString, Is.InstanceOf<IEnumerable>());

            Type localizedReferenceType = typeof(LocalizedString).BaseType;
            var tableReference = (TableReference)localizedReferenceType
                .GetField("m_TableReference", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(localizedString);
            var entryReference = (TableEntryReference)localizedReferenceType
                .GetField("m_TableEntryReference", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(localizedString);

            Assert.That(tableReference.TableCollectionName, Is.EqualTo("Item"));
            Assert.That(entryReference.Key, Is.EqualTo("FlameIngotItemName"));

            tableReference.OnBeforeSerialize();
            tableReference.OnAfterDeserialize();
            entryReference.OnBeforeSerialize();
            entryReference.OnAfterDeserialize();
            Assert.That(tableReference.TableCollectionName, Is.EqualTo("Item"));
            Assert.That(entryReference.Key, Is.EqualTo("FlameIngotItemName"));
        }

        [Test]
        public void DescribeSerializedValue_TreatsLocalizedStringAsStructuredReference()
        {
            MethodInfo convert = GetPrivateMethod("ConvertSerializedValue");
            MethodInfo describe = GetPrivateMethod("DescribeSerializedValue");
            var rawValue = new Dictionary<string, object>
            {
                { "$type", typeof(LocalizedString).AssemblyQualifiedName },
                { "m_TableReference", new Dictionary<string, object>
                    {
                        { "m_TableCollectionName", "Property" },
                    }
                },
                { "m_TableEntryReference", new Dictionary<string, object>
                    {
                        { "m_KeyId", 0L },
                        { "m_Key", "AttackRangePropertyName" },
                    }
                },
            };
            var localizedString = (LocalizedString)convert.Invoke(null,
                new object[] { rawValue, typeof(LocalizedString), "name" });

            var result = (Dictionary<string, object>)describe.Invoke(null,
                new object[] { localizedString, 0, 8, 100, new HashSet<object>() });

            Assert.That(result.ContainsKey("items"), Is.False,
                "LocalizedString is a localized reference, not a semantic collection.");
            var tableReference = (Dictionary<string, object>)result["m_TableReference"];
            var entryReference = (Dictionary<string, object>)result["m_TableEntryReference"];
            Assert.That(tableReference["m_TableCollectionName"], Is.EqualTo("Property"));
            Assert.That(entryReference["m_Key"], Is.EqualTo("AttackRangePropertyName"));
        }

        [Test]
        public void SetPathValue_RefreshesNestedSerializationCallbackState()
        {
            var fixture = new LocalizedStringFixture();
            MethodInfo setPath = GetPrivateMethod("SetPathValue");

            setPath.Invoke(null, new object[]
            {
                fixture, "name.m_TableReference.m_TableCollectionName", "Property",
            });

            Type localizedReferenceType = typeof(LocalizedString).BaseType;
            var tableReference = (TableReference)localizedReferenceType
                .GetField("m_TableReference", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(fixture.name);

            Assert.That(tableReference.TableCollectionName, Is.EqualTo("Property"));
            tableReference.OnBeforeSerialize();
            tableReference.OnAfterDeserialize();
            Assert.That(tableReference.TableCollectionName, Is.EqualTo("Property"));
        }

        [Test]
        public void ListValues_StillUseCollectionConversion()
        {
            MethodInfo convert = typeof(VMFrameworkPipelineGamePrefabTools).GetMethod("ConvertSerializedValue",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(convert, Is.Not.Null);

            var result = (List<string>)convert.Invoke(null,
                new object[] { new[] { "first", "second" }, typeof(List<string>), "values" });

            CollectionAssert.AreEqual(new[] { "first", "second" }, result);
        }

        [Test]
        public void HashSetValues_UseGenericCollectionConversion()
        {
            MethodInfo convert = GetPrivateMethod("ConvertSerializedValue");

            var result = (HashSet<string>)convert.Invoke(null,
                new object[] { new[] { "material_item", "rare_rarity", "material_item" },
                    typeof(HashSet<string>), "gameTags" });

            Assert.That(result.SetEquals(new[] { "material_item", "rare_rarity" }), Is.True);
        }

        [Test]
        public void HashSetOperations_SupportSetAppendIndexedSetRemoveAndClear()
        {
            var fixture = new CollectionFixture();
            MethodInfo setPath = GetPrivateMethod("SetPathValue");
            MethodInfo append = GetPrivateMethod("InsertCollectionValue");
            MethodInfo remove = GetPrivateMethod("RemoveCollectionValue");
            MethodInfo clear = GetPrivateMethod("ClearCollection");

            setPath.Invoke(null, new object[]
            {
                fixture, "gameTags", new[] { "material_item", "common_rarity" },
            });
            Assert.That(fixture.gameTags.SetEquals(new[] { "material_item", "common_rarity" }), Is.True);

            append.Invoke(null, new object[] { fixture, "gameTags", int.MaxValue, "quest_item" });
            Assert.That(fixture.gameTags.Contains("quest_item"), Is.True);

            string replaced = fixture.gameTags.First();
            setPath.Invoke(null, new object[] { fixture, "gameTags[0]", "rare_rarity" });
            Assert.That(fixture.gameTags.Contains(replaced), Is.False);
            Assert.That(fixture.gameTags.Contains("rare_rarity"), Is.True);

            string removed = fixture.gameTags.First();
            remove.Invoke(null, new object[] { fixture, "gameTags", 0 });
            Assert.That(fixture.gameTags.Contains(removed), Is.False);

            clear.Invoke(null, new object[] { fixture, "gameTags" });
            Assert.That(fixture.gameTags, Is.Empty);
        }

        [Test]
        public void SetPathValue_WritesNestedValueTypeBackToOwner()
        {
            var fixture = new NestedValueFixture
            {
                monsterCountRangePerWave = new RangeFixture { min = 3, max = 4 },
            };
            MethodInfo setPath = GetPrivateMethod("SetPathValue");

            setPath.Invoke(null, new object[] { fixture, "monsterCountRangePerWave.min", 6 });

            Assert.That(fixture.monsterCountRangePerWave.min, Is.EqualTo(6));
            Assert.That(fixture.monsterCountRangePerWave.max, Is.EqualTo(4));
        }

        [Test]
        public void SetPathValue_WritesValueTypeBackToCollectionSlot()
        {
            var fixture = new NestedValueFixture();
            fixture.ranges.Add(new RangeFixture { min = 2, max = 5 });
            MethodInfo setPath = GetPrivateMethod("SetPathValue");

            setPath.Invoke(null, new object[] { fixture, "ranges[0].max", 9 });

            Assert.That(fixture.ranges[0].min, Is.EqualTo(2));
            Assert.That(fixture.ranges[0].max, Is.EqualTo(9));
        }

        [Test]
        public void UpdateGamePrefab_RenamesIdAndReadsBackTheNewIdentity()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string oldId = $"vmframework_pipeline_rename_{suffix}_old";
            string newId = $"vmframework_pipeline_rename_{suffix}_new";
            const string testFolder = "Assets/__VMFrameworkPipelineTests";
            string wrapperPath = $"{testFolder}/RenameGamePrefab_{suffix}.asset";
            string generalSettingPath =
                $"{ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH}/" +
                $"RenameGamePrefabGeneralSetting_{suffix}.asset";

            if (AssetDatabase.IsValidFolder(testFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets", "__VMFrameworkPipelineTests");
            }

            var wrapper = UnityEngine.ScriptableObject.CreateInstance<GamePrefabSingleWrapper>();
            wrapper.InitGamePrefabs(new IGamePrefab[]
            {
                new RenameFixtureGamePrefab { id = oldId }
            });
            AssetDatabase.CreateAsset(wrapper, wrapperPath);
            var generalSetting =
                UnityEngine.ScriptableObject.CreateInstance<
                    RenameFixtureGamePrefabGeneralSetting>();
            generalSetting.initialGamePrefabProviders.Add(wrapper);
            AssetDatabase.CreateAsset(generalSetting, generalSettingPath);
            AssetDatabase.SaveAssets();

            try
            {
                VMFrameworkFindGamePrefabResult original = VMFrameworkPipelineTools.FindGamePrefab(
                    new VMFrameworkFindGamePrefabRequest { Id = oldId });
                Assert.That(original.Total, Is.EqualTo(1));

                VMFrameworkUpdateGamePrefabResult result =
                    VMFrameworkPipelineGamePrefabTools.UpdateGamePrefab(
                    new VMFrameworkUpdateGamePrefabRequest
                    {
                        GamePrefab = original.GamePrefabs.Single(),
                        Operations = new List<VMFrameworkGamePrefabUpdateOperation>
                        {
                            new()
                            {
                                Type = VMFrameworkGamePrefabUpdateOperationKind.Set,
                                Path = "id",
                                Value = newId,
                            }
                        },
                    });

                Assert.That(result.GamePrefab.Id, Is.EqualTo(newId));
                Assert.That(result.PreviousId, Is.EqualTo(oldId));
                Assert.That(result.TerminalState, Is.EqualTo("committed"));
                Assert.That(result.CommitEvidence["semanticReadback"], Is.EqualTo(true));
                Assert.That(result.CommitEvidence["assetSha256"].ToString(), Is.Not.Empty);

                VMFrameworkFindGamePrefabResult oldMatches =
                    VMFrameworkPipelineTools.FindGamePrefab(
                        new VMFrameworkFindGamePrefabRequest { Id = oldId });
                VMFrameworkFindGamePrefabResult newMatches =
                    VMFrameworkPipelineTools.FindGamePrefab(
                        new VMFrameworkFindGamePrefabRequest { Id = newId });
                Assert.That(oldMatches.Total, Is.Zero);
                Assert.That(newMatches.Total, Is.EqualTo(1));
            }
            finally
            {
                AssetDatabase.DeleteAsset(generalSettingPath);
                AssetDatabase.DeleteAsset(wrapperPath);
                if (AssetDatabase.IsValidFolder(testFolder) &&
                    AssetDatabase.FindAssets("", new[] { testFolder }).Length == 0)
                {
                    AssetDatabase.DeleteAsset(testFolder);
                }
            }
        }

        private static MethodInfo GetPrivateMethod(string name)
        {
            MethodInfo method = typeof(VMFrameworkPipelineGamePrefabTools).GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
