#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VMFramework.Core;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    public class VMFrameworkGamePrefabValidationToolTests
    {
        [Test]
        public void CreateRegistrationIssue_RequiresExactRuntimeReachability()
        {
            var registered = new FixtureGamePrefab
            {
                id = "registered_fixture",
            };
            var runtimeGamePrefabs = new HashSet<IGamePrefab>
            {
                registered,
            };

            Assert.That(VMFrameworkGamePrefabValidationTool.CreateRegistrationIssue(
                "Assets/RegisteredFixture.asset", registered,
                runtimeGamePrefabs), Is.Null);

            var orphan = new FixtureGamePrefab
            {
                id = registered.id,
            };
            VMFrameworkGamePrefabValidationIssue issue =
                VMFrameworkGamePrefabValidationTool.CreateRegistrationIssue(
                    "Assets/OrphanFixture.asset", orphan,
                    runtimeGamePrefabs);

            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.Code, Is.EqualTo("unregistered_game_prefab"));
            Assert.That(issue.GamePrefabId, Is.EqualTo("registered_fixture"));
            Assert.That(issue.Member,
                Is.EqualTo("IGamePrefabsProvider.GetGamePrefabs"));
        }

        [Test]
        public void CreatePrefabContractIssue_ReportsNullAndDestroyedPrefabs()
        {
            var gamePrefab = new FixtureGamePrefab
            {
                id = "validation_fixture",
            };

            VMFrameworkGamePrefabValidationIssue missingIssue =
                VMFrameworkGamePrefabValidationTool.CreatePrefabContractIssue(
                    "Assets/ValidationFixture.asset", gamePrefab);

            Assert.That(missingIssue, Is.Not.Null);
            Assert.That(missingIssue.Code, Is.EqualTo("missing_prefab_reference"));
            Assert.That(missingIssue.GamePrefabId, Is.EqualTo("validation_fixture"));
            Assert.That(missingIssue.Member, Is.EqualTo("IPrefabProvider.Prefab"));

            var prefab = new GameObject("Validation Fixture");
            try
            {
                gamePrefab.Prefab = prefab;
                Assert.That(VMFrameworkGamePrefabValidationTool.CreatePrefabContractIssue(
                    "Assets/ValidationFixture.asset", gamePrefab), Is.Null);

                UnityEngine.Object.DestroyImmediate(prefab);
                prefab = null;
                Assert.That(VMFrameworkGamePrefabValidationTool.CreatePrefabContractIssue(
                    "Assets/ValidationFixture.asset", gamePrefab)?.Code,
                    Is.EqualTo("missing_prefab_reference"));
            }
            finally
            {
                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }
            }
        }

        [Test]
        public void CreatePrefabContractIssue_AcceptsImplicitAndExplicitSuffixes()
        {
            var gamePrefab = new FixtureGamePrefab
            {
                id = "venom_glob_entity",
                IdSuffix = "entity",
            };
            var prefab = new GameObject("Venom Glob");
            try
            {
                gamePrefab.Prefab = prefab;
                Assert.That(VMFrameworkGamePrefabValidationTool
                    .CreatePrefabContractIssue(
                        "Assets/Venom Glob.asset", gamePrefab), Is.Null);

                gamePrefab.id = "poison_buff";
                gamePrefab.IdSuffix = "buff";
                prefab.name = "Poison Buff";
                Assert.That(VMFrameworkGamePrefabValidationTool
                    .CreatePrefabContractIssue(
                        "Assets/Poison Buff.asset", gamePrefab), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CreatePrefabContractIssue_ReportsNameAndIdMismatch()
        {
            var gamePrefab = new FixtureGamePrefab
            {
                id = "duplication_mirror_item",
                IdSuffix = "item",
            };
            var prefab = new GameObject("Ancestral Template");
            try
            {
                gamePrefab.Prefab = prefab;
                VMFrameworkGamePrefabValidationIssue issue =
                    VMFrameworkGamePrefabValidationTool
                        .CreatePrefabContractIssue(
                            "Assets/Duplication Mirror.asset", gamePrefab);

                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Code,
                    Is.EqualTo("prefab_name_id_mismatch"));
                Assert.That(issue.Member,
                    Is.EqualTo("IPrefabProvider.Prefab.name"));
                Assert.That(issue.PrefabName,
                    Is.EqualTo("Ancestral Template"));
                Assert.That(issue.ExpectedGamePrefabId,
                    Is.EqualTo("ancestral_template_item"));
                Assert.That(issue.GamePrefabId,
                    Is.EqualTo("duplication_mirror_item"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Serializable]
        private sealed class FixtureGamePrefab : GamePrefab, IPrefabProvider
        {
            public GameObject Prefab { get; set; }

            public string IdSuffix { get; set; }

            public override string IDSuffix => IdSuffix;
        }
    }
}
#endif
