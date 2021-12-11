using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MeshTrails : MonoBehaviour
{
    public Mesh trailMesh;
    public Material trailMaterial;

    public float duration;

    public float spaceBetweenMeshes = 0.1f;
    public Vector3 trailStart
    {
        get => _trailStart;
        set
        {
            if (_trailStart != value)
            {
                _trailStart = value;
                isTrailDirty = true;
            }
        }
    }
    public Vector3 trailEnd
    {
        get => _trailEnd;
        set
        {
            if (_trailEnd != value)
            {
                _trailEnd = value;
                isTrailDirty = true;
            }
        }
    }
    public Vector3 _trailStart;
    public Vector3 _trailEnd;
    public Vector3 startLocalRotation;
    public Vector3 rotationPerStep;

    public bool doFadeOut = true;

    private double spawnTime;

    private List<Matrix4x4> matrices = new List<Matrix4x4>(12);
    private MaterialPropertyBlock materialProps;

    private bool isTrailDirty = true;

    private void Awake()
    {
        spawnTime = Time.timeAsDouble;
    }

    private void Update()
    {
        float trailLength = Vector3.Distance(trailStart, trailEnd);
        if ((Time.timeAsDouble - spawnTime < duration || !Application.isPlaying) && trailLength > 0f)
        {
            if (isTrailDirty || !Application.isPlaying)
                RegenerateTrail();

            if (matrices.Count > 0)
            {
                if (doFadeOut)
                {
                    if (materialProps == null)
                        materialProps = new MaterialPropertyBlock();

                    Color materialColour = trailMaterial.color;

                    if (Application.isPlaying)
                        materialColour.a *= 1f - ((float)(Time.timeAsDouble - spawnTime) / duration);
                    else
                        materialColour.a *= 1f - (((float)(Time.timeAsDouble - spawnTime) % duration) / duration);

                    materialProps.SetColor("_Color", materialColour);
                }

                Graphics.DrawMeshInstanced(trailMesh, 0, trailMaterial, matrices, materialProps, UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer);
            }
        }
    }

    private void RegenerateTrail()
    {
        float trailLength = Vector3.Distance(trailStart, trailEnd);
        matrices.Clear();
        Quaternion quatRotationPerStep = Quaternion.Euler(rotationPerStep);
        Quaternion quatStartRotation = Quaternion.Euler(startLocalRotation);
        Quaternion baseRotation = quatStartRotation * Quaternion.LookRotation(trailEnd - trailStart);
        Quaternion rotation = Quaternion.identity;
        for (float p = 0; p < trailLength; p += spaceBetweenMeshes)
        {
            matrices.Add(Matrix4x4.TRS(Vector3.Lerp(trailStart, trailEnd, (float)p / ((int)(trailLength / spaceBetweenMeshes) * spaceBetweenMeshes)), baseRotation * rotation, transform.lossyScale));
            rotation = quatRotationPerStep * rotation;
        }
    }
}
