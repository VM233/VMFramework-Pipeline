#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VMUnityAutomation.Editor;
using VMFramework.Procedure;

namespace VMFramework.Pipeline.Editor
{
    [VmProjectTool(ToolName,
        ShortName = "vmf/procedure-state",
        Description = "Query VMFramework Procedure state or persistently wait for an explicit set/loading-state contract.",
        InputSchemaJson = InputSchema,
        OutputSchemaJson = OutputSchema,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[]
        {
            "requires_play_mode",
            "procedure_manager_unavailable",
            "persistent_job_required",
            "procedure_wait_condition_required",
            "procedure_wait_timeout",
        },
        ReadOnly = true,
        RequiresPlayMode = true)]
    public sealed class VMFrameworkProcedureStateTool : IVmPersistentProjectTool
    {
        private const string ToolName = "vmframework/procedure-state";
        private const string InputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\",\"enum\":[\"state\",\"wait\"],\"description\":\"Query immediately or wait through a persistent Job.\"}," +
            "\"containsAll\":{\"type\":\"array\",\"description\":\"Every listed Procedure id must be active.\",\"items\":{\"type\":\"string\",\"minLength\":1},\"uniqueItems\":true}," +
            "\"containsAny\":{\"type\":\"array\",\"description\":\"At least one listed Procedure id must be active.\",\"items\":{\"type\":\"string\",\"minLength\":1},\"uniqueItems\":true}," +
            "\"notContains\":{\"type\":\"array\",\"description\":\"None of the listed Procedure ids may be active.\",\"items\":{\"type\":\"string\",\"minLength\":1},\"uniqueItems\":true}," +
            "\"exact\":{\"type\":\"array\",\"description\":\"The active Procedure-id set must equal this set.\",\"items\":{\"type\":\"string\",\"minLength\":1},\"uniqueItems\":true}," +
            "\"loading\":{\"type\":\"boolean\",\"description\":\"Optional required Procedure loading state.\"}," +
            "\"timeoutMs\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":600000,\"default\":30000,\"description\":\"Persistent wait timeout in milliseconds.\"}" +
            "},\"required\":[\"action\"],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\"}," +
            "\"currentProcedureIDs\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}," +
            "\"isLoading\":{\"type\":\"boolean\"}," +
            "\"matched\":{\"type\":\"boolean\"}," +
            "\"waited\":{\"type\":\"boolean\"}," +
            "\"actualSideEffects\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"uniqueItems\":true}" +
            "},\"required\":[\"action\",\"currentProcedureIDs\",\"isLoading\",\"matched\",\"waited\",\"actualSideEffects\"],\"additionalProperties\":false}";

        public object Execute(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            RequirePlayMode();
            string action = GetString(args, "action");
            if (action == "wait")
            {
                throw new VmProjectToolException("persistent_job_required",
                    "Procedure waits must be called with runAsJob=true so Unity can advance between observations.");
            }
            if (action != "state")
            {
                throw new VmProjectToolException("invalid_arguments",
                    "Unsupported procedure-state action.");
            }

            return BuildResult(action, GetSnapshot(), matched: true, waited: false,
                new[] { "readsProjectState" });
        }

        public VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
            Dictionary<string, object> state)
        {
            args ??= new Dictionary<string, object>();
            state ??= new Dictionary<string, object>();
            RequirePlayMode();
            string action = GetString(args, "action");
            if (action != "wait")
                return VmProjectToolJobStep.Complete(Execute(args));

            ProcedureWaitContract contract = ProcedureWaitContract.Parse(args);
            Snapshot snapshot = GetSnapshot();
            if (contract.Matches(snapshot))
            {
                return VmProjectToolJobStep.Complete(BuildResult(
                    action, snapshot, matched: true,
                    waited: state.ContainsKey("startedAtTicks"),
                    new[] { "readsProjectState", "waitsAcrossEditorFrames" }));
            }

            long startedAtTicks = state.TryGetValue("startedAtTicks",
                    out object startedValue)
                ? Convert.ToInt64(startedValue, CultureInfo.InvariantCulture)
                : DateTime.UtcNow.Ticks;
            int timeoutMs = GetInt(args, "timeoutMs", 30_000);
            double elapsedMs = TimeSpan.FromTicks(
                DateTime.UtcNow.Ticks - startedAtTicks).TotalMilliseconds;
            if (elapsedMs >= timeoutMs)
            {
                throw new VmProjectToolException("procedure_wait_timeout",
                    $"Timed out after {timeoutMs} ms waiting for the requested Procedure state.",
                    details: new Dictionary<string, object>
                    {
                        { "currentProcedureIDs", snapshot.ProcedureIDs },
                        { "isLoading", snapshot.IsLoading },
                    });
            }

            return VmProjectToolJobStep.Pending(
                new Dictionary<string, object>
                {
                    { "startedAtTicks", startedAtTicks },
                },
                elapsedMs / timeoutMs,
                "Waiting for the requested VMFramework Procedure state.",
                delayMilliseconds: 16);
        }

