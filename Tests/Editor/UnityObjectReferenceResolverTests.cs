using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class UnityObjectReferenceResolverTests
    {
        private const string TestFolder =
            "Assets/__VMFrameworkUnityObjectReferenceTests";
        private const string PrefabPath = TestFolder + "/Nested.prefab";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "__VMFrameworkUnityObjectReferenceTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }
        }

        [Test]
        public void PathOnlyPrefabReference_ResolvesMainRootGameObject()
        {
            CreateNestedPrefab();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var gameObjects = AssetDatabase.LoadAllAssetsAtPath(PrefabPath)
                .OfType<GameObject>()
                .ToArray();
            Assert.That(gameObjects.Length, Is.GreaterThan(1));

            var resolved = VMFrameworkUnityObjectReferenceResolver.Resolve(
                PrefabPath,
                typeof(GameObject),
                "prefab");

            Assert.That(resolved, Is.SameAs(root));
            Assert.That(resolved.name, Is.EqualTo("Root"));
        }

        [Test]
        public void IdentityDescriptor_ResolvesExactNestedGameObject()
        {
            CreateNestedPrefab();
            var child = AssetDatabase.LoadAllAssetsAtPath(PrefabPath)
                .OfType<GameObject>()
                .Single(value => value.name == "Child");
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    child,
                    out var guid,
                    out long fileId),
                Is.True);
            var descriptor = new Dictionary<string, object>
            {
                { "assetPath", PrefabPath },
                { "guid", guid },
                { "fileID", fileId }
            };

            var resolved = VMFrameworkUnityObjectReferenceResolver.Resolve(
                descriptor,
                typeof(GameObject),
                "prefab");

            Assert.That(resolved, Is.SameAs(child));
        }

        private static void CreateNestedPrefab()
        {
            var root = new GameObject("Root");
            try
            {
                var child = new GameObject("Child");
                child.transform.SetParent(root.transform, false);
                Assert.That(
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath),
                    Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.ImportAsset(
                PrefabPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }
    }
}
