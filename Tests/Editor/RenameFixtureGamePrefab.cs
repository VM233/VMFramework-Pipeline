using System;
using UnityEngine;
using VMFramework.GameLogicArchitecture;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Serializable]
    public sealed class RenameFixtureGamePrefab : GamePrefab
    {
        public Rect footprint;
        public double precision;
    }
}
