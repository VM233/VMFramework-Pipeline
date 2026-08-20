#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using VMUnityAutomation.Editor;
using VMFramework.GameLogicArchitecture;
using VMFramework.Properties;
using VMFramework.UI;

namespace VMFramework.Pipeline.Editor
{
    [InitializeOnLoad]
    internal static class VMFrameworkRuntimeGameItemSessions
    {
        internal sealed class Session
        {
            internal string Token;
            internal string SessionKey;
            internal string GamePrefabID;
            internal string RequestFingerprint;
            internal IGameItem GameItem;
            internal IUIPanel BoundPanel;
            internal string BindName;
            internal bool OpenedPanel;
            internal bool ClosePanelOnCleanup;
            internal DateTime CreatedAtUtc;
        }

        private static readonly Dictionary<string, Session> SessionsByToken =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> TokensBySessionKey =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, object>>
            CompletedCleanupResults = new(StringComparer.Ordinal);

        static VMFrameworkRuntimeGameItemSessions()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static Session Create(Dictionary<string, object> args, out bool reused)
        {
            RequirePlayMode();
            string gamePrefabID = GetRequiredString(args, "gamePrefabID");
            string sessionKey = GetString(args, "sessionKey");
            string requestFingerprint = ComputeCreateRequestFingerprint(args);
            if (!string.IsNullOrWhiteSpace(sessionKey) &&
                TokensBySessionKey.TryGetValue(sessionKey, out string existingToken) &&
                SessionsByToken.TryGetValue(existingToken, out Session existing))
            {
                if (existing.GameItem == null || existing.GameItem.IsDestroyed)
                {
                    SessionsByToken.Remove(existingToken);
                    TokensBySessionKey.Remove(sessionKey);
                    CleanupSession(existing, throwOnError: false);
                }
                else
                {
                    if (!string.Equals(existing.RequestFingerprint,
                            requestFingerprint, StringComparison.Ordinal))
                    {
                        throw new VmProjectToolException(
                            "runtime_game_item_session_key_conflict",
                            $"Session key '{sessionKey}' was already used with different create arguments.");
                    }

                    reused = true;
                    return existing;
                }
            }

            var session = new Session
            {
                Token = Guid.NewGuid().ToString("N"),
                SessionKey = sessionKey ?? "",
                GamePrefabID = gamePrefabID,
                RequestFingerprint = requestFingerprint,
                BindName = GetString(args, "bindName", BindObjectsManager.GLOBAL_BIND_NAME),
                ClosePanelOnCleanup = GetBool(args, "closePanelOnCleanup", true),
                CreatedAtUtc = DateTime.UtcNow,
            };

            try
            {
                IGameItemManager manager = GameItemManager.Instance;
                if (manager == null)
                {
                    throw new VmProjectToolException("game_item_manager_unavailable",
                        "GameItemManager is unavailable in the current Play Mode lifecycle.");
                }

                session.GameItem = manager.Get(gamePrefabID);
                ApplyPlacement(session.GameItem, args);
                ApplyProperties(session.GameItem, GetDictionary(args, "properties"));

                string factionID = GetString(args, "factionID");
                if (!string.IsNullOrWhiteSpace(factionID))
                {
                    VMFrameworkPipelineRuntimeGameItemDomain
                        .GetRequiredAdapter(session.GameItem)
                        .SetFaction(session.GameItem, factionID);
                }

                BindToPanel(session, GetString(args, "panelID"),
                    GetBool(args, "openPanel", true));

                SessionsByToken.Add(session.Token, session);
                if (!string.IsNullOrWhiteSpace(session.SessionKey))
                    TokensBySessionKey.Add(session.SessionKey, session.Token);
                reused = false;
                return session;
            }
            catch
            {
                CleanupSession(session, throwOnError: false);
                throw;
            }
        }

        internal static Session GetRequired(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new VmProjectToolException("invalid_arguments",
                    "cleanupToken is required.");
            }
            if (!SessionsByToken.TryGetValue(token, out Session session))
            {
                throw new VmProjectToolException("runtime_game_item_session_not_found",
                    $"Runtime GameItem session '{token}' was not found.");
            }
            return session;
        }

        internal static bool TryGetGameItem(string token, out IGameItem gameItem)
        {
            if (!string.IsNullOrWhiteSpace(token) &&
                SessionsByToken.TryGetValue(token, out Session session))
            {
                gameItem = session.GameItem;
                return gameItem != null;
            }

            gameItem = null;
            return false;
        }

