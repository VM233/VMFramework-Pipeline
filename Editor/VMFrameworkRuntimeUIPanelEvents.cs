#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using VMUnityAutomation.Editor;
using VMFramework.UI;

namespace VMFramework.Pipeline.Editor
{
    internal static class VMFrameworkRuntimeUIPanelEvents
    {
        private static readonly Dictionary<string, long> Sequences =
            new(StringComparer.Ordinal);
        private static readonly HashSet<IUIPanel> TrackedPanels =
            new(ReferenceComparer.Instance);
        private static UIPanelManager subscribedManager;
        private static long sequence;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static void EnsureManagerSubscription()
        {
            UIPanelManager manager = UIPanelManager.Instance;
            if (ReferenceEquals(manager, subscribedManager))
                return;
            if (subscribedManager != null)
                subscribedManager.OnPanelCreatedEvent -= Track;
            subscribedManager = manager;
            if (subscribedManager != null)
                subscribedManager.OnPanelCreatedEvent += Track;
        }

        internal static void Track(IUIPanel panel)
        {
            if (panel == null || !TrackedPanels.Add(panel))
                return;
            panel.OnOpen += OnOpen;
            panel.OnPostClose += OnPostClose;
            panel.OnDestruct += OnDestruct;
        }

        internal static long GetSequence(string panelID, string objectID,
            string eventName)
        {
            long byPanel = Sequences.TryGetValue(
                BuildPanelKey(panelID, eventName), out long panelSequence)
                ? panelSequence
                : 0;
            long byObject = !string.IsNullOrWhiteSpace(objectID) &&
                            Sequences.TryGetValue(
                                BuildObjectKey(objectID, eventName),
                                out long objectSequence)
                ? objectSequence
                : 0;
            return Math.Max(byPanel, byObject);
        }

        private static void OnOpen(IUIPanel panel)
        {
            Record(panel, "open");
        }

        private static void OnPostClose(IUIPanel panel)
        {
            Record(panel, "post-close");
        }

        private static void Record(IUIPanel panel, string eventName)
        {
            long current = ++sequence;
            Sequences[BuildPanelKey(panel.id, eventName)] = current;
            string objectID = GetObjectID(panel);
            if (!string.IsNullOrWhiteSpace(objectID))
                Sequences[BuildObjectKey(objectID, eventName)] = current;
        }

        private static void OnDestruct(IUIPanel panel)
        {
            panel.OnOpen -= OnOpen;
            panel.OnPostClose -= OnPostClose;
            panel.OnDestruct -= OnDestruct;
            TrackedPanels.Remove(panel);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode &&
                state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            if (subscribedManager != null)
                subscribedManager.OnPanelCreatedEvent -= Track;
            subscribedManager = null;
            foreach (IUIPanel panel in TrackedPanels.ToList())
                OnDestruct(panel);
            TrackedPanels.Clear();
            Sequences.Clear();
            sequence = 0;
        }

        private static string BuildPanelKey(string panelID, string eventName) =>
            $"panel:{panelID}:{eventName}";

        private static string BuildObjectKey(string objectID, string eventName) =>
            $"object:{objectID}:{eventName}";

        private static string GetObjectID(IUIPanel panel) =>
            panel is UnityEngine.Object unityObject ? VmObjectId.Get(unityObject) : "";

        private sealed class ReferenceComparer : IEqualityComparer<IUIPanel>
        {
            internal static readonly ReferenceComparer Instance = new();

            public bool Equals(IUIPanel x, IUIPanel y) => ReferenceEquals(x, y);

            public int GetHashCode(IUIPanel obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
