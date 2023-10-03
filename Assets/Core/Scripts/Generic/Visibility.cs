using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the visibility of an object, while considering the complexity of multiple things trying to control visibility at once
/// 
/// E.g. one component controls visibility based on whether you're in first-person, while the other controls visiblity depending on whether the item is dropped and blinking
/// 
/// Simply overriding renderer visibility can cause conflicts in these cases
/// 
/// Instead, this tracks the things that want to control visibility, how they want to control it, and which things don't currently want to affect the visibility.
/// </summary>
public class Visibility : MonoBehaviour
{
    public enum PriorityType
    {
        PrioritiseTrue,
        PrioritiseFalse
    }

    public struct Affector
    {
        public UnityEngine.Object affector;
        public bool value;
    }

    public bool autoPopulateRenderers = true;
    public Renderer[] affectedRenderers = new Renderer[0];
    public PriorityType visibilityPriority = PriorityType.PrioritiseTrue;

    [Header("Default")]
    public bool enableDefaultVisibility = true;
    public bool defaultVisibility = true;

    public List<Affector> affectors = new List<Affector>();

    private void LateUpdate()
    {
        affectors.RemoveAll(x => x.affector == null);

        if (affectors.Count > 0 || enableDefaultVisibility)
        {
            bool isVisible = false;

            if (affectors.Count > 0)
            {
                foreach (var affector in affectors)
                    isVisible |= affector.value;
            }
            else
            {
                isVisible = defaultVisibility;
            }

            foreach (Renderer renderer in affectedRenderers)
                renderer.enabled = isVisible;
        }   
    }

    public void Set(UnityEngine.Object requester, bool isVisible)
    {
        int existingIndex = affectors.FindIndex(x => x.affector == requester);
        if (existingIndex != -1)
            affectors[existingIndex] = new Affector() { affector = requester, value = isVisible };
        else
            affectors.Add(new Affector() { affector = requester, value = isVisible });
    }

    public void Unset(UnityEngine.Object requester)
    {
        affectors.RemoveAll(x => x.affector == requester);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && autoPopulateRenderers)
            affectedRenderers = GetComponentsInChildren<Renderer>();
    }
}