        private static Snapshot GetSnapshot()
        {
            ProcedureManager manager = ProcedureManager.Instance as ProcedureManager;
            if (manager == null)
            {
                throw new VmProjectToolException("procedure_manager_unavailable",
                    "ProcedureManager is unavailable in the current Play Mode lifecycle.");
            }

            return new Snapshot(
                (manager.CurrentProcedureIDs ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
                manager.IsLoading);
        }

        private static Dictionary<string, object> BuildResult(string action,
            Snapshot snapshot, bool matched, bool waited,
            IEnumerable<string> actualSideEffects)
        {
            return new Dictionary<string, object>
            {
                { "action", action },
                { "currentProcedureIDs", snapshot.ProcedureIDs },
                { "isLoading", snapshot.IsLoading },
                { "matched", matched },
                { "waited", waited },
                { "actualSideEffects", actualSideEffects.ToList() },
            };
        }

        private static void RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                throw new VmProjectToolException("requires_play_mode",
                    "Procedure state requires Play Mode.");
            }
        }

        private static string GetString(IReadOnlyDictionary<string, object> args,
            string key)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(IReadOnlyDictionary<string, object> args,
            string key, int fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : fallback;
        }

        private sealed class Snapshot
        {
            internal IReadOnlyList<string> ProcedureIDs { get; }
            internal bool IsLoading { get; }

            internal Snapshot(IReadOnlyList<string> procedureIDs, bool isLoading)
            {
                ProcedureIDs = procedureIDs;
                IsLoading = isLoading;
            }
        }

        private sealed class ProcedureWaitContract
        {
            private readonly IReadOnlyList<string> containsAll;
            private readonly IReadOnlyList<string> containsAny;
            private readonly IReadOnlyList<string> notContains;
            private readonly IReadOnlyList<string> exact;
            private readonly bool hasExact;
            private readonly bool? loading;

            private ProcedureWaitContract(IReadOnlyList<string> containsAll,
                IReadOnlyList<string> containsAny,
                IReadOnlyList<string> notContains,
                IReadOnlyList<string> exact, bool hasExact, bool? loading)
            {
                this.containsAll = containsAll;
                this.containsAny = containsAny;
                this.notContains = notContains;
                this.exact = exact;
                this.hasExact = hasExact;
                this.loading = loading;
            }

            internal static ProcedureWaitContract Parse(
                IReadOnlyDictionary<string, object> args)
            {
                IReadOnlyList<string> containsAll = GetStrings(args, "containsAll");
                IReadOnlyList<string> containsAny = GetStrings(args, "containsAny");
                IReadOnlyList<string> notContains = GetStrings(args, "notContains");
                bool hasExact = args.ContainsKey("exact");
                IReadOnlyList<string> exact = GetStrings(args, "exact");
                bool? loading = args.TryGetValue("loading", out object loadingValue)
                    ? Convert.ToBoolean(loadingValue, CultureInfo.InvariantCulture)
                    : null;
                if (containsAll.Count == 0 && containsAny.Count == 0 &&
                    notContains.Count == 0 && !hasExact && !loading.HasValue)
                {
                    throw new VmProjectToolException(
                        "procedure_wait_condition_required",
                        "Procedure wait requires at least one explicit state constraint.");
                }
                return new ProcedureWaitContract(containsAll, containsAny,
                    notContains, exact, hasExact, loading);
            }

            internal bool Matches(Snapshot snapshot)
            {
                var current = new HashSet<string>(
                    snapshot.ProcedureIDs, StringComparer.Ordinal);
                if (containsAll.Any(id => !current.Contains(id)))
                    return false;
                if (containsAny.Count > 0 && !containsAny.Any(current.Contains))
                    return false;
                if (notContains.Any(current.Contains))
                    return false;
                if (hasExact && !current.SetEquals(exact))
                    return false;
                return !loading.HasValue || loading.Value == snapshot.IsLoading;
            }

            private static IReadOnlyList<string> GetStrings(
                IReadOnlyDictionary<string, object> args, string key)
            {
                if (!args.TryGetValue(key, out object value) || value == null)
                    return Array.Empty<string>();
                if (value is string)
                {
                    throw new VmProjectToolException("invalid_arguments",
                        $"{key} must be an array of Procedure ids.");
                }
                if (value is not IEnumerable enumerable)
                {
                    throw new VmProjectToolException("invalid_arguments",
                        $"{key} must be an array of Procedure ids.");
                }

                return enumerable.Cast<object>()
                    .Where(item => item != null)
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }
}
#endif
