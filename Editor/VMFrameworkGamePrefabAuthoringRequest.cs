using System;
using System.Collections.Generic;

namespace VMFramework.Pipeline.Editor
{
    public sealed class VMFrameworkGamePrefabAuthoringRequest
    {
        public string Id { get; }

        public Type GamePrefabType { get; }

        public bool Overwrite { get; }

        public string AssetName { get; }

        public Dictionary<string, object> SerializedValues { get; }

        public VMFrameworkGamePrefabAuthoringRequest(string id, Type gamePrefabType,
            bool overwrite = false, string assetName = null,
            Dictionary<string, object> serializedValues = null)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("GamePrefab id is required.", nameof(id))
                : id.Trim();
            GamePrefabType = gamePrefabType ??
                             throw new ArgumentNullException(nameof(gamePrefabType));
            Overwrite = overwrite;
            AssetName = assetName;
            SerializedValues = serializedValues;
        }
    }
}
