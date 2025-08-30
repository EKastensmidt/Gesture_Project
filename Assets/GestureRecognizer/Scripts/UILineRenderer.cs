using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GestureRecognizer
{
    public class UILineRenderer : MaskableGraphic
    {
        private enum SegmentType { Start, Middle, End }
        public enum JoinType { Bevel, Miter }

        private const float MIN_MITER_JOIN = 15 * Mathf.Deg2Rad;
        private const float MIN_BEVEL_NICE_JOIN = 30 * Mathf.Deg2Rad;

        private static readonly Vector2[] startUvs = {
            new Vector2(0,0), new Vector2(0,1), new Vector2(0.5f,1), new Vector2(0.5f,0)
        };
        private static readonly Vector2[] middleUvs = {
            new Vector2(0.5f,0), new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,0)
        };
        private static readonly Vector2[] endUvs = {
            new Vector2(0.5f,0), new Vector2(0.5f,1), new Vector2(1,1), new Vector2(1,0)
        };

        [SerializeField] private Texture m_Texture;
        [SerializeField] private Rect m_UVRect = new Rect(0f, 0f, 1f, 1f);

        public float LineThickness = 2;
        public bool UseMargins;
        public Vector2 Margin;
        public Vector2[] Points;
        public bool relativeSize;
        public bool LineList = false;
        public bool LineCaps = false;
        public JoinType LineJoins = JoinType.Bevel;

        private readonly List<UIVertex[]> segments = new List<UIVertex[]>(64);

        public override Texture mainTexture => m_Texture == null ? s_WhiteTexture : m_Texture;

        public void SetPoints(Vector2[] newPoints)
        {
            Points = newPoints;
            SetVerticesDirty(); // forces rebuild next frame
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Points == null || Points.Length < 2) return;

            segments.Clear();
            BuildSegments();
            AddSegmentsToVH(vh);
        }

        private void BuildSegments()
        {
            var sizeX = rectTransform.rect.width;
            var sizeY = rectTransform.rect.height;
            var offsetX = -rectTransform.pivot.x * rectTransform.rect.width;
            var offsetY = -rectTransform.pivot.y * rectTransform.rect.height;

            if (!relativeSize) { sizeX = 1; sizeY = 1; }
            if (UseMargins)
            {
                sizeX -= Margin.x;
                sizeY -= Margin.y;
                offsetX += Margin.x / 2f;
                offsetY += Margin.y / 2f;
            }

            if (LineList)
            {
                for (int i = 1; i < Points.Length; i += 2)
                {
                    var start = new Vector2(Points[i - 1].x * sizeX + offsetX, Points[i - 1].y * sizeY + offsetY);
                    var end = new Vector2(Points[i].x * sizeX + offsetX, Points[i].y * sizeY + offsetY);

                    if (LineCaps) segments.Add(CreateLineCap(start, end, SegmentType.Start));
                    segments.Add(CreateLineSegment(start, end, SegmentType.Middle));
                    if (LineCaps) segments.Add(CreateLineCap(start, end, SegmentType.End));
                }
            }
            else
            {
                for (int i = 1; i < Points.Length; i++)
                {
                    var start = new Vector2(Points[i - 1].x * sizeX + offsetX, Points[i - 1].y * sizeY + offsetY);
                    var end = new Vector2(Points[i].x * sizeX + offsetX, Points[i].y * sizeY + offsetY);

                    if (LineCaps && i == 1) segments.Add(CreateLineCap(start, end, SegmentType.Start));
                    segments.Add(CreateLineSegment(start, end, SegmentType.Middle));
                    if (LineCaps && i == Points.Length - 1) segments.Add(CreateLineCap(start, end, SegmentType.End));
                }
            }
        }

        private void AddSegmentsToVH(VertexHelper vh)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (!LineList && i < segments.Count - 1)
                {
                    var vec1 = (Vector2)(segments[i][1].position - segments[i][2].position);
                    var vec2 = (Vector2)(segments[i + 1][2].position - segments[i + 1][1].position);

                    float dot = Mathf.Clamp(Vector2.Dot(vec1.normalized, vec2.normalized), -1f, 1f);
                    float angle = Mathf.Acos(dot);

                    var sign = Mathf.Sign(Vector3.Cross(vec1.normalized, vec2.normalized).z);
                    var miterDistance = LineThickness / (2 * Mathf.Tan(angle / 2));
                    var miterPointA = segments[i][2].position - (Vector3)vec1.normalized * miterDistance * sign;
                    var miterPointB = segments[i][3].position + (Vector3)vec1.normalized * miterDistance * sign;

                    var joinType = LineJoins;
                    if (joinType == JoinType.Miter)
                    {
                        if (miterDistance < vec1.magnitude / 2 && miterDistance < vec2.magnitude / 2 && angle > MIN_MITER_JOIN)
                        {
                            segments[i][2].position = miterPointA;
                            segments[i][3].position = miterPointB;
                            segments[i + 1][0].position = miterPointB;
                            segments[i + 1][1].position = miterPointA;
                        }
                        else joinType = JoinType.Bevel;
                    }

                    if (joinType == JoinType.Bevel)
                    {
                        if (miterDistance < vec1.magnitude / 2 && miterDistance < vec2.magnitude / 2 && angle > MIN_BEVEL_NICE_JOIN)
                        {
                            if (sign < 0)
                            {
                                segments[i][2].position = miterPointA;
                                segments[i + 1][1].position = miterPointA;
                            }
                            else
                            {
                                segments[i][3].position = miterPointB;
                                segments[i + 1][0].position = miterPointB;
                            }
                        }
                        var join = new UIVertex[] { segments[i][2], segments[i][3], segments[i + 1][0], segments[i + 1][1] };
                        vh.AddUIVertexQuad(join);
                    }
                }
                vh.AddUIVertexQuad(segments[i]);
            }
        }

        private UIVertex[] CreateLineCap(Vector2 start, Vector2 end, SegmentType type)
        {
            if (type == SegmentType.Start)
            {
                var capStart = start - ((end - start).normalized * LineThickness / 2);
                return CreateLineSegment(capStart, start, SegmentType.Start);
            }
            else if (type == SegmentType.End)
            {
                var capEnd = end + ((end - start).normalized * LineThickness / 2);
                return CreateLineSegment(end, capEnd, SegmentType.End);
            }
            return null;
        }

        private UIVertex[] CreateLineSegment(Vector2 start, Vector2 end, SegmentType type)
        {
            var uvs = middleUvs;
            if (type == SegmentType.Start) uvs = startUvs;
            else if (type == SegmentType.End) uvs = endUvs;

            Vector2 offset = new Vector2(start.y - end.y, end.x - start.x).normalized * LineThickness / 2;
            var v1 = start - offset;
            var v2 = start + offset;
            var v3 = end + offset;
            var v4 = end - offset;
            return SetVbo(new[] { v1, v2, v3, v4 }, uvs);
        }

        private UIVertex[] SetVbo(Vector2[] vertices, Vector2[] uvs)
        {
            UIVertex[] vbo = new UIVertex[4];
            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = UIVertex.simpleVert;
                vert.color = color;
                vert.position = vertices[i];
                vert.uv0 = uvs[i];
                vbo[i] = vert;
            }
            return vbo;
        }
    }
}
