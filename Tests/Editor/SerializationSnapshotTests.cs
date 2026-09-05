using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VMUnityAutomation.Editor;
using Object = UnityEngine.Object;

namespace VMFramework.Pipeline.Editor.Tests
{
    public sealed class SerializationSnapshotTests
    {
        private string assetDirectory;
        private string snapshotDirectory;

        [Serializable]
        public sealed class Node
        {
            public string text;
            [SerializeReference] public Node next;
        }

        public sealed class Host : ScriptableObject
        {
            [SerializeReference] public Node first;
            [SerializeReference] public Node second;
            public Object reference;
            public Vector3 position;
            public Color color;
            public Rect rect;
            public Gradient gradient = new();
            public AnimationCurve curve = new();
        }

        [SetUp]
        public void SetUp()
        {
            string identity = Guid.NewGuid().ToString("N");
            assetDirectory = "Assets/__SerializationSnapshotTests_" + identity;
            snapshotDirectory = "Temp/SerializationSnapshotTests_" + identity;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(assetDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(assetDirectory);
            if (Directory.Exists(snapshotDirectory)) Directory.Delete(snapshotDirectory, true);
        }

        [Test]
        public void Apply_RoundTripsSharedCyclesNativeValuesAndUnityReferences()
        {
            Host asset = ScriptableObject.CreateInstance<Host>();
            Host referencedAsset = ScriptableObject.CreateInstance<Host>();
            AssetDatabase.CreateAsset(referencedAsset, assetDirectory + "/Referenced.asset");
            asset.first = new Node { text = "Captured" };
            asset.first.next = asset.first;
            asset.second = asset.first;
            asset.reference = referencedAsset;
            asset.position = new Vector3(1.25f, -7.5f, 12f);
            asset.color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
            asset.rect = new Rect(-2, 3, 4, 5);
            asset.gradient.SetKeys(new[] { new GradientColorKey(Color.red, 0),
                new GradientColorKey(Color.blue, 1) }, new[] { new GradientAlphaKey(0.25f, 0),
                new GradientAlphaKey(0.75f, 1) });
            asset.curve = AnimationCurve.Linear(0, 2, 1, 9);
            string path = assetDirectory + "/Host.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            var request = new VMFrameworkSerializationSnapshotRequest
            {
                AssetPaths = new List<string> { path }, SnapshotDirectory = snapshotDirectory
            };
            VMFrameworkSerializationSnapshotTool.Capture(request);
            asset.first.text = "Changed only in memory";
            var result = VMFrameworkSerializationSnapshotTool.Apply(request);
            Host reloaded = AssetDatabase.LoadAssetAtPath<Host>(path);
            Assert.That(result.VerifiedAssetPaths, Is.EqualTo(new[] { path }));
            Assert.That(reloaded.first.text, Is.EqualTo("Captured"));
            Assert.That(reloaded.second, Is.SameAs(reloaded.first));
            Assert.That(reloaded.first.next, Is.SameAs(reloaded.first));
            Assert.That(reloaded.reference, Is.EqualTo(referencedAsset));
            Assert.That(reloaded.position, Is.EqualTo(new Vector3(1.25f, -7.5f, 12f)));
            Assert.That(reloaded.rect, Is.EqualTo(new Rect(-2, 3, 4, 5)));
            Assert.That(reloaded.gradient.Evaluate(0).a, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(reloaded.curve.Evaluate(1), Is.EqualTo(9f));
        }

        [Test]
        public void Apply_RejectsFilesChangedAfterCapture()
        {
            Host asset = ScriptableObject.CreateInstance<Host>();
            string path = assetDirectory + "/Changed.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            var request = new VMFrameworkSerializationSnapshotRequest
            {
                AssetPaths = new List<string> { path }, SnapshotDirectory = snapshotDirectory
            };
            VMFrameworkSerializationSnapshotTool.Capture(request);
            asset.position = Vector3.one;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            VmProjectToolException error = Assert.Throws<VmProjectToolException>(
                () => VMFrameworkSerializationSnapshotTool.Apply(request));
            Assert.That(error.ErrorCode, Is.EqualTo("serialization_source_changed"));
            Assert.That(asset.position, Is.EqualTo(Vector3.one));
        }
    }
}
