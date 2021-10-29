using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshMaterialSplitter : MonoBehaviour
{
    [Header("Settings")]
    public GameObject sourceMesh;
    public GameObject submeshPrefab;
    public float scale = 1f / 64f;
    public bool removeDoubleSidedFaces = true;

    [System.Serializable]
    public struct SubobjectSettings
    {
        public string name;
        public List<string> materials;
        public bool shouldDelete;
        public bool shouldHaveCollider;
    }

    public SubobjectSettings baseSettings;
    [UnityEngine.Serialization.FormerlySerializedAsAttribute("categories")]
    public List<SubobjectSettings> subobjectSettings = new List<SubobjectSettings>();

    public GameObject[] PerformConversion()
    {
        // Generate the subobjects
        GameObject[] outSubobjects = new GameObject[subobjectSettings.Count + 1];
        Mesh[] outMeshes = new Mesh[subobjectSettings.Count + 1];
        List<Material>[] outMaterials = new List<Material>[subobjectSettings.Count + 1];
        List<SubMeshDescriptor>[] outSubmeshes = new List<SubMeshDescriptor>[subobjectSettings.Count + 1];
        List<List<int>>[] outIndices = new List<List<int>>[subobjectSettings.Count + 1];
        List<Vector2>[] outUvs = new List<Vector2>[subobjectSettings.Count + 1];
        List<Vector3>[] outVertices = new List<Vector3>[subobjectSettings.Count + 1];

        for (int i = 0; i < subobjectSettings.Count + 1; i++)
        {
            outSubobjects[i] = Instantiate(submeshPrefab);
            outSubobjects[i].transform.position = Vector3.zero;
            outSubobjects[i].transform.rotation = Quaternion.identity;
            outSubobjects[i].name = i == 0 ? baseSettings.name : subobjectSettings[i - 1].name;
            outSubmeshes[i] = new List<SubMeshDescriptor>();
            outMaterials[i] = new List<Material>();
            outIndices[i] = new List<List<int>>();
            outUvs[i] = new List<Vector2>();
            outVertices[i] = new List<Vector3>();
            outMeshes[i] = new Mesh();
        }


        Mesh inputMesh = sourceMesh.GetComponentInChildren<MeshFilter>().sharedMesh;
        Material[] inputMaterials = sourceMesh.GetComponentInChildren<MeshRenderer>().sharedMaterials;
        List<Vector3> inputVertices = new List<Vector3>();
        List<Vector2> inputUvs = new List<Vector2>();
        Matrix4x4 vertexTransform = sourceMesh.GetComponentInChildren<MeshFilter>().transform.localToWorldMatrix;
        vertexTransform = vertexTransform * Matrix4x4.Scale(new Vector3(scale, scale, scale));

        inputMesh.GetVertices(inputVertices);
        inputMesh.GetUVs(0, inputUvs);

        // Fill the meshes
        for (int sub = 0; sub < inputMesh.subMeshCount; sub++)
        {
            int target = 0;

            for (int j = 0; j < subobjectSettings.Count; j++)
            {
                if (subobjectSettings[j].materials.Contains(inputMaterials[sub].name))
                    target = j + 1;
            }

            // add the submesh to target
            SubMeshDescriptor inSubmesh = inputMesh.GetSubMesh(sub);
            int totalIndices = 0;
            for (int fff = 0; fff < outIndices[target].Count; fff++)
                totalIndices += outIndices[target][fff].Count;
            outSubmeshes[target].Add(new SubMeshDescriptor()
            {
                baseVertex = outVertices[target].Count,
                firstVertex = outVertices[target].Count,
                indexStart = totalIndices,
                indexCount = inSubmesh.indexCount,
                topology = inSubmesh.topology,
                bounds = inSubmesh.bounds,
                vertexCount = inSubmesh.vertexCount
            });

            for (int v = 0; v < inSubmesh.vertexCount; v++)
            {
                outVertices[target].Add(vertexTransform.MultiplyPoint(inputVertices[inSubmesh.firstVertex + v]));
                outUvs[target].Add(inputUvs[inSubmesh.firstVertex + v]);
            }

            outIndices[target].Add(new List<int>(inputMesh.GetIndices(sub, true)));

            for (int idx = 0; idx < inSubmesh.indexCount; idx++)
                outIndices[target][outIndices[target].Count - 1][idx] -= inSubmesh.firstVertex;

            outMaterials[target].Add(inputMaterials[sub]);
        }

        // Remove double-sided bits
        if (removeDoubleSidedFaces)
        {
            int[] quad = new int[6];
            for (int subObj = 0; subObj < outIndices.Length; subObj++)
            {
                for (int subMesh = 0; subMesh < outIndices[subObj].Count; subMesh++)
                {
                    List<int> indices = outIndices[subObj][subMesh];
                    int oldNumIndices = indices.Count;
                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        // triangles
                        for (int j = i + 3; j < indices.Count; j += 3)
                        {
                            if (   (indices[j  ] == indices[i] || indices[j  ] == indices[i+1] || indices[j  ] == indices[i+2])
                                && (indices[j+1] == indices[i] || indices[j+1] == indices[i+1] || indices[j+1] == indices[i+2])
                                && (indices[j+2] == indices[i] || indices[j+2] == indices[i+1] || indices[j+2] == indices[i+2]))
                            {
                                indices.RemoveRange(j, 3); // this is a backface or duplicate face
                                j -= 3; // for next iteration
                            }
                        }

                        // quads
                        if (i + 6 <= indices.Count)
                        {
                            quad[0] = indices[i];
                            quad[1] = indices[i+1];
                            quad[2] = indices[i+2];

                            int numUniques = 3;
                            if (indices[i+3] != indices[i] && indices[i+3] != indices[i+1] && indices[i+3] != indices[i+2])
                                quad[numUniques++] = indices[i+3];
                            if (indices[i+4] != indices[i] && indices[i+4] != indices[i+1] && indices[i+4] != indices[i+2])
                                quad[numUniques++] = indices[i+4];
                            if (indices[i+5] != indices[i] && indices[i+5] != indices[i+1] && indices[i+5] != indices[i+2])
                                quad[numUniques++] = indices[i+5];

                            if (numUniques == 4)
                            {
                                // it's probably a quad, scan for other quads that overlap this
                                for (int j = i + 6; j + 6 <= indices.Count; j += 3)
                                {
                                    bool isOverlapping = true;
                                    for (int k = j; k < j + 6; k++)
                                        isOverlapping &= indices[k] == quad[0] || indices[k] == quad[1] || indices[k] == quad[2] || indices[k] == quad[3];

                                    if (isOverlapping)
                                    {
                                        indices.RemoveRange(j, 6); // this is a backface or duplicate face
                                        j -= 3; // for next iteration
                                    }
                                }
                            }
                        }
                    }

                    if (oldNumIndices != indices.Count)
                    {
                        SubMeshDescriptor dangit = outSubmeshes[subObj][subMesh];
                        dangit.indexCount = indices.Count;
                        outSubmeshes[subObj][subMesh] = dangit;

                        for (int next = subMesh + 1; next < outSubmeshes[subObj].Count; next++)
                        {
                            SubMeshDescriptor dangitAgain = outSubmeshes[subObj][next];
                            dangitAgain.indexStart += indices.Count - oldNumIndices;
                            outSubmeshes[subObj][next] = dangitAgain;
                        }
                    }
                }
            }
        }

        // Assign the meshes
        for (int i = 0; i < outSubobjects.Length; i++)
        {
            outMeshes[i].SetVertices(outVertices[i]);
            outMeshes[i].SetUVs(0, outUvs[i].ToArray());
            outMeshes[i].subMeshCount = outSubmeshes[i].Count;

            for (int sub = 0; sub < outSubmeshes[i].Count; sub++)
                outMeshes[i].SetIndices(outIndices[i][sub].ToArray(), outSubmeshes[i][sub].topology, sub);
            outMeshes[i].SetSubMeshes(outSubmeshes[i].ToArray());

            outMeshes[i].RecalculateNormals();
            outMeshes[i].RecalculateBounds();
            outMeshes[i].RecalculateTangents();

            outSubobjects[i].GetComponent<MeshFilter>().sharedMesh = outMeshes[i];
            outSubobjects[i].GetComponent<MeshRenderer>().materials = outMaterials[i].ToArray();
        }

        // Parent under new "Converted" object
        GameObject parent = new GameObject($"{sourceMesh.name}-Converted");

        foreach (GameObject obj in outSubobjects)
            obj.transform.SetParent(parent.transform);

        // Post process
        for (int i = 0; i < outSubobjects.Length; i++)
        {
            SubobjectSettings settings = i > 0 ? subobjectSettings[i - 1] : baseSettings;

            if (settings.shouldDelete)
            {
                GameObject.DestroyImmediate(outSubobjects[i]);
            }
            else
            {
                if (settings.shouldHaveCollider)
                    outSubobjects[i].AddComponent<MeshCollider>().sharedMesh = outSubobjects[i].GetComponent<MeshFilter>().sharedMesh;
            }
        }

        return outSubobjects;
    }
}
