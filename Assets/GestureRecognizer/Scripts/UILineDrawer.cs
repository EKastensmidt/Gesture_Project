using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GestureRecognizer
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineDrawer : MaskableGraphic
    {
        [SerializeField] private float lineThickness = 4f;

        private Mesh lineMesh;
        private List<Vector3> vertices = new List<Vector3>();
        private List<int> indices = new List<int>();
        private List<Vector2> uvs = new List<Vector2>();

        private Vector2? lastPoint = null;

        protected override void Awake()
        {
            base.Awake();
            lineMesh = new Mesh();
            lineMesh.MarkDynamic(); // optimize for streaming geometry
        }

        public override Texture mainTexture => s_WhiteTexture;

        /// <summary>
        /// Call this every time the player moves their finger/mouse.
        /// </summary>
        public void AddPoint(Vector2 point)
        {
            if (lastPoint == null)
            {
                lastPoint = point;
                // Only start drawing after the second point
                return;
            }

            Vector2 start = (Vector2)lastPoint;
            Vector2 end = point;
            lastPoint = point;

            // create quad
            Vector2 dir = (end - start).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * (lineThickness * 0.5f);

            int vertIndex = vertices.Count;

            vertices.Add(start - normal); // v0
            vertices.Add(start + normal); // v1
            vertices.Add(end + normal);   // v2
            vertices.Add(end - normal);   // v3

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
            uvs.Add(new Vector2(1, 0));

            indices.Add(vertIndex + 0);
            indices.Add(vertIndex + 1);
            indices.Add(vertIndex + 2);
            indices.Add(vertIndex + 2);
            indices.Add(vertIndex + 3);
            indices.Add(vertIndex + 0);

            // update mesh
            lineMesh.Clear();
            lineMesh.SetVertices(vertices);
            lineMesh.SetUVs(0, uvs);
            lineMesh.SetTriangles(indices, 0);

            canvasRenderer.SetMesh(lineMesh);
        }

        /// <summary>
        /// Reset the line.
        /// </summary>
        public void ClearLine()
        {
            vertices.Clear();
            indices.Clear();
            uvs.Clear();
            lineMesh.Clear();
            canvasRenderer.SetMesh(lineMesh);
            lastPoint = null;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // Only draw our mesh if we actually have points
            if (vertices.Count == 0)
            {
                vh.Clear();
                return;
            }
        }
    }
}
