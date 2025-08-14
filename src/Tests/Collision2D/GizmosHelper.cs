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
#if UNITY_EDITOR
            drawer.AddRender(new Colliders2DRenders(green, red));
#endif
        }

        private void Update()
        {
            if (render)
            {
                drawer?.Draw();
            }
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // if(!render) return;
            // drawer?.Draw();
            //     if (Grid2D.Instance != null && renderGrid)
            //     {
            //     var grid = Grid2D.Instance;
            //     grid.DrawCells();
            // }

            var buffer = DebugUtility.Buffer;
            while (buffer.Count > 0)
            {
                var (action, color) = buffer.Dequeue();
                UnityEditor.Handles.color = color;
                action.Invoke();
            }
        }
#endif
        public interface IGizmosRender
        {
            void Render();
        }

        public class GizmosDrawer
        {
            private readonly List<IGizmosRender> gizmosList = new();
            public static GizmosDrawer Instance;

            public void AddRender(IGizmosRender gizmosRender)
            {
                gizmosList.Add(gizmosRender);
            }

            public void RemoveRender(IGizmosRender gizmosRender)
            {
                gizmosList.Remove(gizmosRender);
            }

            public void Draw()
            {
#if UNITY_EDITOR
                for (int i = 0; i < gizmosList.Count; i++)
                {
                    gizmosList[i].Render();
                }
#endif
            }
        }

#if UNITY_EDITOR

        private class Colliders2DRenders : IGizmosRender
        {
            private readonly Query queryCircles;
            private readonly Query queryRects;
            private GenericPool circles;
            private GenericPool rectangles;
            private GenericPool transforms;
            private readonly Color green;
            private readonly Color red;

            private Mesh circleMesh;
            private Mesh rectMesh;
            private Material material;

            private List<Matrix4x4> circleMatrices = new List<Matrix4x4>(1023);
            private List<Matrix4x4> rectMatrices = new List<Matrix4x4>(1023);

            public Colliders2DRenders(Color g, Color r)
            {
                ref var world = ref World.Get(0);
                if (!world.IsAlive)
                {
                    Debug.Log("Gizmos World is not Alive");
                    return;
                }

                queryCircles = world.Query().With<Circle2D>();
                queryRects = world.Query().With<Rectangle2D>();
                circles = world.GetPool<Circle2D>();
                rectangles = world.GetPool<Rectangle2D>();
                transforms = world.GetPool<Transform>();
                green = g;
                red = r;

                circleMesh = CreateCircleMesh(1f, 32);
                rectMesh = CreateRectMesh();
                material = new Material(Shader.Find("Unlit/Color"));
                material.enableInstancing = true;
                material.color = g;
            }

            public void Render()
            {
                // Очистка списков
                circleMatrices.Clear();
                rectMatrices.Clear();

                // Круги
                for (int i = 0; i < queryCircles.Count; i++)
                {
                    var entity = queryCircles.GetEntityIndex(i);
                    ref var c = ref circles.GetRef<Circle2D>(entity);
                    ref var t = ref transforms.GetRef<Transform>(entity);
                    var m = Matrix4x4.TRS(
                        new Vector3(t.Position.x, t.Position.y, 0),
                        Quaternion.identity,
                        Vector3.one * (c.radius * 2f)
                    );
                    circleMatrices.Add(m);
                }

                // Прямоугольники
                for (int i = 0; i < queryRects.Count; i++)
                {
                    var entity = queryRects.GetEntityIndex(i);
                    ref var r = ref rectangles.GetRef<Rectangle2D>(entity);
                    ref var t = ref transforms.GetRef<Transform>(entity);

                    // Размер и поворот из твоей логики
                    rectMatrices.Add(Matrix4x4.TRS(
                        new Vector3(t.Position.x, t.Position.y, 0),
                        t.Rotation,
                        new Vector3(r.w, r.h, 1f)
                    ));
                }

                // Рендер пачками (1023 макс. за раз)
                DrawBatched(circleMesh, circleMatrices);
                DrawBatched(rectMesh, rectMatrices);
            }

            private void DrawBatched(Mesh mesh, List<Matrix4x4> matrices)
            {
                for (int i = 0; i < matrices.Count; i += 1023)
                {
                    int count = Mathf.Min(1023, matrices.Count - i);
                    Graphics.DrawMeshInstanced(mesh, 0, material, matrices.GetRange(i, count));
                }
            }

            private Mesh CreateCircleMesh(float radius, int segments)
            {
                Mesh mesh = new Mesh();
                Vector3[] vertices = new Vector3[segments + 1];
                int[] triangles = new int[segments * 3];

                vertices[0] = Vector3.zero;
                for (int i = 0; i < segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                }

                for (int i = 0; i < segments; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = (i + 2 > segments) ? 1 : i + 2;
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                return mesh;
            }

            private Mesh CreateRectMesh()
            {
                Mesh mesh = new Mesh();
                mesh.vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0)
                };
                mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
                return mesh;
            }
        }
#endif


        public static class DebugUtility
        {
            public static readonly Queue<(Action, Color)> Buffer = new Queue<(Action, Color)>();

            public static void DrawRect(Vector2 pos, Vector2 size, Color color, Color colorOutline)
            {
#if UNITY_EDITOR
                ;
                Buffer.Enqueue((() =>
                    {
                        UnityEditor.Handles.DrawSolidRectangleWithOutline(new Rect(pos, size), color, colorOutline);
                    }
                    , color));
#endif
            }

            public static void DrawCircle(Vector2 pos, float radius, Color color, float thick)
            {
#if UNITY_EDITOR
                Buffer.Enqueue((() => { UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, radius, thick); }
                    , color));
#endif
            }

            public static void DrawLabel(string text, Vector3 pos, Color color, GUIStyle style)
            {
#if UNITY_EDITOR
                if (style == null) style = GUIStyle.none;
                style.normal.textColor = color;
                Buffer.Enqueue((
                    () => { UnityEditor.Handles.Label(pos, text, style); }
                    , color));
#endif
            }
        }
    }
}

    