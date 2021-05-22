using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimelineGraphic : MaskableGraphic
{
    private struct Tick
    {
        public float time;
        public float heightScale;
        public float offset;
        public Color32 color;
        public string label;
    }

    private VertexHelper vh;

    [Header("Timeline")]
    public Color32 timelineColour = new Color32(255, 255, 255, 255);
    public float timelineThickness = 5;

    public float timeStart = 10;
    public float timeEnd = 20;

    [Header("Ticks")]
    public float tickHeight = 20;

    private List<Tick> ticks = new List<Tick>(52);

    /// <summary>
    /// Prepares the timeline for drawing
    /// </summary>
    public void ClearDraw()
    {
        ticks.Clear();
        SetVerticesDirty();
    }

    /// <summary>
    /// Inserts a tick into the timeline
    /// </summary>
    public void DrawTick(float time, float heightScale, float offset, Color32 color, string label = "")
    {
        if (time < timeStart || time > timeEnd)
        {
            return;
        }

        ticks.Add(new Tick()
        {
            time = time,
            heightScale = heightScale,
            offset = offset,
            color = color,
            label = label
        });
    }

    // Draws a tick
    private void DrawTickInternal(float time, float heightScale, float offset, Color32 color)
    {
        Vector2 centre = new Vector2(rectTransform.rect.xMin + (time - timeStart) / (timeEnd - timeStart) * rectTransform.rect.width, rectTransform.rect.center.y);
        
        DrawQuadInternal(
            centre - new Vector2(1f, tickHeight * heightScale / 2),
            centre + new Vector2(1f, tickHeight * heightScale / 2),
            color);
    }

    // Draws a quad into the current VertexHelper UI
    private void DrawQuadInternal(Vector2 min, Vector2 max, Color32 color)
    {
        UIVertex vert = new UIVertex();
        vert.color = color;
        int root = vh.currentVertCount;

        vert.position = new Vector3(min.x, max.y, 0f);
        vert.uv0 = new Vector2(0, 1);
        vh.AddVert(vert);
        vert.position = new Vector3(max.x, min.y, 0f);
        vert.uv0 = new Vector2(1, 0);
        vh.AddVert(vert);
        vert.position = new Vector3(min.x, min.y, 0f);
        vert.uv0 = new Vector2(0, 0);
        vh.AddVert(vert);
        vert.position = new Vector3(max.x, max.y, 0f);
        vert.uv0 = new Vector2(0, 0);
        vh.AddVert(vert);

        vh.AddTriangle(root + 0, root + 1, root + 2);
        vh.AddTriangle(root + 1, root + 3, root + 0);
    }

    // Handles main UI
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        this.vh = vh;
        vh.Clear();
        UIVertex vert = new UIVertex();
        vert.color = new Color32(255, 0, 0, 255);

        float minX = rectTransform.rect.xMin, minY = rectTransform.rect.yMin;
        float maxX = rectTransform.rect.xMax, maxY = rectTransform.rect.yMax;
        float centreY = rectTransform.rect.center.y;

        // draw main timeline
        DrawQuadInternal(new Vector2(minX, centreY - timelineThickness / 2), new Vector2(maxX, centreY + timelineThickness / 2), timelineColour);

        // draw ticks
        foreach (Tick tick in ticks)
        {
            DrawTickInternal(tick.time, tick.heightScale, tick.offset, tick.color);
        }
    }

    // Handles labels
    private void OnGUI()
    {
        GUI.color = Color.blue;

        // get screen coords
        Vector3 topLeft = rectTransform.TransformPoint(rectTransform.rect.xMin, rectTransform.rect.yMin, 0f);
        Vector3 bottomRight = rectTransform.TransformPoint(rectTransform.rect.xMax, rectTransform.rect.yMax, 0f);

        topLeft.y = Screen.height - topLeft.y;
        bottomRight.y = Screen.height - bottomRight.y;

        Rect pixelRect = new Rect(topLeft, bottomRight - topLeft);

        // draw beginning/end time
        GUI.color = timelineColour;
        GUI.Label(new Rect(topLeft.x, pixelRect.center.y, 0, 0), timeStart.ToString("F2"), new GUIStyle() { alignment = TextAnchor.MiddleRight });
        GUI.Label(new Rect(bottomRight.x, pixelRect.center.y, 50, 0), timeEnd.ToString("F2"), new GUIStyle() { alignment = TextAnchor.MiddleLeft });

        // draw tick labels
        foreach (Tick tick in ticks)
        {
            if (!string.IsNullOrEmpty(tick.label))
            {
                GUI.color = tick.color;
                GUI.Label(new Rect(pixelRect.xMin + pixelRect.width * (tick.time - timeStart) / (timeEnd - timeStart), pixelRect.yMax, 0, 0), tick.label);
            }
        }
    }
}
