#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using VMFramework.Core;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    public class VMFrameworkGamePrefabValidationToolTests
    {
        [Test]
        public void CreatePrefabReferenceIssue_ReportsNullAndDestroyedPrefabs()
        {
            var gamePrefab = new FixtureGamePrefab
            {
                id = "validation_fixture",
            };

            VMFrameworkGamePrefabValidationIssue missingIssue =
                VMFrameworkGamePrefabValidationTool.CreatePrefabReferenceIssue(
                    "Assets/ValidationFixture.asset", gamePrefab);

            Assert.That(missingIssue, Is.Not.Null);
            Assert.That(missingIssue.Code, Is.EqualTo("missing_prefab_reference"));
            Assert.That(missingIssue.GamePrefabId, Is.EqualTo("validation_fixture"));
            Assert.That(missingIssue.Member, Is.EqualTo("IPrefabProvider.Prefab"));

            var prefab = new GameObject("Validation Fixture Prefab");
            try
            {
                gamePrefab.Prefab = prefab;
                Assert.That(VMFrameworkGamePrefabValidationTool.CreatePrefabReferenceIssue(
                    "Assets/ValidationFixture.asset", gamePrefab), Is.Null);

                UnityEngine.Object.DestroyImmediate(prefab);
                prefab = null;
                Assert.That(VMFrameworkGamePrefabValidationTool.CreatePrefabReferenceIssue(
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

        [Serializable]
        private sealed class FixtureGamePrefab : GamePrefab, IPrefabProvider
        {
            public GameObject Prefab { get; set; }
        }
    }
}
#endif
