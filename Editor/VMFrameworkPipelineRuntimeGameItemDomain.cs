#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using VMUnityAutomation.Editor;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor
{
    internal static class VMFrameworkPipelineRuntimeGameItemDomain
    {
        private static IReadOnlyList<IVMFrameworkPipelineRuntimeGameItemDomainAdapter> adapters;

        internal static IVMFrameworkPipelineRuntimeGameItemDomainAdapter GetRequiredAdapter(
            IGameItem gameItem)
        {
            List<IVMFrameworkPipelineRuntimeGameItemDomainAdapter> matches = GetAdapters()
                .Where(adapter => adapter.CanHandle(gameItem))
                .OrderByDescending(adapter => adapter.Priority)
                .ThenBy(adapter => adapter.GetType().FullName, StringComparer.Ordinal)
                .ToList();
            if (matches.Count == 0)
            {
                throw new VmProjectToolException(
                    "runtime_game_item_domain_adapter_not_found",
                    $"No project domain adapter handles runtime GameItem '{gameItem?.GetType().FullName}'.");
            }

            if (matches.Count > 1 && matches[0].Priority == matches[1].Priority)
            {
                throw new VmProjectToolException(
                    "runtime_game_item_domain_adapter_ambiguous",
                    $"Multiple project domain adapters with priority {matches[0].Priority} handle " +
                    $"runtime GameItem '{gameItem?.GetType().FullName}': " +
                    string.Join(", ", matches
                        .Where(adapter => adapter.Priority == matches[0].Priority)
                        .Select(adapter => adapter.GetType().FullName)));
            }

            return matches[0];
        }

        internal static bool TryGetAdapter(IGameItem gameItem,
            out IVMFrameworkPipelineRuntimeGameItemDomainAdapter adapter)
        {
            try
            {
                adapter = GetRequiredAdapter(gameItem);
                return true;
            }
            catch (VmProjectToolException exception)
                when (exception.ErrorCode == "runtime_game_item_domain_adapter_not_found")
            {
                adapter = null;
                return false;
            }
        }

        internal static IReadOnlyList<IVMFrameworkPipelineRuntimeGameItemDomainAdapter> GetAdapters()
        {
            if (adapters != null)
                return adapters;

            var discovered = new List<IVMFrameworkPipelineRuntimeGameItemDomainAdapter>();
            foreach (Type type in TypeCache
                         .GetTypesDerivedFrom<IVMFrameworkPipelineRuntimeGameItemDomainAdapter>()
                         .Where(type => !type.IsAbstract && !type.IsInterface)
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Runtime GameItem domain adapter '{type.FullName}' needs a public parameterless constructor.");
                }

                discovered.Add(
                    (IVMFrameworkPipelineRuntimeGameItemDomainAdapter)Activator.CreateInstance(type));
            }

            adapters = discovered;
            return adapters;
        }
    }
}
#endif
