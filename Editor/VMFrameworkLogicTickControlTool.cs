#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VMUnityAutomation.Editor;
using VMFramework.Timers;

namespace VMFramework.Pipeline.Editor
{
    [VmProjectTool(ToolName,
        ShortName = "vmf/logic-tick-control",
        Description = "Query, start, stop, explicitly advance, or persistently wait for VMFramework Logic Tick state.",
        InputSchemaJson = InputSchema,
        OutputSchemaJson = OutputSchema,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState |
                      VmProjectToolSideEffect.ChangesRuntimeState |
                      VmProjectToolSideEffect.AdvancesLogicTicks,
        ErrorCodes = new[]
        {
            "requires_play_mode",
            "logic_tick_manager_unavailable",
            "persistent_job_required",
            "logic_tick_wait_timeout",
        },
        MutatesRuntime = true,
        RequiresPlayMode = true)]
    public sealed class VMFrameworkLogicTickControlTool : IVmPersistentProjectTool
    {
        private const int MaximumDirectAdvance = 32;
        private const string ToolName = "vmframework/logic-tick-control";
        private const string InputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\",\"enum\":[\"state\",\"start\",\"stop\",\"advance\",\"wait\"],\"description\":\"Logic Tick operation.\"}," +
            "\"ticks\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000000,\"default\":1,\"description\":\"Number of explicit Logic Ticks requested by advance.\"}," +
            "\"ticksPerStep\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":1000,\"default\":32,\"description\":\"Maximum explicit Logic Ticks per persistent Job step.\"}," +
            "\"targetTick\":{\"description\":\"Unsigned target Logic Tick for wait, as a JSON-safe decimal string or integer.\",\"oneOf\":[{\"type\":\"integer\",\"minimum\":0},{\"type\":\"string\",\"pattern\":\"^[0-9]+$\"}]}," +
            "\"advanceWhileWaiting\":{\"type\":\"boolean\",\"default\":false,\"description\":\"Explicitly advance Logic Ticks while waiting instead of only observing natural progression.\"}," +
            "\"timeoutMs\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":600000,\"default\":30000,\"description\":\"Persistent wait timeout in milliseconds.\"}" +
            "},\"required\":[\"action\"],\"additionalProperties\":false}";
        private const string OutputSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"action\":{\"type\":\"string\"}," +
            "\"tick\":{\"type\":\"string\",\"pattern\":\"^[0-9]+$\"}," +
            "\"tickGap\":{\"type\":\"number\",\"exclusiveMinimum\":0}," +
            "\"isTicking\":{\"type\":\"boolean\"}," +
            "\"advancedByTool\":{\"type\":\"integer\",\"minimum\":0}," +
            "\"waited\":{\"type\":\"boolean\"}," +
            "\"targetReached\":{\"type\":\"boolean\"}," +
            "\"actualSideEffects\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"uniqueItems\":true}" +
            "},\"required\":[\"action\",\"tick\",\"tickGap\",\"isTicking\",\"advancedByTool\",\"waited\",\"targetReached\",\"actualSideEffects\"],\"additionalProperties\":false}";

        public object Execute(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            RequirePlayMode();
            ILogicTickManager manager = GetManager();
            string action = GetString(args, "action");
            switch (action)
            {
                case "state":
                    return BuildResult(action, manager, 0, false, true,
                        new[] { "readsProjectState" });
                case "start":
                    manager.StartTick();
                    return BuildResult(action, manager, 0, false, true,
                        new[] { "readsProjectState", "changesRuntimeState" });
                case "stop":
                    manager.StopTick();
                    return BuildResult(action, manager, 0, false, true,
                        new[] { "readsProjectState", "changesRuntimeState" });
                case "advance":
                {
                    int ticks = GetInt(args, "ticks", 1);
                    if (ticks > MaximumDirectAdvance)
                    {
                        throw new VmProjectToolException("persistent_job_required",
                            $"Advancing more than {MaximumDirectAdvance} Logic Ticks must use runAsJob=true.");
                    }
                    Advance(manager, ticks);
                    return BuildResult(action, manager, ticks, false, true,
                        new[]
                        {
                            "readsProjectState",
                            "changesRuntimeState",
                            "advancesLogicTicks",
                        });
                }
                case "wait":
                    throw new VmProjectToolException("persistent_job_required",
                        "Logic Tick waits must use runAsJob=true.");
                default:
                    throw new VmProjectToolException("invalid_arguments",
                        "Unsupported logic-tick-control action.");
            }
        }

        public VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
            Dictionary<string, object> state)
        {
            args ??= new Dictionary<string, object>();
            state ??= new Dictionary<string, object>();
            RequirePlayMode();
            string action = GetString(args, "action");
            if (action == "advance")
                return AdvanceStep(args, state);
            if (action == "wait")
                return WaitStep(args, state);
            return VmProjectToolJobStep.Complete(Execute(args));
        }

