using System;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    public sealed class RenameFixtureGamePrefabGeneralSetting :
        GamePrefabGeneralSetting
    {
        public override Type BaseGamePrefabType =>
            typeof(RenameFixtureGamePrefab);
    }
}
