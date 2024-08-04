namespace Wargon.Nukecs
{

    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using UnityEngine;
    using Transform = Tests.Transform;

    public class GizsomHelper : MonoBehaviour
    {
        public bool render;
        public bool renderGrid;
        [SerializeField] private Color green;
        [SerializeField] private Color red;
        private GizsomDrawer drawer;
        private void Start()
        {
            drawer = new GizsomDrawer();
            GizsomDrawer.Instance = drawer;
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
        public class GizsomDrawer {
            private readonly List<IGizmosRender> gizmosList = new();
            public static GizsomDrawer Instance;
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
            private readonly GenericPool circles;
            private readonly GenericPool rectangles;
            private readonly GenericPool transforms;
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
                    ref var c = ref rectangles.GetRef<Rectangle2D>(entity);
                    ref var transform = ref transforms.GetRef<Transform>(entity);
                    Vector3 Y1 = new Vector3(transform.Position.x, transform.Position.y + c.h);
                    Vector3 X1 = new Vector3(transform.Position.x, transform.Position.y);
                    Vector3 Y2 = new Vector3(transform.Position.x + c.w, transform.Position.y + c.h);
                    Vector3 X2 = new Vector3(transform.Position.x + c.w, transform.Position.y);
                    Debug.DrawLine(X1, Y1, green);
                    Debug.DrawLine(Y1, Y2, green);
                    Debug.DrawLine(Y2, X2, green);
                    Debug.DrawLine(X2, X1, green);
                }
    #endif
            }
            public Colliders2DRenders(Color g, Color r) {
                ref var world = ref World.Get(0);
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

    