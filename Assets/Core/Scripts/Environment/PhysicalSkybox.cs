using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[DefaultExecutionOrder(99999)]
public class PhysicalSkybox : MonoBehaviour
{
    private Camera camera;

    public Vector3 parallaxOrigin;
    public float parallaxPower = 0.01f;
    public bool followEditorCamera = true;

    private void Awake()
    {
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.Nothing;
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && !followEditorCamera)
            return;

        if (camera == null)
            camera = Camera.main;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            camera = SceneView.lastActiveSceneView.camera;
        }
#endif
        transform.position = camera.transform.position - (camera.transform.position - parallaxOrigin) * parallaxPower;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(parallaxOrigin, new Vector3(5, 5, 5));
    }
}
