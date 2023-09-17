using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(LevelScreenshotter))]
public class LevelScreenshotterEditor : Editor
{
    [MenuItem("Tools/Level Screenshotter/Take screenshot from Scene Camera")]
    private static void TakeScreenshotFromSceneCamera()
    {
        LevelScreenshotter screenshotter = FindFirstObjectByType<LevelScreenshotter>();
        if (screenshotter != null)
        {
            Camera sceneCamera = UnityEditor.SceneView.lastActiveSceneView.camera;
            Undo.RecordObject(screenshotter.gameObject, "Repositioning screenshot camera");
            screenshotter.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
            EditorUtility.SetDirty(screenshotter.gameObject);
            TakeScreenshot(screenshotter);
        }
        else
        {
            EditorUtility.DisplayDialog("Failed", "Could not find level screenshotter in this scene", "OK");
        }
    }

    public override void OnInspectorGUI()
    {
        LevelScreenshotter targetScreenshotter = target as LevelScreenshotter;

        Camera attachedCamera = targetScreenshotter.GetComponent<Camera>();

        if (attachedCamera != null)
        {
            if (GUILayout.Button("Take Screenshot"))
                TakeScreenshot(target as LevelScreenshotter);
        }
    }

    private static void TakeScreenshot(LevelScreenshotter targetScreenshotter)
    {
        Camera attachedCamera = targetScreenshotter.GetComponent<Camera>();

        if (attachedCamera != null)
        {
            string scenePath = targetScreenshotter.gameObject.scene.path;
            RingslingersContentDatabase owningDatabase = null;
            List<MapConfiguration> owningMapConfigs = new List<MapConfiguration>();

            // Find the RingslingersContent folder for this map. If we find it, prefer to put the screenshots in that
            string screenshotDirectory = $"{System.IO.Path.GetDirectoryName(scenePath)}/LevelScreenshots";

            foreach (string assetGuid in AssetDatabase.FindAssets($"t:{nameof(RingslingersContentDatabase)}"))
            {
                RingslingersContentDatabase contentDatabase = AssetDatabase.LoadAssetAtPath<RingslingersContentDatabase>(AssetDatabase.GUIDToAssetPath(assetGuid));

                foreach (MapConfiguration map in contentDatabase.content.GetAllMaps())
                {
                    if (System.IO.Path.GetFullPath(scenePath) == System.IO.Path.GetFullPath(map.path))
                        owningMapConfigs.Add(map);
                }

                if (owningMapConfigs.Count > 0)
                {
                    owningDatabase = contentDatabase;
                    screenshotDirectory = $"{System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(contentDatabase))}/LevelScreenshots";
                    break;
                }
            }

            string screenshotName = $"SCR_{System.IO.Path.GetFileNameWithoutExtension(scenePath)}";
            string screenshotPath = $"{screenshotDirectory}/{screenshotName}.asset";

            // Make visible InvisibleInGame objects invisible
            List<Renderer> disabledRenderers = new List<Renderer>();
            foreach (InvisibleInGame go in FindObjectsByType<InvisibleInGame>(FindObjectsSortMode.None))
            {
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.enabled)
                    {
                        disabledRenderers.Add(renderer);
                        renderer.enabled = false;
                    }
                }
            }

            try
            {
                // Render onto the rendertexture
                RenderTexture renderTexture = new RenderTexture(targetScreenshotter.screenshotDimensions.x, targetScreenshotter.screenshotDimensions.y, 24, RenderTextureFormat.ARGB32, 1);

                attachedCamera.targetTexture = renderTexture;
                attachedCamera.Render();

                // Transfer it to a Texture2D
                RenderTexture oldRenderTexture = RenderTexture.active;
                RenderTexture.active = renderTexture;

                Texture2D textureToSave = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false, false, false);
                textureToSave.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                textureToSave.Apply();

                RenderTexture.active = oldRenderTexture;

                // Save the asset
                if (!Directory.Exists(screenshotDirectory))
                    Directory.CreateDirectory(screenshotDirectory);

                Sprite spriteToSave = Sprite.Create(textureToSave, new Rect(0, 0, textureToSave.width, textureToSave.height), Vector2.zero);
                spriteToSave.name = screenshotName;

                AssetDatabase.CreateAsset(textureToSave, screenshotPath);
                AssetDatabase.AddObjectToAsset(spriteToSave, textureToSave);

                // Add it to the content database if we found one
                foreach (MapConfiguration mapConfig in owningMapConfigs)
                    mapConfig.screenshot = spriteToSave;

                if (owningDatabase != null)
                    EditorUtility.SetDirty(owningDatabase);
                AssetDatabase.SaveAssets();

                if (owningMapConfigs.Count > 0)
                    Debug.Log($"Created screenshot {screenshotName} in {screenshotDirectory} and pinned it to the level config in the content database. Click to select.", spriteToSave);
                else
                    Debug.LogWarning($"Created screenshot {screenshotName} in {screenshotDirectory}, but could not pin it to the level config, we couldn't find this level in any content database.", spriteToSave);

                attachedCamera.targetTexture = null;
                DestroyImmediate(renderTexture);
            }
            finally
            {
                // Re-enable renderers we switched off
                foreach (var renderer in disabledRenderers)
                    renderer.enabled = true;
            }

            // Cleanup
            attachedCamera.targetTexture = null;
        }
    }
}
