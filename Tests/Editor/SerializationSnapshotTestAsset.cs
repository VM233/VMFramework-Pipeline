using System;
using UnityEngine;
using UnityEngine.Localization;

namespace VMFramework.Pipeline.Editor.Tests
{
    public sealed class SerializationSnapshotTestAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Node
        {
            public string text;
            [SerializeReference] public Node next;
        }

        [SerializeReference] public Node first;
        [SerializeReference] public Node second;
        public UnityEngine.Object reference;
        public Vector3 position;
        public Color color;
        public Rect rect;
        public Gradient gradient = new();
        public AnimationCurve curve = new();
        public LocalizedString localizedByName = new();
        public LocalizedString localizedByGuid = new();
        [NonSerialized] public int runtimeValue = 5;
    }
}
