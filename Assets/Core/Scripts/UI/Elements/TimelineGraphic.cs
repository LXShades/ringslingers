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
        public int labelLine;
    }

    private VertexHelper vh;

    [Header("Timeline")]
    public Color32 timelineColour = new Color32(255, 255, 255, 255);
    public float timelineThickness = 5;

    public float timeStart = 10;
    public float timeEnd = 20;

    [Header("Ticks")]
    public float tickHeight = 20;

    [Header("Labels")]
    public int labelSize = 14;

    private List<Tick> ticks = new List<Tick>(52);

    private GUIStyle labelStyleLeft;
    private GUIStyle labelStyleRight;
    private GUIStyle labelStyleCentre;

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
    public void DrawTick(float time, float heightScale, float offset, Color32 color, string label = "", int labelLine = 0)
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
            label = label,
            labelLine = labelLine
        });
    }

    // Draws a tick
    private void DrawTickInternal(float time, float heightScale, float offset, Color32 color)
    {
        Vector2 centre = new Vector2(rectTransform.rect.xMin + (time - timeStart) / (timeEnd - timeStart) * rectTransform.rect.width, rectTransform.rect.center.y);

        centre.x = Mathf.Round(centre.x);
        centre.y = Mathf.Round(centre.y);

        DrawQuadInternal(
            centre + new Vector2(0f, tickHeight / 2 * (-heightScale - offset)),
            centre + new Vector2(1f, tickHeight / 2 * (heightScale - offset)),
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
        //if (labelStyleCentre == null)
        {
            InitStyles();
        }

        // get screen coords
        Vector3 topLeft = rectTransform.TransformPoint(rectTransform.rect.xMin, rectTransform.rect.yMin, 0f);
        Vector3 bottomRight = rectTransform.TransformPoint(rectTransform.rect.xMax, rectTransform.rect.yMax, 0f);

        topLeft.y = Screen.height - topLeft.y;
        bottomRight.y = Screen.height - bottomRight.y;

        Rect pixelRect = new Rect(topLeft, bottomRight - topLeft);

        // draw beginning/end time
        GUI.contentColor = timelineColour;
        GUI.Label(new Rect(topLeft.x, pixelRect.center.y, 0, 0), timeStart.ToString("F2"), labelStyleRight);
        GUI.Label(new Rect(bottomRight.x, pixelRect.center.y, 0, 0), timeEnd.ToString("F2"), labelStyleLeft);

        // draw tick labels
        foreach (Tick tick in ticks)
        {
            if (!string.IsNullOrEmpty(tick.label))
            {
                GUI.contentColor = tick.color;
                GUI.Label(new Rect(pixelRect.xMin + pixelRect.width * (tick.time - timeStart) / (timeEnd - timeStart), pixelRect.center.y + tickHeight / 2f * tick.heightScale + labelSize * 3 / 4 + tick.labelLine * (labelSize + 2), 0, 0), tick.label, labelStyleCentre);
            }
        }
    }

    // Initialises GUI styles
    private void InitStyles()
    {
        labelStyleLeft = new GUIStyle();
        labelStyleLeft.normal.textColor = Color.white;
        labelStyleLeft.alignment = TextAnchor.MiddleLeft;
        labelStyleLeft.fontSize = labelSize;

        labelStyleCentre = new GUIStyle();
        labelStyleCentre.normal.textColor = Color.white;
        labelStyleCentre.alignment = TextAnchor.MiddleCenter;
        labelStyleCentre.fontSize = labelSize;

        labelStyleRight = new GUIStyle();
        labelStyleRight.normal.textColor = Color.white;
        labelStyleRight.alignment = TextAnchor.MiddleRight;
        labelStyleRight.fontSize = labelSize;
    }
}
