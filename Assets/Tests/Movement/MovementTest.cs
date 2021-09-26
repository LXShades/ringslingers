using UnityEngine;
using UnityEngine.UI;

public class MovementCapsuleTest : MonoBehaviour
{
    private Movement movement;
    private MeshFilter meshFilter;

    public Material debugMaterial;
    public int numStepsToMake = 1;
    public float stepSize = 0.3f;
    public bool usePenetration = false;

    public Text debugText;

    void Awake()
    {
        movement = GetComponent<Movement>();
        meshFilter = GetComponent<MeshFilter>();
    }

    void Update()
    {
        MovementDebugStats.Snapshot stats = MovementDebugStats.total;
        Vector3 initialPosition = transform.position;

        for (int i = 0; i < numStepsToMake; i++)
        {
            movement.Move(transform.forward * stepSize);

            Graphics.DrawMesh(meshFilter.sharedMesh, transform.localToWorldMatrix, debugMaterial, gameObject.layer, null, 0, null, false, false, false);
        }

        stats = MovementDebugStats.total.Since(stats);
        if (debugText)
            debugText.text = stats.ToString();

        transform.position = initialPosition;
        transform.rotation = Quaternion.identity;
    }
}
