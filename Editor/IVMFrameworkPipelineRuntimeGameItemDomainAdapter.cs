#if UNITY_EDITOR
using System.Collections.Generic;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor
{
    /// <summary>
    /// Project-owned extension point for facts that VMFramework does not own, such as factions or abilities.
    /// Implementations must read and mutate the domain's authoritative components instead of inferring facts
    /// from names, tags, prefab paths, or UI state.
    /// </summary>
    public interface IVMFrameworkPipelineRuntimeGameItemDomainAdapter
    {
        int Priority { get; }

        bool CanHandle(IGameItem gameItem);

        void SetFaction(IGameItem gameItem, string factionID);

        void AddInspectionSections(IGameItem gameItem,
            IDictionary<string, object> sections);
    }
}
#endif
