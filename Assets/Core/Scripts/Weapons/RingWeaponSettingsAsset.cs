using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Ring Weapon", menuName = "Ring Weapon", order = 50)]
public class RingWeaponSettingsAsset : ScriptableObject, ILookupableAsset
{
    public RingWeaponSettings settings;

    /// <summary>
    /// Returns whether this weapon could be combined with all of the given weapon selections
    /// </summary>
    public bool CanBeCombinedWith(List<RingWeaponSettingsAsset> others)
    {
        if (others.Count == 0)
            return true;

        RingWeaponSettingsAsset currentPrimary = this;
        int numValidPrimariesFound = 0;
        int currentIndex = 0;
        int idealNumEffectors = others.Count; // ideal number of effectors is total number of weapons - 1 (note that "this" is a weapon too so we don't subtract 1 from "others")

        do
        {
            int numEffectors = 0;
            for (int i = 0; i < currentPrimary.settings.comboSettings.Length; i++)
            {
                if (others.Contains(currentPrimary.settings.comboSettings[i].effector))
                    numEffectors++;
            }

            if (System.Array.FindIndex(currentPrimary.settings.comboSettings, a => a.effector == this) != -1)
                numEffectors++; // include self

            if (numEffectors == idealNumEffectors)
                numValidPrimariesFound++;

            if (currentIndex < others.Count)
                currentPrimary = others[currentIndex++];
            else
                break;
        } while (true);

        return numValidPrimariesFound == 1;
    }
}