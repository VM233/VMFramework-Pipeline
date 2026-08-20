#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VMUnityAutomation.Editor;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class VMFrameworkPipelineGamePrefabRollbackTests
    {
        [TearDown]
        public void TearDown()
        {
            VMFrameworkPipelineAssetSnapshotStore.FaultInjector = null;
        }

        [Test]
        public void InvalidUpdate_ReturnsVerifiedRollbackWithoutReplacingOriginalFailure()
        {
            using var fixture = new FixtureScope();

            VmProjectToolException exception = Assert.Throws<VmProjectToolException>(() =>
                ExecuteInvalidUpdate(fixture.Reference));

            Assert.That(exception.ErrorCode, Is.EqualTo("game_prefab_update_rolled_back"));
            Assert.That(exception.Details["terminalState"], Is.EqualTo("rolled_back"));
            Assert.That(exception.Details["rollbackVerified"], Is.EqualTo(true));
            Dictionary<string, object> original =
                RequireDictionary(exception.Details["originalError"]);
            Assert.That(original["message"].ToString(), Is.Not.Empty);
            Assert.That(VMFrameworkPipelineTools.FindGamePrefab(
                new VMFrameworkFindGamePrefabRequest { Id = fixture.Id }).Total, Is.EqualTo(1));
        }

        [Test]
        public void RestoreFailure_ReturnsRollbackFailedWithOriginalErrorAndRollbackErrors()
        {
            using var fixture = new FixtureScope();
            VMFrameworkPipelineAssetSnapshotStore.FaultInjector = boundary =>
                boundary == "before-asset-restore"
                    ? new IOException("injected wrapper restore failure")
                    : null;

            VmProjectToolException exception = Assert.Throws<VmProjectToolException>(() =>
                ExecuteInvalidUpdate(fixture.Reference));

            Assert.That(exception.ErrorCode, Is.EqualTo("rollback_failed"));
            Assert.That(exception.Details["terminalState"], Is.EqualTo("rollback_failed"));
            Assert.That(exception.Details["originalError"],
                Is.InstanceOf<Dictionary<string, object>>());
            Assert.That(exception.Details["rollbackErrors"], Is.InstanceOf<IList>());
            Assert.That(string.Join("\n", ((IList)exception.Details["rollbackErrors"])
                    .Cast<object>()), Does.Contain("injected wrapper restore failure"));
        }

        private static void ExecuteInvalidUpdate(VMFrameworkGamePrefabReference reference)
        {
            VMFrameworkPipelineGamePrefabTools.UpdateGamePrefab(
                new VMFrameworkUpdateGamePrefabRequest
                {
                    GamePrefab = reference,
                    Operations = new List<VMFrameworkGamePrefabUpdateOperation>
                    {
                        new()
                        {
                            Type = VMFrameworkGamePrefabUpdateOperationKind.Set,
                            Path = "missingMember",
                            Value = 7,
                        },
                    },
                });
        }

        private static Dictionary<string, object> RequireDictionary(object value)
        {
            Assert.That(value, Is.InstanceOf<Dictionary<string, object>>());
            return (Dictionary<string, object>)value;
        }

        private sealed class FixtureScope : IDisposable
        {
            private const string TestFolder = "Assets/__VMFrameworkPipelineRollbackTests";
            private readonly string wrapperPath;
            private readonly string generalSettingPath;

            internal string Id { get; }
            internal VMFrameworkGamePrefabReference Reference { get; }

            internal FixtureScope()
            {
                string suffix = Guid.NewGuid().ToString("N");
                Id = "vmframework_pipeline_rollback_" + suffix;
                wrapperPath = TestFolder + "/RollbackGamePrefab_" + suffix + ".asset";
                generalSettingPath = ConfigurationPath.DEFAULT_GENERAL_SETTINGS_PATH +
                                     "/RollbackGamePrefabGeneralSetting_" + suffix + ".asset";
                if (!AssetDatabase.IsValidFolder(TestFolder))
                    AssetDatabase.CreateFolder("Assets", "__VMFrameworkPipelineRollbackTests");

                var wrapper = UnityEngine.ScriptableObject
                    .CreateInstance<GamePrefabSingleWrapper>();
                wrapper.InitGamePrefabs(new IGamePrefab[]
                {
                    new RenameFixtureGamePrefab { id = Id },
                });
                AssetDatabase.CreateAsset(wrapper, wrapperPath);
                var setting = UnityEngine.ScriptableObject.CreateInstance<
                    RenameFixtureGamePrefabGeneralSetting>();
                setting.initialGamePrefabProviders.Add(wrapper);
                AssetDatabase.CreateAsset(setting, generalSettingPath);
                AssetDatabase.SaveAssets();
                Reference = VMFrameworkPipelineTools.FindGamePrefab(
                    new VMFrameworkFindGamePrefabRequest { Id = Id }).GamePrefabs.Single();
            }

            public void Dispose()
            {
                AssetDatabase.DeleteAsset(generalSettingPath);
                AssetDatabase.DeleteAsset(wrapperPath);
                if (AssetDatabase.IsValidFolder(TestFolder) &&
                    AssetDatabase.FindAssets("", new[] { TestFolder }).Length == 0)
                    AssetDatabase.DeleteAsset(TestFolder);
            }
        }
    }
}
#endif
