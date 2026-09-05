using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using VMUnityAutomation.Editor;
using Object = UnityEngine.Object;
using Host = VMFramework.Pipeline.Editor.Tests.SerializationSnapshotTestAsset;
using Node = VMFramework.Pipeline.Editor.Tests.SerializationSnapshotTestAsset.Node;

namespace VMFramework.Pipeline.Editor.Tests
{
    public sealed class SerializationSnapshotTests
    {
        private string assetDirectory;
        private string snapshotDirectory;

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
            asset.localizedByName = new LocalizedString("NamedTable", "NamedEntry");
            Guid tableGuid = Guid.Parse("13b825a3-7f70-4baa-a64a-1cd07957d84b");
            asset.localizedByGuid = new LocalizedString(tableGuid, "GuidEntry");
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
            Assert.That(reloaded.localizedByName.TableReference.TableCollectionName, Is.EqualTo("NamedTable"));
            Assert.That(reloaded.localizedByGuid.TableReference.TableCollectionNameGuid, Is.EqualTo(tableGuid));
            Assert.That(reloaded.localizedByGuid.TableEntryReference.Key, Is.EqualTo("GuidEntry"));
        }

        [Test]
        public void Apply_MaterializesInlineNullsAndPreservesManagedNulls()
        {
            Host asset = ScriptableObject.CreateInstance<Host>();
            string path = assetDirectory + "/Nulls.asset";
            AssetDatabase.CreateAsset(asset, path);
            asset.localizedByName = null;
            asset.labels = null;
            asset.first = null;
            asset.managedNodes = new List<Node> { null };
            var request = new VMFrameworkSerializationSnapshotRequest
            {
                AssetPaths = new List<string> { path }, SnapshotDirectory = snapshotDirectory
            };
            VMFrameworkSerializationSnapshotTool.Capture(request);
            VMFrameworkSerializationSnapshotTool.Apply(request);
            Host reloaded = AssetDatabase.LoadAssetAtPath<Host>(path);
            Assert.That(reloaded.localizedByName, Is.Not.Null);
            Assert.That(reloaded.localizedByName.IsEmpty, Is.True);
            Assert.That(reloaded.labels, Is.Empty);
            Assert.That(reloaded.first, Is.Null);
            Assert.That(reloaded.reference, Is.Null);
            Assert.That(reloaded.managedNodes, Has.Count.EqualTo(1));
            Assert.That(reloaded.managedNodes[0], Is.Null);
        }

        [Test]
        public void Apply_LeavesExplicitlyTransientFieldsAtTheirRuntimeValue()
        {
            Host asset = ScriptableObject.CreateInstance<Host>();
            string path = assetDirectory + "/Transient.asset";
            AssetDatabase.CreateAsset(asset, path);
            var request = new VMFrameworkSerializationSnapshotRequest
            {
                AssetPaths = new List<string> { path }, SnapshotDirectory = snapshotDirectory
            };
            var result = VMFrameworkSerializationSnapshotTool.Capture(request);
            var snapshot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(result.SnapshotFiles[0]));
            snapshot["graph"]["fields"]["runtimeValue"] = 99;
            File.WriteAllText(result.SnapshotFiles[0], snapshot.ToString());
            VMFrameworkSerializationSnapshotTool.Apply(request);
            Assert.That(AssetDatabase.LoadAssetAtPath<Host>(path).runtimeValue, Is.EqualTo(5));
        }

        [Test]
        public void Capture_PreservesReadonlyAuthoringFields()
        {
            var asset = ScriptableObject.CreateInstance<SerializationReadonlyTestAsset>();
            string path = assetDirectory + "/Readonly.asset";
            AssetDatabase.CreateAsset(asset, path);
            var request = new VMFrameworkSerializationSnapshotRequest
            {
                AssetPaths = new List<string> { path }, SnapshotDirectory = snapshotDirectory
            };
            var result = VMFrameworkSerializationSnapshotTool.Capture(request);
            var snapshot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(result.SnapshotFiles[0]));
            Assert.That((int)snapshot["graph"]["fields"]["authoringValue"], Is.EqualTo(37));
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
