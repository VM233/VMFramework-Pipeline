using System;
using System.Collections.Generic;
using System.Linq;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor
{
    public static class VMFrameworkGamePrefabAuthoring
    {
        public static VMFrameworkAddGamePrefabResult CreateOrReplace(
            VMFrameworkGamePrefabAuthoringRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var warnings = new List<string>();
            VMFrameworkPipelineTools.RefreshGamePrefabRegistry();

            var existingInfos = VMFrameworkPipelineTools.FindGamePrefabInfos(
                request.Id, null, null, int.MaxValue);
            if (existingInfos.Count > 0 && request.Overwrite == false)
            {
                throw new InvalidOperationException(
                    $"GamePrefab id '{request.Id}' already exists in: " +
                    string.Join(", ", existingInfos.Select(info => info.wrapperPath)));
            }

            IGamePrefab gamePrefab = VMFrameworkPipelineTools.CreateGamePrefab(
                request.Id, request.GamePrefabType, request.SerializedValues, warnings);
            GamePrefabGeneralSetting generalSetting =
                VMFrameworkPipelineTools.ResolveGamePrefabGeneralSetting(gamePrefab);

            GamePrefabWrapper wrapper;
            bool created;
            bool replaced;
            if (existingInfos.Count > 0)
            {
                if (existingInfos.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"GamePrefab id '{request.Id}' exists in multiple wrappers. Refusing to overwrite.");
                }

                var existingInfo = existingInfos[0];
                if (!(existingInfo.wrapper is GamePrefabSingleWrapper singleWrapper))
                {
                    throw new InvalidOperationException(
                        $"Existing wrapper '{existingInfo.wrapperPath}' is not a GamePrefabSingleWrapper.");
                }

                if (string.IsNullOrWhiteSpace(request.AssetName) == false)
                    warnings.Add("assetName is ignored when overwriting an existing GamePrefab.");

                singleWrapper.InitGamePrefabs(new[] { gamePrefab });
                wrapper = singleWrapper;
                created = false;
                replaced = true;
            }
            else
            {
                wrapper = VMFrameworkPipelineTools.CreateWrapper(
                    gamePrefab, generalSetting, request.AssetName);
                created = true;
                replaced = false;
            }

            VMFrameworkPipelineTools.RegisterWrapper(generalSetting, wrapper);
            wrapper = VMFrameworkPipelineTools.SaveAndRefresh(wrapper, generalSetting);
            VMFrameworkPipelineTools.ValidateWrapperContainsGamePrefab(wrapper, request.Id);

            return new VMFrameworkAddGamePrefabResult
            {
                GamePrefab = VMFrameworkPipelineTools.CreateGamePrefabReference(
                    gamePrefab, wrapper, generalSetting),
                Created = created,
                Replaced = replaced,
                Registered = generalSetting.initialGamePrefabProviders.Contains(wrapper),
                Warnings = warnings,
            };
        }
    }
}
