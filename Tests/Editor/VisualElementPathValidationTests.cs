using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using VMFramework.OdinExtensions;
using VMFramework.UI;

namespace VMFramework.Pipeline.Editor.Tests
{
    [Category("VMFrameworkPipeline.FullRegression")]
    public sealed class VisualElementPathValidationTests
    {
        private sealed class Fixture
        {
            public VisualElementPath optionalPath = new();

            [IsNotNullOrEmpty]
            public VisualElementPath requiredPath = new();
        }

        private sealed class MissingUnityObjectFixture
        {
            public Transform customTransform;
            public VisualElementPath path = new();
        }

        [Test]
        public void EmptyPath_IsValidOnlyWhenFieldIsOptional()
        {
            Type toolsType = typeof(VMFrameworkUIPanelPipelineTools);
            MethodInfo isRequired = toolsType.GetMethod("IsVisualElementPathRequired",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo validate = toolsType.GetMethod("ValidateVisualElementPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Type recordType = toolsType.GetNestedType("VisualElementPathRecord", BindingFlags.NonPublic);

            Assert.That(isRequired, Is.Not.Null);
            Assert.That(validate, Is.Not.Null);
            Assert.That(recordType, Is.Not.Null);

            AssertEmptyPathResult(nameof(Fixture.optionalPath), expectedRequired: false, expectedValid: true);
            AssertEmptyPathResult(nameof(Fixture.requiredPath), expectedRequired: true, expectedValid: false);

            void AssertEmptyPathResult(string fieldName, bool expectedRequired, bool expectedValid)
            {
                FieldInfo field = typeof(Fixture).GetField(fieldName);
                bool required = (bool)isRequired.Invoke(null, new object[] { field });
                Assert.That(required, Is.EqualTo(expectedRequired));

                object record = Activator.CreateInstance(recordType, nonPublic: true);
                recordType.GetField("owner").SetValue(record, "Fixture");
                recordType.GetField("member").SetValue(record, fieldName);
                recordType.GetField("path").SetValue(record, field.GetValue(new Fixture()));
                recordType.GetField("required").SetValue(record, required);
                recordType.GetField("allowedTypes").SetValue(record, new List<Type>());

                var result = (Dictionary<string, object>)validate.Invoke(null,
                    new[] { (object)new VisualElement(), record });
                Assert.That(result["required"], Is.EqualTo(expectedRequired));
                Assert.That(result["valid"], Is.EqualTo(expectedValid));
                Assert.That(result.ContainsKey("skipped"), Is.EqualTo(expectedValid));
            }
        }

        [Test]
        public void LocalPathRoot_UsesNearestEntryGeneratorAndHonorsParentOnly()
        {
            var parent = new GameObject("Parent Entry");
            var child = new GameObject("Nested Entry");
            var asset = ScriptableObject.CreateInstance<VisualTreeAsset>();
            try
            {
                child.transform.SetParent(parent.transform);
                var outer = parent.AddComponent<PairEntryAdder>();
                var inner = child.AddComponent<PairEntryAdder>();
                outer.entryAsset = asset;
                inner.entryAsset = asset;
                var panelRoot = new VisualElement { name = "Panel Root" };
                var cache = new Dictionary<IVisualElementGenerator, VisualElement>();
                var method = typeof(VMFrameworkUIPanelPipelineTools).GetMethod("ResolveVisualElementPathRoot",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var settings = new VisualElementPathSettingsAttribute { IsFromLocalProvider = true };

                var args = new object[] { panelRoot, inner, settings, cache, null };
                var innerRoot = (VisualElement)method.Invoke(null, args);
                Assert.That(args[4], Is.Null);
                Assert.That(innerRoot, Is.SameAs(cache[inner]));
                Assert.That(innerRoot, Is.Not.SameAs(panelRoot));
                Assert.That(method.Invoke(null, args), Is.SameAs(innerRoot));
                Assert.That(cache.Count, Is.EqualTo(1));

                settings.MustFromParent = true;
                var outerRoot = (VisualElement)method.Invoke(null, args);
                Assert.That(outerRoot, Is.SameAs(cache[outer]));
                Assert.That(outerRoot, Is.Not.SameAs(innerRoot));
                Assert.That(cache.Count, Is.EqualTo(2));

                settings.IsFromLocalProvider = false;
                Assert.That(method.Invoke(null, args), Is.SameAs(panelRoot));

                settings.IsFromLocalProvider = true;
                args[1] = outer;
                Assert.That(method.Invoke(null, args), Is.Null);
                Assert.That(args[4], Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ScanVisualElementPaths_DoesNotEnumerateMissingUnityObjectReferences()
        {
            Type toolsType = typeof(VMFrameworkUIPanelPipelineTools);
            MethodInfo scan = toolsType.GetMethod("ScanVisualElementPaths",
                BindingFlags.Static | BindingFlags.NonPublic);
            Type recordType = toolsType.GetNestedType("VisualElementPathRecord", BindingFlags.NonPublic);
            Assert.That(scan, Is.Not.Null);
            Assert.That(recordType, Is.Not.Null);

            var gameObject = new GameObject("Missing Transform Fixture");
            var fixture = new MissingUnityObjectFixture
            {
                customTransform = gameObject.transform
            };
            UnityEngine.Object.DestroyImmediate(gameObject);

            Type listType = typeof(List<>).MakeGenericType(recordType);
            var records = (IList)Activator.CreateInstance(listType);
            Assert.DoesNotThrow(() => scan.Invoke(null, new object[]
            {
                fixture,
                "Fixture",
                records,
                new HashSet<object>(),
                0,
                null
            }));
            Assert.That(records.Count, Is.EqualTo(1));
        }

        [Test]
        public void ScanVisualElementPaths_SkipsInactiveOdinConditionalFields()
        {
            Type toolsType = typeof(VMFrameworkUIPanelPipelineTools);
            MethodInfo scan = toolsType.GetMethod("ScanVisualElementPaths",
                BindingFlags.Static | BindingFlags.NonPublic);
            Type recordType = toolsType.GetNestedType("VisualElementPathRecord", BindingFlags.NonPublic);
            Assert.That(scan, Is.Not.Null);
            Assert.That(recordType, Is.Not.Null);

            var gameObject = new GameObject("Conditional VisualElementPath Fixture");
            try
            {
                var pairEntryAdder = gameObject.AddComponent<PairEntryAdder>();
                pairEntryAdder.useParentObject = false;
                AssertScannedMembers(pairEntryAdder,
                    included: new[] { nameof(PairEntryAdder.containerPath) },
                    excluded: new[] { nameof(PairEntryAdder.parentContainerPath) });

                pairEntryAdder.useParentObject = true;
                AssertScannedMembers(pairEntryAdder,
                    included: new[] { nameof(PairEntryAdder.parentContainerPath) },
                    excluded: new[] { nameof(PairEntryAdder.containerPath) });

                var overflowModifier = gameObject.AddComponent<ElementOverflowControlModifier>();
                overflowModifier.containerMode = ElementOverflowControlModifier.ContainerMode.Panel;
                AssertScannedMembers(overflowModifier,
                    included: new[] { nameof(ElementOverflowControlModifier.targetPath) },
                    excluded: new[] { nameof(ElementOverflowControlModifier.containerPath) });

                overflowModifier.containerMode = ElementOverflowControlModifier.ContainerMode.Custom;
                AssertScannedMembers(overflowModifier,
                    included: new[]
                    {
                        nameof(ElementOverflowControlModifier.containerPath),
                        nameof(ElementOverflowControlModifier.targetPath)
                    },
                    excluded: Array.Empty<string>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            void AssertScannedMembers(object target, IEnumerable<string> included, IEnumerable<string> excluded)
            {
                Type listType = typeof(List<>).MakeGenericType(recordType);
                var records = (IList)Activator.CreateInstance(listType);
                scan.Invoke(null, new object[]
                {
                    target,
                    target.GetType().Name,
                    records,
                    new HashSet<object>(),
                    0,
                    null
                });

                FieldInfo memberField = recordType.GetField("member");
                var members = new HashSet<string>();
                foreach (object record in records)
                {
                    members.Add(memberField.GetValue(record)?.ToString());
                }
                foreach (string member in included)
                {
                    Assert.That(members, Does.Contain(member));
                }
                foreach (string member in excluded)
                {
                    Assert.That(members, Does.Not.Contain(member));
                }
            }
        }
    }
}
