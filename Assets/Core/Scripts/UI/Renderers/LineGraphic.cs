using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LineGraphic : ExtendedGraphic
{
    public Color32 colour = new Color32(255, 255, 255, 255);
    public float thickness = 5f;
    public List<Vector2> points = new List<Vector2>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        vh.Clear();

        DrawLines(vh, points, colour, thickness);
    }
}
