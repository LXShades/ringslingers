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
    /** 
     Examples of conflicts between multiple components fighting for visibility:
    - Invincibility blink (turn on/off)
    - First person mode on local character (needs to be off)
     */
    public struct Affector
    {
        public UnityEngine.Object affector;
        public bool value;
        public int priority;
    }

    public bool autoPopulateRenderers = true;
    public Renderer[] affectedRenderers = new Renderer[0];

    [Header("Default")]
    public bool enableDefaultVisibility = true;
    public bool defaultVisibility = true;

    public List<Affector> affectors = new List<Affector>();

    private bool hasChanged = true;

    private void LateUpdate()
    {
        int lastCount = affectors.Count;
        affectors.RemoveAll(x => x.affector == null);
        hasChanged |= lastCount != affectors.Count;

        if (hasChanged)
        {
            if (affectors.Count > 0 || enableDefaultVisibility)
            {
                bool isVisible = false;

                if (affectors.Count > 0)
                {
                    int highestPrioritySoFar = -1;
                    foreach (var affector in affectors)
                    {
                        if (affector.priority > highestPrioritySoFar)
                        {
                            isVisible = affector.value;
                            highestPrioritySoFar = affector.priority;
                        }
                        else if (affector.priority == highestPrioritySoFar)
                        {
                            isVisible |= affector.value;
                        }
                    }
                }
                else
                {
                    isVisible = defaultVisibility;
                }

                foreach (Renderer renderer in affectedRenderers)
                    renderer.enabled = isVisible;
            }

            hasChanged = false;
        }
    }

    /// <summary>
    /// Sets the visibility. The requester should be a related calling object. Requests of the same priority will combine together, favoring isVisibile=true. Requests of higher priority are overridden.
    /// </summary>
    public void Set(UnityEngine.Object requester, bool isVisible, int priority = 0)
    {
        int existingIndex = affectors.FindIndex(x => x.affector == requester);
        if (existingIndex != -1)
        {
            if (affectors[existingIndex].value != isVisible || affectors[existingIndex].priority != priority)
            {
                affectors[existingIndex] = new Affector() { affector = requester, value = isVisible, priority = priority };
                hasChanged = true;
            }
        }
        else
        {
            affectors.Add(new Affector() { affector = requester, value = isVisible, priority = priority });
            hasChanged = true;
        }
    }

    /// <summary>
    /// Tells the component that this requester no longer wants to affect the visibility of the object.
    /// </summary>
    public void Unset(UnityEngine.Object requester)
    {
        int idx = affectors.FindIndex(x => x.affector == requester);

        if (idx != -1)
        {
            affectors.RemoveAt(idx);
            hasChanged = true;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && autoPopulateRenderers)
            affectedRenderers = GetComponentsInChildren<Renderer>();
    }
}
