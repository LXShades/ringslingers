using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CreateMaterialFromMaterialDescription : AssetPostprocessor
{
    public void OnPreprocessAsset()
    {
        if (assetPath.ToLower().EndsWith("mtl"))
        {
            // ZoneBuilder exported materials are missing Default texture and Unity hates this
            try
            {
                string mtlText = File.ReadAllText(assetPath);

                if (!mtlText.ToLower().Contains("newmtl default"))
                {
                    mtlText += "\n\nnewmtl Default\nKd 1.0 1.0 1.0\n";
                    File.WriteAllText(assetPath, mtlText);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
