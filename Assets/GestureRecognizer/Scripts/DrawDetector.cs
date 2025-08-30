using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Events;

namespace GestureRecognizer
{

    public class DrawDetector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {

        public Recognizer recognizer;

        public UILineDrawer line; // updated to UILineDrawer
        private List<UILineDrawer> lines;

        [Range(0f, 1f)]
        public float scoreToAccept = 0.8f;

        [Range(1, 10)]
        public int minLines = 1;
        public int MinLines { set { minLines = Mathf.Clamp(value, 1, 10); } }

        [Range(1, 10)]
        public int maxLines = 2;
        public int MaxLines { set { maxLines = Mathf.Clamp(value, 1, 10); } }

        public enum RemoveStrategy { RemoveOld, ClearAll }
        public RemoveStrategy removeStrategy;

        public bool clearNotRecognizedLines;

        public bool fixedArea = false;

        GestureData data = new GestureData();

        [System.Serializable]
        public class ResultEvent : UnityEvent<RecognitionResult> { }
        public ResultEvent OnRecognize;

        RectTransform rectTransform;

        void Start()
        {
            lines = new List<UILineDrawer>() { line };
            rectTransform = transform as RectTransform;
        }

        void OnValidate()
        {
            maxLines = Mathf.Max(minLines, maxLines);
        }

        public void ClearLines()
        {
            data.lines.Clear();
            foreach (var l in lines) l.ClearLine();
        }

        public void OnPointerClick(PointerEventData eventData) { }

        // Convert screen point to local point in RectTransform
        private Vector2 ScreenToLocal(PointerEventData eventData)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );
            return localPoint;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {

            // Remove old lines if exceeding maxLines
            if (data.lines.Count >= maxLines)
            {
                switch (removeStrategy)
                {
                    case RemoveStrategy.RemoveOld:
                        // Remove the first line
                        data.lines.RemoveAt(0);
                        lines[0].ClearLine(); // clear the corresponding drawer
                        break;
                    case RemoveStrategy.ClearAll:
                        data.lines.Clear();
                        foreach (var l in lines) l.ClearLine(); // clear all drawers
                        break;
                }
            }

            // Start a new line
            data.lines.Add(new GestureLine());

            Vector2 localPoint = ScreenToLocal(eventData);

            if (lines.Count < data.lines.Count)
            {
                var newLine = Instantiate(line, line.transform.parent);
                lines.Add(newLine);
            }
            else
            {
                // Clear the drawer if reusing existing one
                lines[data.lines.Count - 1].ClearLine();
            }

            // Add first point
            data.LastLine.points.Add(localPoint);
            lines[data.lines.Count - 1].AddPoint(localPoint);
        }


        public void OnDrag(PointerEventData eventData)
        {
            Vector2 localPoint = ScreenToLocal(eventData);

            if (data.LastLine.points.Count == 0 || data.LastLine.points.Last() != localPoint)
            {
                data.LastLine.points.Add(localPoint);
                lines[data.lines.Count - 1].AddPoint(localPoint);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            StartCoroutine(OnEndDragCoroutine(eventData));
        }

        IEnumerator OnEndDragCoroutine(PointerEventData eventData)
        {

            Vector2 localPoint = ScreenToLocal(eventData);
            data.LastLine.points.Add(localPoint);
            lines[data.lines.Count - 1].AddPoint(localPoint);

            for (int size = data.lines.Count; size >= 1 && size >= minLines; size--)
            {
                var sizedData = new GestureData()
                {
                    lines = data.lines.GetRange(data.lines.Count - size, size)
                };

                var sizedNormalizedData = sizedData;

                if (fixedArea)
                {
                    var rect = this.rectTransform.rect;
                    sizedNormalizedData = new GestureData()
                    {
                        lines = sizedData.lines.Select(line => new GestureLine()
                        {
                            closedLine = line.closedLine,
                            points = line.points.Select(p => Rect.PointToNormalized(rect, this.rectTransform.InverseTransformPoint(p))).ToList()
                        }).ToList()
                    };
                }

                RecognitionResult result = null;

                var thread = new System.Threading.Thread(() => {
                    result = recognizer.Recognize(sizedNormalizedData, normalizeScale: !fixedArea);
                });
                thread.Start();
                while (thread.IsAlive) yield return null;

                if (result.gesture != null && result.score.score >= scoreToAccept)
                {
                    OnRecognize.Invoke(result);
                    if (clearNotRecognizedLines)
                    {
                        data = sizedData;

                        // update lines
                        for (int i = 0; i < lines.Count; i++) lines[i].ClearLine();
                        for (int i = 0; i < data.lines.Count; i++)
                        {
                            foreach (var p in data.lines[i].points) lines[i].AddPoint(p);
                        }
                    }
                    break;
                }
                else
                {
                    OnRecognize.Invoke(RecognitionResult.Empty);
                }
            }

            yield return null;
        }
    }

}
