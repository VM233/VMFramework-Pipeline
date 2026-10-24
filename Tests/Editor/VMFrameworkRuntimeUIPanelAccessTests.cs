using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VMFramework.GameLogicArchitecture;
using VMFramework.UI;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class VMFrameworkRuntimeUIPanelAccessTests
    {
        [Test]
        public void UnpublishedUniquePanelRejectsWithoutRentingOrRegistering()
        {
            string id = "cli_unique_" + Guid.NewGuid().ToString("N");
            var config = new UIPanelConfig { id = id, isUnique = true };
            var owner = new GameObject("CLI Panel Ownership Test") { hideFlags = HideFlags.HideAndDontSave };
            var manager = owner.AddComponent<UIPanelManager>();
            int created = 0;
            manager.OnPanelCreatedEvent += _ => created++;
            Assert.That(GamePrefabManager.RegisterGamePrefab(config), Is.True);
            try
            {
                var error = Assert.Throws<VmProjectToolException>(() => VMFrameworkRuntimeUIPanelAccess.Open(manager, id));
                Assert.That(error.ErrorCode, Is.EqualTo("ui_panel_initialization_pending"));
                Assert.That(created, Is.Zero);
                Assert.That(manager.TryGetUniquePanel(id, out IUIPanel _), Is.False);
            }
            finally
            {
                GamePrefabManager.UnregisterGamePrefab(config);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ExactNonPanelObjectCannotFallThroughToCreation()
        {
            var target = new GameObject("CLI Exact Panel Selector Test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var resolve = typeof(VMFrameworkRuntimeUIPanelTool).GetMethod("ResolvePanel",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var error = Assert.Throws<TargetInvocationException>(() => resolve.Invoke(null,
                    new object[] { null, "profile_ui", VmObjectId.Get(target), false, true }));
                Assert.That(error.InnerException, Is.TypeOf<VmProjectToolException>());
                Assert.That(((VmProjectToolException)error.InnerException).ErrorCode, Is.EqualTo("ui_panel_not_found"));
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
