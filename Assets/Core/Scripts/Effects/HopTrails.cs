using System.Collections.Generic;
using UnityEngine;

public class HopTrails : MonoBehaviour
{
    private Character character;
    public LineRenderer referenceRenderer;

    public int poolSize = 6;
    public float fadeDuration = 0.3f;
    public float hopCooldown = 0.1f;
    public float trailAlpha = 1f;
    public float hopSpeedThreshold = 60f;
    public float hopLengthThreshold = 3f;

    private List<LineRenderer> rendererPool = new List<LineRenderer>();

    private Vector3 lastPosition;
    private float lastHopTime;

    private void Start()
    {
        character = GetComponentInParent<Character>();

        Color clr = Color.white;// character.GetCharacterColour();
        clr.a = 0f;
        referenceRenderer.startColor = clr;
        referenceRenderer.endColor = clr;
        rendererPool.Add(referenceRenderer);

        for (int i = 1; i < poolSize; i++)
        {
            rendererPool.Add(Instantiate(referenceRenderer));
            rendererPool[i].startColor = clr;
            rendererPool[i].endColor = clr;
        }

        lastPosition = transform.position;
    }

    private void Update()
    {
        float fadeAmt = Time.deltaTime / Mathf.Max(fadeDuration, 0.0001f) * trailAlpha;
        Color neutralColour = Color.white;//character.GetCharacterColour();

        for (int i = 0; i < poolSize; i++)
        {
            if (rendererPool[i].startColor.a >= 0f)
            {
                neutralColour.a = Mathf.Max(rendererPool[i].startColor.a - fadeAmt, 0f);
                rendererPool[i].startColor = neutralColour;
                rendererPool[i].endColor = neutralColour;
            }
        }

        float hopDistance = Vector3.Distance(lastPosition, transform.position);
        if (hopDistance > hopSpeedThreshold * Time.deltaTime && hopDistance > hopLengthThreshold)
            AddTrail(lastPosition, transform.position);

        lastPosition = transform.position;
    }

    private void AddTrail(Vector3 start, Vector3 end)
    {
        if (Time.time - lastHopTime > hopCooldown)
        {
            for (int i = 0; i < rendererPool.Count; i++)
            {
                if (rendererPool[i].startColor.a == 0f)
                {
                    Color clr = rendererPool[i].startColor;
                    clr.a = trailAlpha;
                    rendererPool[i].startColor = clr;
                    rendererPool[i].endColor = clr;

                    rendererPool[i].positionCount = 2;
                    rendererPool[i].SetPosition(0, start);
                    rendererPool[i].SetPosition(1, end);
                }
            }

            lastHopTime = Time.time;
        }
    }
}
