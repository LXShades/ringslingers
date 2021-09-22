using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LineGraphic : MaskableGraphic
{
    public Color32 colour = new Color32(255, 255, 255, 255);
    public float thickness = 5f;
    public List<Vector2> points = new List<Vector2>();

    public void Redraw()
    {
        SetVerticesDirty();
    }

    private void DrawQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
    {
        UIVertex vert = new UIVertex();
        vert.color = color;
        int root = vh.currentVertCount;

        vert.position = new Vector3(a.x, a.y, 0f);
        vert.uv0 = new Vector2(0, 1);
        vh.AddVert(vert);
        vert.position = new Vector3(b.x, b.y, 0f);
        vert.uv0 = new Vector2(1, 0);
        vh.AddVert(vert);
        vert.position = new Vector3(c.x, c.y, 0f);
        vert.uv0 = new Vector2(0, 0);
        vh.AddVert(vert);
        vert.position = new Vector3(d.x, d.y, 0f);
        vert.uv0 = new Vector2(0, 0);
        vh.AddVert(vert);

        vh.AddTriangle(root + 0, root + 1, root + 2);
        vh.AddTriangle(root + 0, root + 2, root + 3);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        vh.Clear();

        float halfThickness = thickness * 0.5f;
        if (points.Count >= 2)
        {
            Vector2 lastCross = new Vector2(points[1].y - points[0].y, points[0].x - points[1].x).normalized * halfThickness;
            Vector2 lastU = points[0] + lastCross;
            Vector2 lastD = points[0] - lastCross;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 nextCross = i + 1 < points.Count ? new Vector2(points[i + 1].y - points[i].y, points[i].x - points[i + 1].x).normalized * halfThickness : lastCross;
                Vector2 nextU = points[i] + nextCross;
                Vector2 nextD = points[i] - nextCross;

                DrawQuad(vh, lastU, nextU, nextD, lastD, colour);

                lastU = nextU;
                lastD = nextD;
            }
        }
    }
}
