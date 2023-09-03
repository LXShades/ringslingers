using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;


public class RingslingersBuildPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        foreach (BuildFile file in report.GetFiles())
        {
            string buildFolder = System.IO.Path.GetDirectoryName(file.path);
            if (buildFolder.EndsWith("_Data"))
            {
                string assetBundleFolder = $"{Application.dataPath}/../Builds/AssetBundles";

                if (Directory.Exists(assetBundleFolder))
                {
                    Debug.Log("Copying AssetBundles to build");

                    foreach (string assetBundlePath in Directory.EnumerateFiles(assetBundleFolder))
                        File.Copy(assetBundlePath, $"{buildFolder}/{System.IO.Path.GetFileName(assetBundlePath)}", true);
                }
                else
                {
                    Debug.LogWarning("Couldn't find built AssetBundles. You might need to build AssetBundles (Ringslingers menu) for the game to run.");
                }

                break;
            }
        }
    }
}
