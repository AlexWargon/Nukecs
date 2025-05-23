namespace Wargon.Nukecs.Collision2D
{

    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Transform = Transforms.Transform;

    public class GizmosHelper : MonoBehaviour
    {
        public bool render;
        public bool renderGrid;
        [SerializeField] private Color green;
        [SerializeField] private Color red;
        private GizmosDrawer drawer;
        private void Start()
        {
            drawer = new GizmosDrawer();
            GizmosDrawer.Instance = drawer;
            drawer.AddRender(new Colliders2DRenders(green, red));
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if(!render) return;
            drawer?.Draw();
                if (Grid2D.Instance != null && renderGrid)
                {
                var grid = Grid2D.Instance;
                grid.DrawCells();
            }

            var buffer = DebugUtility.Buffer;
            while (buffer.Count > 0) {
                var (action, color) = buffer.Dequeue();
                UnityEditor.Handles.color = color;
                action.Invoke();
            }
        }
#endif 
        public interface IGizmosRender {
            void Render();
        }
        public class GizmosDrawer {
            private readonly List<IGizmosRender> gizmosList = new();
            public static GizmosDrawer Instance;
            public void AddRender(IGizmosRender gizmosRender) {
                gizmosList.Add(gizmosRender);
            }
            public void RemoveRender(IGizmosRender gizmosRender) {
                gizmosList.Remove(gizmosRender);
            }
            public void Draw()
            {
#if UNITY_EDITOR
                for (int i = 0; i < gizmosList.Count; i++) {
                    gizmosList[i].Render();
                }
#endif
            }
        }

        private class Colliders2DRenders : IGizmosRender {
            private readonly Query query2;
            private readonly Query query;
            private GenericPool circles;
            private GenericPool rectangles;
            private GenericPool transforms;
            private readonly Color green;
            private readonly Color red;
            public void Render() {
#if UNITY_EDITOR
                for (int i = 0; i < query.Count; i++)
                {
                    var entity = query.GetEntityIndex(i);
                    ref var c = ref circles.GetRef<Circle2D>(entity);
                    ref var transform = ref transforms.GetRef<Transform>(entity);
                    UnityEditor.Handles.color = c.collided ? red : green;
                    UnityEditor.Handles.DrawWireDisc(new Vector3(transform.Position.x,transform.Position.y,0), Vector3.forward, c.radius, 3);
                }
                for (int i = 0; i < query2.Count; i++)
                {
                    var entity = query2.GetEntityIndex(i);
                    ref var rectangle = ref rectangles.GetRef<Rectangle2D>(entity);
                    ref var transform = ref transforms.GetRef<Transform>(entity);
                    rectangle.GetVerticesVectors(in transform, out var v0, out var v1, out var v2, out var v3);
                    Debug.DrawLine(v0, v1, green);
                    Debug.DrawLine(v1, v2, green);
                    Debug.DrawLine(v2, v3, green);
                    Debug.DrawLine(v3, v0, green);
                }
#endif
            }
            public Colliders2DRenders(Color g, Color r) {
                ref var world = ref World.Get(0);
                if (!world.IsAlive)
                {
                    Debug.Log("Gizmos World is not Alive");
                    return;
                }
                query2 = world.Query().With<Rectangle2D>();
                query = world.Query().With<Circle2D>();
                circles = world.GetPool<Circle2D>();
                rectangles = world.GetPool<Rectangle2D>();
                transforms = world.GetPool<Transform>();
                green = g;
                red = r;
            }
        }
    }

    public static class DebugUtility {
            public static readonly Queue<(Action,Color)> Buffer = new Queue<(Action, Color)>();
            public static void DrawRect(Vector2 pos, Vector2 size, Color color, Color colorOutline) {
#if UNITY_EDITOR
                ;
                Buffer.Enqueue((() => {
                    UnityEditor.Handles.DrawSolidRectangleWithOutline(new Rect(pos, size), color, colorOutline);
                }
                    ,color));
#endif
            }

            public static void DrawCircle(Vector2 pos, float radius, Color color, float thick) {
#if UNITY_EDITOR
                Buffer.Enqueue((() => {
                        UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, radius, thick);}
                    ,color));
#endif
            }

        public static void DrawLabel(string text, Vector3 pos, Color color, GUIStyle style)
        {
#if UNITY_EDITOR
                if (style == null) style = GUIStyle.none;
                style.normal.textColor = color;
                Buffer.Enqueue((
                    ()=>{UnityEditor.Handles.Label(pos, text, style);}
                    , color));
#endif
        }
    }
}

    