using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Mirror2023Initialiser
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void InitReadWriteStuff()
    {
        Debug.Log("Running workaround for Unity 2023 not running Mirror generated initialisers");

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var generatedReaderWriterClass = assembly.GetTypes().FirstOrDefault(x => x.Name == "GeneratedNetworkCode" && x.Namespace.Contains("Mirror"));

            if (generatedReaderWriterClass != null)
            {
                var theFunction = generatedReaderWriterClass.GetMethod("InitReadWriters", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (theFunction != null)
                {
                    theFunction.Invoke(null, new object[0]);
                }
            }
        }
    }
}