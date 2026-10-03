using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Wargon.Nukecs.Editor;
using Wargon.Nukecs.Editor.EcsDebugV2;

namespace Wargon.Nukecs.Tests
{
    public enum AuthoringMode { Idle = 3, Running = 17 }
    public struct AuthoringValue : IComponent
    {
        public int Count;
        public bool Enabled;
        public float3 Position;
        public AuthoringMode Mode;
    }
    public struct AuthoringPoolValue : IPoolComponent { public float Amount; }

    public class ComponentAuthoringTests
    {
        private GameObject _go;
        private EntityBaker _baker;
        private SerializedObject _serialized;

        [SetUp] public void SetUp()
        {
            _go = new GameObject("Authoring test");
            _baker = _go.AddComponent<EntityBaker>();
            _serialized = new SerializedObject(_baker);
            var list = _serialized.FindProperty("components");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).managedReferenceValue = new AuthoringValue {
                Count = 2, Position = new float3(1, 2, 3), Mode = AuthoringMode.Idle
            };
            list.GetArrayElementAtIndex(1).managedReferenceValue = new AuthoringPoolValue { Amount = 4 };
            _serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown] public void TearDown()
        {
            _serialized?.Dispose();
            Object.DestroyImmediate(_go);
        }

        [Test] public void SharedRendererDataAdapter_WritesStructAndPoolFields_AndPreservesUndo()
        {
            var binding = new SerializedComponentCardBinding(_serialized, "components", 0, () => { });
            var snapshot = binding.Read();
            Assert.AreEqual(2, snapshot.GetField("Count").NumberVal);
            Assert.AreEqual(3, snapshot.GetField("Position.z").NumberVal);
            CollectionAssert.AreEqual(new long[] { 3, 17 }, snapshot.GetField("Mode").EnumRawValues);
            binding.SetFieldValue(0, snapshot.Name, "Enabled", FieldValue.FromBool(true));
            binding.SetFieldValue(0, snapshot.Name, "Position.y", FieldValue.FromNumber(12.5));
            binding.SetFieldValue(0, snapshot.Name, "Mode", FieldValue.FromEnum(
                new[] { "Idle", "Running" }, new long[] { 3, 17 }, 1, 17));
            Assert.IsTrue(binding.Read().GetField("Enabled").BoolVal);
            Assert.AreEqual(12.5, binding.Read().GetField("Position.y").NumberVal);
            Assert.AreEqual(17, binding.Read().GetField("Mode").EnumRawValue);
            var pool = new SerializedComponentCardBinding(_serialized, "components", 1, () => { });
            pool.SetFieldValue(1, "AuthoringPoolValue", "Amount", FieldValue.FromNumber(8));
            Assert.AreEqual(8, pool.Read().GetField("Amount").NumberVal);
            Undo.IncrementCurrentGroup();
            binding.SetFieldValue(0, snapshot.Name, "Count", FieldValue.FromNumber(99));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            _serialized.Update();
            Assert.AreEqual(2, binding.Read().GetField("Count").NumberVal);
        }

        [Test] public void SerializedStructValues_RoundTrip_AndRemoveRefreshesTheList()
        {
            var binding = new SerializedComponentCardBinding(_serialized, "components", 0, () => { });
            binding.SetFieldValue(0, "AuthoringValue", "Count", FieldValue.FromNumber(42));
            var json = EditorJsonUtility.ToJson(_baker);
            var clone = new GameObject("Authoring clone");
            try {
                var other = clone.AddComponent<EntityBaker>();
                EditorJsonUtility.FromJsonOverwrite(json, other);
                using var serialized = new SerializedObject(other);
                var refreshes = 0;
                var restored = new SerializedComponentCardBinding(serialized, "components", 0, () => refreshes++);
                Assert.AreEqual(42, restored.Read().GetField("Count").NumberVal);
                restored.RemoveComponent(0, "AuthoringValue");
                Assert.AreEqual(1, refreshes);
                Assert.AreEqual(1, serialized.FindProperty("components").arraySize);
                Assert.AreEqual("AuthoringPoolValue", restored.Read().Name);
            }
            finally { Object.DestroyImmediate(clone); }
        }
    }
}
