using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GraphGraphic : ExtendedGraphic
{
    public class GraphCurve
    {
        public Color32 colour = new Color32(255, 255, 255, 255);
        public TimelineList<float> data = new TimelineList<float>();
    }

    public float graphWidth = 2f;
    public float graphMinY = -1f;
    public float graphMaxY = 1f;
    public bool autoFitMinMaxY = true;
    public float autoFitPadding = 0.2f;

    public float curveThickness = 1f;

    public List<GraphCurve> curves = new List<GraphCurve>();

    private List<Vector2> lineBuffer = new List<Vector2>(256);

    public GraphCurve AddCurve(Color32 colour)
    {
        GraphCurve outCurve = new GraphCurve()
        {
            colour = colour
        };

        curves.Add(outCurve);
        return outCurve;
    }

    public void ClearTimeAfter(float time)
    {
        foreach (GraphCurve curve in curves)
            curve.data.TrimAfter(time);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        vh.Clear();

        float timeMax = float.MinValue;

        foreach (GraphCurve curve in curves)
            timeMax = curve.data.LatestTime > timeMax ? (float)curve.data.LatestTime : timeMax;


        float currentMinY = float.MaxValue;
        float currentMaxY = float.MinValue;
        if (autoFitMinMaxY)
        {
            foreach (GraphCurve curve in curves)
            {
                for (int i = 0; i < curve.data.Count; i++)
                {
                    currentMinY = Mathf.Min(currentMinY, curve.data[i]);
                    currentMaxY = Mathf.Max(currentMaxY, curve.data[i]);
                }
            }

            float paddingHalfExtent = autoFitPadding * (currentMaxY - currentMinY) * 0.5f;

            currentMaxY += paddingHalfExtent;
            currentMinY -= paddingHalfExtent;
        }
        else
        {
            currentMinY = graphMinY;
            currentMaxY = graphMaxY;
        }

        float timeMin = timeMax - graphWidth;
        Vector2 scale = new Vector2(rectTransform.rect.width / graphWidth, rectTransform.rect.height / (currentMaxY - currentMinY));
        Vector2 origin = new Vector2(rectTransform.rect.x - timeMin * scale.x, rectTransform.rect.y - currentMinY * scale.y);

        foreach (GraphCurve curve in curves)
        {
            curve.data.TrimBefore(timeMin);

            lineBuffer.Clear();
            for (int i = 0, e = curve.data.Count; i < e; i++)
                lineBuffer.Add(new Vector2((float)curve.data.TimeAt(i), curve.data[i]) * scale + origin);

            DrawLines(vh, lineBuffer, curve.colour, curveThickness);
        }
    }
}