        private static VmProjectToolJobStep AdvanceStep(
            IReadOnlyDictionary<string, object> args,
            IReadOnlyDictionary<string, object> state)
        {
            ILogicTickManager manager = GetManager();
            int requested = GetInt(args, "ticks", 1);
            int completed = state.TryGetValue("advancedByTool", out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : 0;
            int stepCount = Math.Min(GetInt(args, "ticksPerStep", 32),
                requested - completed);
            Advance(manager, stepCount);
            completed += stepCount;
            if (completed >= requested)
            {
                return VmProjectToolJobStep.Complete(BuildResult(
                    "advance", manager, completed, completed > stepCount, true,
                    new[]
                    {
                        "readsProjectState",
                        "changesRuntimeState",
                        "advancesLogicTicks",
                        "waitsAcrossEditorFrames",
                    }));
            }

            return VmProjectToolJobStep.Pending(
                new Dictionary<string, object>
                {
                    { "advancedByTool", completed },
                },
                (double)completed / requested,
                $"Advanced {completed} of {requested} requested Logic Ticks.",
                delayMilliseconds: 0);
        }

        private static VmProjectToolJobStep WaitStep(
            IReadOnlyDictionary<string, object> args,
            IReadOnlyDictionary<string, object> state)
        {
            ILogicTickManager manager = GetManager();
            if (!args.TryGetValue("targetTick", out object targetValue) ||
                targetValue == null)
            {
                throw new VmProjectToolException("invalid_arguments",
                    "targetTick is required for action=wait.");
            }

            ulong targetTick = ParseTick(targetValue, "targetTick");
            int advancedByTool = state.TryGetValue("advancedByTool",
                    out object advancedValue)
                ? Convert.ToInt32(advancedValue, CultureInfo.InvariantCulture)
                : 0;
            bool hasWaited = state.ContainsKey("startedAtTicks");
            if (manager.Tick >= targetTick)
            {
                return VmProjectToolJobStep.Complete(BuildResult(
                    "wait", manager, advancedByTool, hasWaited, true,
                    GetWaitSideEffects(advancedByTool)));
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
                throw new VmProjectToolException("logic_tick_wait_timeout",
                    $"Timed out after {timeoutMs} ms waiting for Logic Tick {targetTick}.",
                    details: new Dictionary<string, object>
                    {
                        { "currentTick", manager.Tick.ToString(CultureInfo.InvariantCulture) },
                        { "targetTick", targetTick.ToString(CultureInfo.InvariantCulture) },
                        { "advancedByTool", advancedByTool },
                    });
            }

            if (GetBool(args, "advanceWhileWaiting", false))
            {
                ulong remaining = targetTick - manager.Tick;
                int stepCount = (int)Math.Min(
                    (ulong)GetInt(args, "ticksPerStep", 32), remaining);
                Advance(manager, stepCount);
                advancedByTool += stepCount;
                if (manager.Tick >= targetTick)
                {
                    return VmProjectToolJobStep.Complete(BuildResult(
                        "wait", manager, advancedByTool, true, true,
                        GetWaitSideEffects(advancedByTool)));
                }
            }

            double progress = targetTick == 0
                ? 1
                : Math.Min(1d, (double)manager.Tick / targetTick);
            return VmProjectToolJobStep.Pending(
                new Dictionary<string, object>
                {
                    { "startedAtTicks", startedAtTicks },
                    { "advancedByTool", advancedByTool },
                },
                progress,
                $"Waiting for Logic Tick {targetTick}; current tick is {manager.Tick}.",
                delayMilliseconds: 16);
        }

        private static IReadOnlyList<string> GetWaitSideEffects(int advancedByTool)
        {
            var effects = new List<string>
            {
                "readsProjectState",
                "waitsAcrossEditorFrames",
            };
            if (advancedByTool > 0)
            {
                effects.Add("changesRuntimeState");
                effects.Add("advancesLogicTicks");
            }
            return effects;
        }

        private static void Advance(ILogicTickManager manager, int count)
        {
            for (int index = 0; index < count; index++)
                manager.IncreaseTick();
        }

        private static Dictionary<string, object> BuildResult(string action,
            ILogicTickManager manager, int advancedByTool, bool waited,
            bool targetReached, IEnumerable<string> actualSideEffects)
        {
            return new Dictionary<string, object>
            {
                { "action", action },
                { "tick", manager.Tick.ToString(CultureInfo.InvariantCulture) },
                { "tickGap", manager.TickGap },
                { "isTicking", manager.IsTicking },
                { "advancedByTool", advancedByTool },
                { "waited", waited },
                { "targetReached", targetReached },
                { "actualSideEffects", actualSideEffects.ToList() },
            };
        }

        private static ILogicTickManager GetManager()
        {
            ILogicTickManager manager = LogicTickManager.Instance;
            if (manager == null)
            {
                throw new VmProjectToolException("logic_tick_manager_unavailable",
                    "LogicTickManager is unavailable in the current Play Mode lifecycle.");
            }
            return manager;
        }

        private static void RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                throw new VmProjectToolException("requires_play_mode",
                    "Logic Tick control requires Play Mode.");
            }
        }

        private static ulong ParseTick(object value, string name)
        {
            if (value is string text &&
                ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
                    out ulong parsed))
            {
                return parsed;
            }
            try
            {
                return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or
                                             InvalidCastException or
                                             OverflowException)
            {
                throw new VmProjectToolException("invalid_arguments",
                    $"{name} must be a non-negative integer or decimal string.");
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

        private static bool GetBool(IReadOnlyDictionary<string, object> args,
            string key, bool fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                : fallback;
        }
    }
}
#endif
