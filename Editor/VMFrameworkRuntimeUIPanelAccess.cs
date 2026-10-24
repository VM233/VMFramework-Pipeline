using VMFramework.GameLogicArchitecture;
using VMFramework.UI;
using VMUnityAutomation.Editor;

namespace VMFramework.Pipeline.Editor
{
    internal static class VMFrameworkRuntimeUIPanelAccess
    {
        internal static IUIPanel Open(UIPanelManager manager, string panelID)
        {
            if (!GamePrefabManager.TryGetGamePrefab<IUIPanelConfig>(panelID, out var config))
                throw new VmProjectToolException("ui_panel_not_found",
                    $"UIPanel config '{panelID}' is not registered.");
            if (config.IsUnique && !manager.TryGetUniquePanel(panelID, out IUIPanel _))
                throw new VmProjectToolException("ui_panel_initialization_pending",
                    $"Unique UIPanel '{panelID}' has not been published by UniqueUIPanelCreationInitializer. " +
                    "Wait for native game initialization before opening or binding it.");
            return manager.GetAndOpen(panelID);
        }
    }
}
