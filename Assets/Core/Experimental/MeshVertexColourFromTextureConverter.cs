using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scans all objects in the scene with affectedShader as their shader and colours their vertices based on their textures
/// </summary>
public class MeshVertexColourFromTextureConverter : MonoBehaviour
{
    public Shader affectedShader;

    [Range(0, 250)]
    public float fadeStart;
    [Range(0, 250)]
    public float fadeEnd;

    HashSet<Mesh> affectedMeshes = new HashSet<Mesh>();

    private void Awake()
    {
        foreach (MeshFilter filter in FindObjectsOfType<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            if (mesh && renderer && !affectedMeshes.Contains(mesh))
            {
                Vector2[] uvs = mesh.uv;
                Color32[] colours = new Color32[mesh.vertexCount];

                for (int m = 0; m < mesh.subMeshCount; m++)
                {
                    if (m >= renderer.sharedMaterials.Length)
                        break; // submeshes doesn't match material count

                    Material mat = renderer?.sharedMaterials[m];
                    Texture2D tex = mat.GetTexture("_MainTex") as Texture2D;

                    if (mat && tex && mat.shader == affectedShader)
                    {
                        if (!tex.isReadable)
                            continue;

                        int width = tex.width, height = tex.height;
                        var submesh = mesh.GetSubMesh(m);
                        Color32[] pixels = tex.GetPixels32();

                        if (pixels.Length == 0)
                            continue;

                        for (int i = 0; i < submesh.vertexCount; i++)
                        {
                            Vector2 uv = uvs[submesh.firstVertex + i];
                            uv = new Vector2((uv.x % 1f + 1f) % 1f, (uv.y % 1f + 1f) % 1f);

                            colours[submesh.firstVertex + i] = pixels[(int)((uv.y * height) % height) * width + (int)((uv.x * width) % width)];
                        }

                        mat.SetFloat("_FadeStart", fadeStart);
                        mat.SetFloat("_FadeEnd", fadeEnd);
                    }
                }

                mesh.SetColors(colours);
                affectedMeshes.Add(mesh);
            }
        }
    }
}