#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace VMFramework.Pipeline.Editor
{
    [Serializable]
    internal sealed class VMFrameworkPipelineProjectConfiguration
    {
        internal const string ConfigPath = "ProjectSettings/VMFrameworkPipelineSettings.json";
        internal const int CurrentSchemaVersion = 1;

        [SerializeField]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField]
        private GameTagValidationConfiguration gameTagValidation = new();

        [NonSerialized]
        internal bool Found;

        [NonSerialized]
        internal bool Valid = true;

        [NonSerialized]
        internal string Error = "";

        internal int SchemaVersion => schemaVersion;

        internal bool IncludeMissingGameTagTranslations
        {
            get => gameTagValidation.includeMissingTranslations;
            set => gameTagValidation.includeMissingTranslations = value;
        }

        internal bool IncludeGamePrefabTagReferences
        {
            get => gameTagValidation.includeGamePrefabReferences;
            set => gameTagValidation.includeGamePrefabReferences = value;
        }

        internal static VMFrameworkPipelineProjectConfiguration Load()
        {
            var configuration = new VMFrameworkPipelineProjectConfiguration();
            string path = GetFullPath();
            if (!File.Exists(path))
                return configuration;

            configuration.Found = true;
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), configuration);
                if (configuration.schemaVersion != CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported schemaVersion {configuration.schemaVersion}. Expected {CurrentSchemaVersion}.");
                }

                if (configuration.gameTagValidation == null)
                    throw new InvalidDataException("gameTagValidation must be a JSON object.");
            }
            catch (Exception ex)
            {
                configuration.Valid = false;
                configuration.Error = ex.Message;
            }

            return configuration;
        }

        internal void Save()
        {
            schemaVersion = CurrentSchemaVersion;
            gameTagValidation ??= new GameTagValidationConfiguration();

            string path = GetFullPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "ProjectSettings");
            File.WriteAllText(path, JsonUtility.ToJson(this, true) + Environment.NewLine);
            Found = true;
            Valid = true;
            Error = "";
        }

        internal static string GetFullPath()
        {
            return Path.GetFullPath(ConfigPath);
        }

        [Serializable]
        private sealed class GameTagValidationConfiguration
        {
            public bool includeMissingTranslations = true;
            public bool includeGamePrefabReferences = true;
        }
    }
}
#endif
