#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Linq;
using UnityEditor;
using UnityEngine;
using TransformComponent = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    /// <summary>
    /// Draws a GameObject-Transform-style gizmo in SceneView for the entity currently selected
    /// in the EcsDebugV2 window (EntitiesTab / ArchetypesList selection both write
    /// <see cref="EcsDebugV2Window.selectedEntityId"/>, which is polled here).
    /// Follows the active Unity tool: Move / Rotate / Scale / Transform are interactive and write
    /// straight into the Transform component memory (ref into the archetype — no Undo support);
    /// with no transform tool active, static colored axis arrows are drawn from the entity position.
    /// Only entities with the world-space <see cref="TransformComponent"/> get a gizmo.
    /// </summary>
    internal static class EntityTransformGizmoDrawer
    {
        // Cached so SceneView repaints don't pay Resources.FindObjectsOfTypeAll every frame;
        // destroyed windows become fake-null (EditorWindow is a ScriptableObject) and re-resolve.
        private static EcsDebugV2Window _window;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (_window == null)
            {
                _window = Resources.FindObjectsOfTypeAll<EcsDebugV2Window>().FirstOrDefault();
                if (_window == null) return;
            }

            // Mock data (edit mode) has no world memory to read or write.
            if (_window.provider is not LiveDataProvider provider) return;
            if (!_window.selectedEntityId.HasValue) return;

            var entityId = _window.selectedEntityId.Value;
            if (!TryResolveTransformEntity(provider, entityId, out var entity)) return; // dead or no Transform

            // UnityEngine types for the Handles API — its ref parameters and comparison
            // operators don't mix with float3/quaternion implicit conversions (CS0034/CS1503).
            // Written back only when changed so idle repaints never touch component memory.
            ref var transform = ref entity.Get<TransformComponent>();
            Vector3 position = transform.Position;
            Quaternion rotation = transform.Rotation;
            Vector3 scale = transform.Scale;

            switch (Tools.current)
            {
                case Tool.Move:
                {
                    var newPosition = ScaledPositionHandle(position, rotation);
                    if (newPosition != position)
                        transform.Position = newPosition;
                    break;
                }
                case Tool.Rotate:
                {
                    var newRotation = ScaledRotationHandle(rotation, position);
                    if (newRotation != rotation)
                        transform.Rotation = newRotation;
                    break;
                }
                case Tool.Scale:
                {
                    var newScale = ScaledScaleHandle(scale, position, rotation);
                    if (newScale != scale)
                        transform.Scale = newScale;
                    break;
                }
                case Tool.Transform:
                {
                    var newPos = position;
                    var newRot = rotation;
                    var newScl = scale;
                    ScaledTransformHandle(ref newPos, ref newRot, ref newScl);
                    if (newPos != position) transform.Position = newPos;
                    if (newRot != rotation) transform.Rotation = newRot;
                    if (newScl != scale) transform.Scale = newScl;
                    break;
                }
                default:
                    DrawAxisArrows(position, rotation);
                    break;
            }
        }

        // >1 renders the handle arrows bigger and with larger pick areas (easier to grab).
        private const float HandleScale = 1.6f;

        private static Vector3 ScaledPositionHandle(Vector3 position, Quaternion rotation)
        {
            var prevMatrix = Handles.matrix;
            // Uniform scale around the pivot: handles render bigger; the handle works in its
            // local (scaled) space, so the returned offset converts back by the scale factor.
            Handles.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * HandleScale);
            var localOffset = Handles.PositionHandle(Vector3.zero, rotation);
            Handles.matrix = prevMatrix;
            return position + localOffset * HandleScale;
        }

        private static Quaternion ScaledRotationHandle(Quaternion rotation, Vector3 position)
        {
            var prevMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * HandleScale);
            var result = Handles.RotationHandle(rotation, Vector3.zero);
            Handles.matrix = prevMatrix;
            return result; // rotation values are unaffected by the uniform matrix scale
        }

        private static Vector3 ScaledScaleHandle(Vector3 scale, Vector3 position, Quaternion rotation)
        {
            var prevMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * HandleScale);
            var result = Handles.ScaleHandle(scale, Vector3.zero, rotation);
            Handles.matrix = prevMatrix;
            return result; // returned scale stays in the units of the passed-in scale
        }

        private static void ScaledTransformHandle(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            var prevMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * HandleScale);
            var localOffset = Vector3.zero;
            Handles.TransformHandle(ref localOffset, ref rotation, ref scale);
            Handles.matrix = prevMatrix;
            position += localOffset * HandleScale;
        }

        private static void DrawAxisArrows(Vector3 position, Quaternion rotation)
        {
            var size = HandleUtility.GetHandleSize(position);
            DrawArrow(position, rotation * Vector3.right, AxisColorX, size);
            DrawArrow(position, rotation * Vector3.up, AxisColorY, size);
            DrawArrow(position, rotation * Vector3.forward, AxisColorZ, size);
        }

        private static void DrawArrow(Vector3 position, Vector3 direction, Color color, float size)
        {
            if (direction.sqrMagnitude == 0f) return; // degenerate rotation — avoid LookRotation warning
            Handles.color = color;
            Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(direction), size, EventType.Repaint);
        }

        /// <summary>
        /// Moves the last active SceneView camera pivot to the entity's Transform position,
        /// like Unity's Frame Selected. Called on entity double-click in the EntitiesTab list.
        /// </summary>
        internal static void FrameEntity(EcsDebugV2Window window, int entityId)
        {
            if (window == null || window.provider is not LiveDataProvider provider) return;
            if (!TryResolveTransformEntity(provider, entityId, out var entity)) return;

            ref var transform = ref entity.Get<TransformComponent>();
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            var maxScale = Mathf.Max(transform.Scale.x, Mathf.Max(transform.Scale.y, transform.Scale.z));
            sceneView.pivot = transform.Position; // float3 → Vector3
            sceneView.size = Mathf.Max(1f, maxScale * 2f);
            sceneView.Repaint();
        }

        // Shared resolution: world alive, entity alive, entity has a Transform component.
        private static bool TryResolveTransformEntity(LiveDataProvider provider, int entityId, out Entity entity)
        {
            entity = default;
            ref var world = ref World.Get(provider.WorldIndex);
            if (!world.IsAlive) return false;
            if (provider.GetEntityArchetypeIndex(entityId) < 0) return false; // not alive
            entity = world.GetEntity(entityId);
            return entity.Has<TransformComponent>();
        }

        private static readonly Color AxisColorX = new(0.95f, 0.35f, 0.35f);
        private static readonly Color AxisColorY = new(0.55f, 0.95f, 0.45f);
        private static readonly Color AxisColorZ = new(0.45f, 0.60f, 0.95f);
    }
}
#endif