        internal static void BindToExistingPanel(Session session, IUIPanel panel,
            string bindName)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (panel?.BindObjectsManager == null)
            {
                throw new VmProjectToolException("ui_panel_has_no_bind_objects_manager",
                    $"UIPanel '{panel?.id}' has no BindObjectsManager.");
            }
            if (session.GameItem == null || session.GameItem.IsDestroyed)
            {
                throw new VmProjectToolException("runtime_game_item_session_not_live",
                    $"Runtime GameItem session '{session.Token}' no longer owns a live GameItem.");
            }

            if (session.BoundPanel?.BindObjectsManager != null)
            {
                session.BoundPanel.BindObjectsManager.RemoveObject(
                    session.BindName, session.GameItem);
            }

            session.BindName = string.IsNullOrWhiteSpace(bindName)
                ? BindObjectsManager.GLOBAL_BIND_NAME
                : bindName;
            panel.BindObjectsManager.AddObject(session.BindName, session.GameItem);
            session.BoundPanel = panel;
        }

        internal static bool TryGetToken(IGameItem gameItem, out string token)
        {
            Session match = SessionsByToken.Values.FirstOrDefault(
                session => ReferenceEquals(session.GameItem, gameItem));
            token = match?.Token;
            return match != null;
        }

        internal static Dictionary<string, object> Cleanup(string token)
        {
            if (!string.IsNullOrWhiteSpace(token) &&
                CompletedCleanupResults.TryGetValue(token,
                    out Dictionary<string, object> completed))
            {
                var reused = new Dictionary<string, object>(completed)
                {
                    ["reused"] = true,
                };
                return reused;
            }

            Session session = GetRequired(token);
            Dictionary<string, object> result =
                CleanupSession(session, throwOnError: true);
            SessionsByToken.Remove(token);
            if (!string.IsNullOrWhiteSpace(session.SessionKey))
                TokensBySessionKey.Remove(session.SessionKey);
            result["reused"] = false;
            CompletedCleanupResults[token] =
                new Dictionary<string, object>(result);
            return result;
        }

        internal static Dictionary<string, object> Describe(Session session)
        {
            return new Dictionary<string, object>
            {
                { "cleanupToken", session.Token },
                { "sessionKey", session.SessionKey },
                { "createdAt", session.CreatedAtUtc.ToString("O") },
                { "gamePrefabID", session.GamePrefabID },
                { "gameItem", VMFrameworkRuntimeGameItemInspector.Describe(session.GameItem) },
                { "binding", DescribeBinding(session) },
            };
        }

        private static Dictionary<string, object> DescribeBinding(Session session)
        {
            if (session.BoundPanel == null)
            {
                return new Dictionary<string, object>
                {
                    { "bound", false },
                };
            }

            return new Dictionary<string, object>
            {
                { "bound", session.BoundPanel.BindObjectsManager != null &&
                           session.BoundPanel.BindObjectsManager.ContainsObject(
                               session.BindName, session.GameItem) },
                { "panelID", session.BoundPanel.id },
                { "panelInstanceID", GetUnityObjectID(session.BoundPanel) },
                { "bindName", session.BindName },
                { "openedPanel", session.OpenedPanel },
                { "closePanelOnCleanup", session.ClosePanelOnCleanup },
            };
        }

        private static Dictionary<string, object> CleanupSession(Session session,
            bool throwOnError)
        {
            var errors = new List<string>();
            bool unbound = false;
            bool panelCloseRequested = false;
            bool returned = false;

            try
            {
                if (session.BoundPanel?.BindObjectsManager != null &&
                    session.GameItem != null)
                {
                    session.BoundPanel.BindObjectsManager.RemoveObject(
                        session.BindName, session.GameItem);
                    unbound = true;
                }
            }
            catch (Exception exception)
            {
                errors.Add("unbind: " + exception.Message);
            }

            try
            {
                if (session.OpenedPanel && session.ClosePanelOnCleanup &&
                    session.BoundPanel != null && UIPanelManager.Instance != null)
                {
                    panelCloseRequested = UIPanelManager.Instance.TryClose(session.BoundPanel);
                }
            }
            catch (Exception exception)
            {
                errors.Add("close-panel: " + exception.Message);
            }

            try
            {
                if (session.GameItem != null && !session.GameItem.IsDestroyed &&
                    GameItemManager.Instance != null)
                {
                    GameItemManager.Instance.Return(session.GameItem);
                    returned = true;
                }
            }
            catch (Exception exception)
            {
                errors.Add("return-game-item: " + exception.Message);
            }

            var result = new Dictionary<string, object>
            {
                { "cleanupToken", session.Token },
                { "unbound", unbound },
                { "panelCloseRequested", panelCloseRequested },
                { "gameItemReturned", returned },
                { "errors", errors },
            };
            if (errors.Count > 0 && throwOnError)
            {
                throw new VmProjectToolException("runtime_game_item_session_cleanup_failed",
                    string.Join(" ", errors), false, result);
            }
            return result;
        }

        private static void ApplyPlacement(IGameItem gameItem,
            IReadOnlyDictionary<string, object> args)
        {
            bool hasPosition = args.ContainsKey("position");
            string parentPath = GetString(args, "parentPath");
            if (!hasPosition && string.IsNullOrWhiteSpace(parentPath))
                return;

            if (gameItem is not Component component)
            {
                throw new VmProjectToolException("runtime_game_item_not_placeable",
                    $"GameItem '{gameItem.GetType().FullName}' has no Unity Transform.");
            }

            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                GameObject parent = FindSceneGameObject(parentPath);
                if (parent == null)
                {
                    throw new VmProjectToolException("parent_game_object_not_found",
                        $"Parent GameObject '{parentPath}' was not found in loaded scenes.");
                }
                component.transform.SetParent(parent.transform, worldPositionStays: true);
            }

            if (hasPosition)
                component.transform.position = ReadVector3(args["position"], "position");
        }

        private static void ApplyProperties(IGameItem gameItem,
            IReadOnlyDictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
                return;
            if (gameItem is not IPropertyManagerOwner owner || owner.PropertyManager == null)
            {
                throw new VmProjectToolException("runtime_game_item_has_no_property_manager",
                    $"GameItem '{gameItem.GetType().FullName}' does not expose a PropertyManager.");
            }

            foreach (KeyValuePair<string, object> pair in values)
            {
                if (!owner.PropertyManager.Properties.TryGetValue(pair.Key,
                        out IReadOnlyProperty readOnlyProperty))
                {
                    throw new VmProjectToolException("runtime_game_item_property_not_found",
                        $"Property '{pair.Key}' was not found on GameItem '{gameItem.id}'.");
                }
                if (readOnlyProperty is not IProperty property)
                {
                    throw new VmProjectToolException("runtime_game_item_property_read_only",
                        $"Property '{pair.Key}' on GameItem '{gameItem.id}' is read-only.");
                }

                Type valueType = GetPropertyValueType(readOnlyProperty);
                object converted = VMFrameworkPipelineGamePrefabTools.ConvertSerializedValue(
                    pair.Value, valueType, pair.Key);
                property.SetObjectValue(converted, initial: false);
            }
        }

        private static Type GetPropertyValueType(IReadOnlyProperty property)
        {
            Type propertyInterface = property.GetType().GetInterfaces().FirstOrDefault(type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IReadOnlyProperty<>));
            return propertyInterface?.GetGenericArguments()[0] ??
                   property.ObjectValue?.GetType() ??
                   typeof(object);
        }

        private static void BindToPanel(Session session, string panelID,
            bool openPanel)
        {
            if (string.IsNullOrWhiteSpace(panelID))
                return;
            UIPanelManager manager = UIPanelManager.Instance;
            if (manager == null)
            {
                throw new VmProjectToolException("ui_panel_manager_unavailable",
                    "UIPanelManager is unavailable in the current Play Mode lifecycle.");
            }

            IUIPanel panel = null;
            bool wasOpened = false;
            if (manager.TryGetUniquePanel(panelID, out IUIPanel uniquePanel))
            {
                panel = uniquePanel;
                wasOpened = panel.IsOpened;
            }
            if (panel == null && manager.TryGetOpenedPanels(panelID,
                    out IReadOnlyCollection<IUIPanel> openedPanels))
            {
                panel = openedPanels.FirstOrDefault();
                wasOpened = panel != null;
            }
            if (panel == null && !openPanel)
            {
                throw new VmProjectToolException("ui_panel_not_found",
                    $"UIPanel '{panelID}' has no existing instance to bind while openPanel=false.");
            }
            if (panel == null || openPanel && !panel.IsOpened)
                panel = manager.GetAndOpen(panelID);
            if (panel == null)
            {
                throw new VmProjectToolException("ui_panel_not_found",
                    $"UIPanel '{panelID}' could not be resolved or opened.");
            }
            if (panel.BindObjectsManager == null)
            {
                throw new VmProjectToolException("ui_panel_has_no_bind_objects_manager",
                    $"UIPanel '{panelID}' has no BindObjectsManager.");
            }

            BindToExistingPanel(session, panel, session.BindName);
            session.OpenedPanel = !wasOpened && panel.IsOpened;
        }

        private static void CleanupBeforeAssemblyReload()
        {
            CleanupAll();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                CleanupAll();
            }
        }

        private static void CleanupAll()
        {
            foreach (Session session in SessionsByToken.Values.ToList())
                CleanupSession(session, throwOnError: false);
            SessionsByToken.Clear();
            TokensBySessionKey.Clear();
            CompletedCleanupResults.Clear();
        }

        private static void RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                throw new VmProjectToolException("requires_play_mode",
                    "Runtime GameItem sessions require Play Mode.");
            }
        }

        private static string GetUnityObjectID(object value)
        {
            return value is UnityEngine.Object unityObject
                ? VmObjectId.Get(unityObject)
                : "";
        }

        private static Vector3 ReadVector3(object rawValue, string path)
        {
            if (rawValue is Dictionary<string, object> values)
            {
                return new Vector3(
                    GetFloat(values, "x", 0),
                    GetFloat(values, "y", 0),
                    GetFloat(values, "z", 0));
            }
            if (rawValue is IList list && list.Count is 2 or 3)
            {
                return new Vector3(
                    Convert.ToSingle(list[0], CultureInfo.InvariantCulture),
                    Convert.ToSingle(list[1], CultureInfo.InvariantCulture),
                    list.Count == 3
                        ? Convert.ToSingle(list[2], CultureInfo.InvariantCulture)
                        : 0);
            }

            throw new VmProjectToolException("invalid_arguments",
                $"{path} must be an {{x,y,z}} object or a two/three-number array.");
        }

        private static float GetFloat(IReadOnlyDictionary<string, object> values,
            string key, float fallback)
        {
            return values.TryGetValue(key, out object value) && value != null
                ? Convert.ToSingle(value, CultureInfo.InvariantCulture)
                : fallback;
        }

        private static GameObject FindSceneGameObject(string pathOrName)
        {
            foreach (GameObject root in Enumerable.Range(0,
                         UnityEngine.SceneManagement.SceneManager.sceneCount)
                     .Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt)
                     .Where(scene => scene.isLoaded)
                     .SelectMany(scene => scene.GetRootGameObjects()))
            {
                if (string.Equals(root.name, pathOrName, StringComparison.Ordinal) ||
                    string.Equals(VMFrameworkPipelineTools.GetGameObjectPath(root.transform),
                        pathOrName, StringComparison.Ordinal))
                {
                    return root;
                }

                Transform match = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform =>
                        string.Equals(transform.name, pathOrName,
                            StringComparison.Ordinal) ||
                        string.Equals(VMFrameworkPipelineTools.GetGameObjectPath(transform),
                            pathOrName, StringComparison.Ordinal));
                if (match != null)
                    return match.gameObject;
            }

            return null;
        }

        private static Dictionary<string, object> GetDictionary(
            IReadOnlyDictionary<string, object> args, string key)
        {
            return args.TryGetValue(key, out object value)
                ? value as Dictionary<string, object>
                : null;
        }

        private static string GetRequiredString(IReadOnlyDictionary<string, object> args,
            string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new VmProjectToolException("invalid_arguments", $"{key} is required.");
            return value;
        }

        private static string GetString(IReadOnlyDictionary<string, object> args,
            string key, string fallback = "")
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : fallback;
        }

        private static bool GetBool(IReadOnlyDictionary<string, object> args,
            string key, bool fallback)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return fallback;
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static string ComputeCreateRequestFingerprint(
            IReadOnlyDictionary<string, object> args)
        {
            var normalized = new Dictionary<string, object>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in args)
            {
                if (pair.Key == "action" || pair.Key == "sessionKey")
                    continue;
                normalized[pair.Key] = pair.Value;
            }

            string canonical = Canonicalize(normalized);
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical)))
                .Replace("-", "")
                .ToLowerInvariant();
        }

        private static string Canonicalize(object value)
        {
            if (value == null)
                return "null";
            if (value is string text)
                return "\"" + text.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"") + "\"";
            if (value is bool boolValue)
                return boolValue ? "true" : "false";
            if (value is IDictionary dictionary)
            {
                var entries = new List<(string key, object value)>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key != null)
                        entries.Add((entry.Key.ToString(), entry.Value));
                }
                return "{" + string.Join(",", entries
                    .OrderBy(entry => entry.key, StringComparer.Ordinal)
                    .Select(entry => Canonicalize(entry.key) + ":" +
                                     Canonicalize(entry.value))) + "}";
            }
            if (value is IEnumerable enumerable)
            {
                return "[" + string.Join(",", enumerable.Cast<object>()
                    .Select(Canonicalize)) + "]";
            }
            if (value is IFormattable formattable)
            {
                return formattable.ToString(null,
                    CultureInfo.InvariantCulture);
            }
            return Canonicalize(value.ToString());
        }
    }

}
#endif
