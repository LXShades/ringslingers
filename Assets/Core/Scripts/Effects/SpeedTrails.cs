using System.Collections.Generic;
using UnityEngine;

public class SpeedTrails : MonoBehaviour
{
    [Header("Hierarchy")]
    public MeshFilter trailRendererObject;

    [Header("Passive trails")]
    public bool enablePassiveSpeedTrail = false;
    public AnimationCurve opacityBySpeed = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Thok pulse")]
    public float thokPulseDuration = 0.1f;

    [Header("Rolling")]
    public float opacityWhileRolling = 0.4f;

    [Header("Misc")]
    public float thickness = 0.8f;
    public float fadeOutTime = 0.5f;

    private bool hasPulsedSinceLand = false;

    private Timer thokPulseProgress = new Timer();

    private Character character;
    private PlayerCharacterMovement movement;

    public struct TrailPoint
    {
        public TrailPoint(Vector3 position, float opacity)
        {
            this.position = position;
            this.opacity = opacity;
            this.time = Time.time;
        }

        public float opacity;
        public Vector3 position;
        public float time;
    }

    public int numFrontCirclePoints = 6;
    private int numTrailPoints = 600;
    private List<TrailPoint> trailPoints = new List<TrailPoint>(600);

    GameObject trailObject;
    private Mesh trailMesh;
    private List<Vector3> trailVertices = new List<Vector3>(600 * 2);
    private List<Color32> trailColours = new List<Color32>(600 * 2);
    private List<int> trailIndices = new List<int>(600 * 6);

    private void Start()
    {
        movement = GetComponentInParent<PlayerCharacterMovement>();
        character = GetComponentInParent<Character>();

        trailRendererObject.transform.parent = null;
        trailRendererObject.transform.position = Vector3.zero;
        trailRendererObject.transform.rotation = Quaternion.identity;
        trailRendererObject.transform.localScale = Vector3.one;

        for (int i = 0; i < numTrailPoints - 1; i++)
        {
            int baseVert = i * 2;
            trailIndices.Add(baseVert);
            trailIndices.Add(baseVert + 2);
            trailIndices.Add(baseVert + 1);
            trailIndices.Add(baseVert + 2);
            trailIndices.Add(baseVert + 3);
            trailIndices.Add(baseVert + 1);
        }

        for (int i = 0; i < numTrailPoints * 2; i++)
            trailVertices.Add(Vector3.zero);
        for (int i = 0; i < numTrailPoints * 2; i++)
            trailColours.Add(new Color32(255, 0, 0, 255));

        trailMesh = new Mesh();
        trailRendererObject.sharedMesh = trailMesh;
    }

    private void OnDestroy()
    {
        Destroy(trailMesh);
        Destroy(trailObject);
    }

    private void Update()
    {
        float opacity = 0f;

        // thok pulses
        if (movement.baseState == CharacterMovementState.Thokked)
        {
            if (!hasPulsedSinceLand)
            {
                hasPulsedSinceLand = true;
                thokPulseProgress.Start(thokPulseDuration);
            }
        }
        else
        {
            hasPulsedSinceLand = false;
        }

        // decide opacity
        if (enablePassiveSpeedTrail && movement)
            opacity = Mathf.Max(opacity, opacityBySpeed.Evaluate(movement.velocity.magnitude));

        if (thokPulseProgress.isRunning)
            opacity = Mathf.Max(opacity, 1f - thokPulseProgress.progress);

        if (movement.baseState == CharacterMovementState.Rolling)
            opacity = Mathf.Max(opacity, opacityWhileRolling);

        //if (opacity >= 0.05f)
            TryAddTrailPoint(transform.position, opacity);

        Vector3 forward = -GameManager.singleton.camera.transform.forward;
        Color32 colour = character.GetCharacterColour();

        float t = Time.time;
        for (int i = 0; i < trailPoints.Count; i++)
        {
            float a = 1f - (t - trailPoints[i].time) / fadeOutTime;

            if (a <= 0.05f)
            {
                // reached the oldest faded part of the trail
                trailPoints.RemoveRange(i, trailPoints.Count - i);
                break;
            }

            trailColours[i * 2] = new Color32(colour.r, colour.g, colour.b, (byte)(trailPoints[i].opacity * a * 255f));
            trailColours[i * 2 + 1] = new Color32(colour.r, colour.g, colour.b, (byte)(trailPoints[i].opacity * a * 255f));
        }

        if (trailPoints.Count > 0)
        {
            Vector3 up = default;
            float halfThickness = thickness / 2f;
            for (int i = 0; i < trailPoints.Count; i++)
            {
                if (i > 0 && i < trailPoints.Count - 1)
                {
                    up = Vector3.Cross(trailPoints[i + 1].position - trailPoints[i - 1].position, forward);
                    up = up * halfThickness / Mathf.Max(up.magnitude, 0.0001f);
                }
                if (i < trailPoints.Count - 1)
                {
                    up = Vector3.Cross(trailPoints[i + 1].position - trailPoints[i].position, forward);
                    up = up * halfThickness / Mathf.Max(up.magnitude, 0.0001f);
                }

                trailVertices[i * 2] = trailPoints[i].position + up;
                trailVertices[i * 2 + 1] = trailPoints[i].position - up;
            }
        }

        trailMesh.Clear();

        if (trailPoints.Count > 0)
        {
            trailMesh.SetVertices(trailVertices, 0, trailPoints.Count * 2);
            trailMesh.SetColors(trailColours, 0, trailPoints.Count * 2);
            trailMesh.SetIndices(trailIndices, 0, (trailPoints.Count - 1) * 6, MeshTopology.Triangles, 0);
        }
    }

    private void TryAddTrailPoint(Vector3 position, float opacity = 1f)
    {
        if (trailPoints.Count >= numTrailPoints)
            return;

        if (trailPoints.Count > 1)
        {
            if (Vector3.Distance(position, trailPoints[1].position) <= 0.2f)
            {
                trailPoints[0] = new TrailPoint() {
                    position = position,
                    opacity = opacity,
                    time = Time.time
                };
            }
            else
            {
                trailPoints.Insert(0, new TrailPoint()
                {
                    position = position,
                    opacity = opacity,
                    time = Time.time
                });
                trailPoints[1] = trailPoints[0];
            }
        }
        else
        {
            trailPoints.Add(new TrailPoint()
            {
                position = position,
                opacity = opacity,
                time = Time.time
            });
        }
    }
}
