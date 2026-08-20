#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class VMFrameworkPipelineGamePrefabQueryTests
    {
        [Test]
        public void QueryGamePrefabConfigs_ComposesTypeTagsAndFieldProjection()
        {
            using var fixture = new FixtureScope();
            VMFrameworkQueryGamePrefabConfigsResult result =
                VMFrameworkPipelineGamePrefabQueryTools.QueryGamePrefabConfigs(
                    new VMFrameworkQueryGamePrefabConfigsRequest
                    {
                        GamePrefabType = typeof(QueryFixtureGamePrefab).FullName,
                        GameTagsAll = new List<string> { "negative_effect" },
                        GameTagsNone = new List<string> { "beneficial_effect" },
                        HasDescription = true,
                        Fields = new List<VMFrameworkGamePrefabConfigField>
                        {
                            VMFrameworkGamePrefabConfigField.GameTags,
                            VMFrameworkGamePrefabConfigField.Name,
                            VMFrameworkGamePrefabConfigField.Description,
                        },
                        Locales = new List<string> { "en-US" },
                    });

            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Count, Is.EqualTo(1));
            VMFrameworkGamePrefabConfigRecord record = result.GamePrefabs.Single();
            Assert.That(record.GamePrefab.Id, Is.EqualTo(fixture.NegativeId));
            Assert.That(record.GamePrefab.FullTypeName,
                Is.EqualTo(typeof(QueryFixtureGamePrefab).FullName));
            CollectionAssert.AreEqual(
                new[] { "negative_effect", "shared_effect" }, record.GameTags);
            Assert.That(record.Name.Table, Is.EqualTo("QueryFixture"));
            Assert.That(record.Name.Key, Is.EqualTo("NegativeName"));
            Assert.That(record.Name.Values, Is.Empty);
            Assert.That(record.Description.Table, Is.EqualTo("QueryFixture"));
            Assert.That(record.Description.Key, Is.EqualTo("NegativeDescription"));
            Assert.That(record.Description.Values, Is.Empty);
        }

        [Test]
        public void QueryGamePrefabConfigs_PaginatesIdentityOnlyResults()
        {
            using var fixture = new FixtureScope();
            VMFrameworkQueryGamePrefabConfigsResult firstPage =
                VMFrameworkPipelineGamePrefabQueryTools.QueryGamePrefabConfigs(
                    new VMFrameworkQueryGamePrefabConfigsRequest
                    {
                        GamePrefabType = typeof(QueryFixtureGamePrefab).FullName,
                        Limit = 1,
                    });

            Assert.That(firstPage.Total, Is.EqualTo(2));
            Assert.That(firstPage.Count, Is.EqualTo(1));
            Assert.That(firstPage.NextOffset, Is.EqualTo(1));
            Assert.That(firstPage.GamePrefabs[0].GameTags, Is.Null);
            Assert.That(firstPage.GamePrefabs[0].Name, Is.Null);
            Assert.That(firstPage.GamePrefabs[0].Description, Is.Null);
        }

        [Test]
        public void QueryGamePrefabConfigs_ModelsUnconfiguredNamesAsAbsent()
        {
            using var fixture = new FixtureScope();
            VMFrameworkQueryGamePrefabConfigsResult projected =
                VMFrameworkPipelineGamePrefabQueryTools.QueryGamePrefabConfigs(
                    new VMFrameworkQueryGamePrefabConfigsRequest
                    {
                        GamePrefabType = typeof(QueryFixtureGamePrefab).FullName,
                        Fields = new List<VMFrameworkGamePrefabConfigField>
                        {
                            VMFrameworkGamePrefabConfigField.Name,
                        },
                        Locales = new List<string> { "en-US" },
                    });
            VMFrameworkGamePrefabConfigRecord unnamed = projected.GamePrefabs
                .Single(record => record.GamePrefab.Id == fixture.UnnamedId);
            Assert.That(unnamed.Name, Is.Null);

            VMFrameworkQueryGamePrefabConfigsResult namedOnly =
                VMFrameworkPipelineGamePrefabQueryTools.QueryGamePrefabConfigs(
                    new VMFrameworkQueryGamePrefabConfigsRequest
                    {
                        GamePrefabType = typeof(QueryFixtureGamePrefab).FullName,
                        HasName = true,
                    });
            Assert.That(namedOnly.Total, Is.EqualTo(1));
            Assert.That(namedOnly.GamePrefabs.Single().GamePrefab.Id,
                Is.EqualTo(fixture.NegativeId));
        }

        [Test]
        public void QueryGamePrefabConfigs_RejectsLocalesWithoutTextProjection()
        {
            Assert.Throws<ArgumentException>(() =>
                VMFrameworkPipelineGamePrefabQueryTools.QueryGamePrefabConfigs(
                    new VMFrameworkQueryGamePrefabConfigsRequest
                    {
                        GamePrefabType = typeof(QueryFixtureGamePrefab).FullName,
                        Fields = new List<VMFrameworkGamePrefabConfigField>
                        {
                            VMFrameworkGamePrefabConfigField.GameTags,
                        },
                        Locales = new List<string> { "en-US" },
                    }));
        }

        private sealed class FixtureScope : IDisposable
        {
            private const string TestFolder = "Assets/__VMFrameworkPipelineQueryTests";
            private readonly string wrapperPath;
            private readonly string generalSettingPath;

            internal string NegativeId { get; }
            internal string UnnamedId { get; }

            internal FixtureScope()
            {
                string suffix = Guid.NewGuid().ToString("N");
                NegativeId = "query_negative_" + suffix;
                UnnamedId = "query_unnamed_" + suffix;
                wrapperPath = TestFolder + "/QueryGamePrefabs_" + suffix + ".asset";
                generalSettingPath = ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH +
                                     "/QueryGamePrefabGeneralSetting_" + suffix + ".asset";
                if (!AssetDatabase.IsValidFolder(TestFolder))
                    AssetDatabase.CreateFolder("Assets", "__VMFrameworkPipelineQueryTests");

                QueryFixtureGamePrefab negative = CreateLocalizedGamePrefab(
                    NegativeId, "NegativeName", "NegativeDescription", true,
                    "negative_effect", "shared_effect");
                QueryFixtureGamePrefab beneficial = CreateLocalizedGamePrefab(
                    UnnamedId, "BeneficialName", "", false,
                    "beneficial_effect", "shared_effect");
                beneficial.name = null;
                var wrapper = UnityEngine.ScriptableObject
                    .CreateInstance<GamePrefabMultipleWrapper>();
                wrapper.InitGamePrefabs(new IGamePrefab[] { negative, beneficial });
                AssetDatabase.CreateAsset(wrapper, wrapperPath);
                var setting = UnityEngine.ScriptableObject.CreateInstance<
                    QueryFixtureGamePrefabGeneralSetting>();
                setting.initialGamePrefabProviders.Add(wrapper);
                AssetDatabase.CreateAsset(setting, generalSettingPath);
                AssetDatabase.SaveAssets();
            }

            public void Dispose()
            {
                AssetDatabase.DeleteAsset(generalSettingPath);
                AssetDatabase.DeleteAsset(wrapperPath);
                if (AssetDatabase.IsValidFolder(TestFolder) &&
                    AssetDatabase.FindAssets("", new[] { TestFolder }).Length == 0)
                {
                    AssetDatabase.DeleteAsset(TestFolder);
                }
            }

            private static QueryFixtureGamePrefab CreateLocalizedGamePrefab(
                string id, string nameKey, string descriptionKey,
                bool hasDescription, params string[] gameTags)
            {
                var gamePrefab = new QueryFixtureGamePrefab
                {
                    id = id,
                    name = new LocalizedString(),
                    hasDescription = hasDescription,
                    description = new LocalizedString(),
                };
                gamePrefab.name.SetReference("QueryFixture", nameKey);
                if (hasDescription)
                    gamePrefab.description.SetReference("QueryFixture", descriptionKey);
                foreach (string gameTag in gameTags)
                    gamePrefab.gameTags.Add(gameTag);
                return gamePrefab;
            }
        }
    }
}
#endif
