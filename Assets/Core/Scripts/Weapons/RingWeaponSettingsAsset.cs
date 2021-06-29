using UnityEngine;

[CreateAssetMenu(fileName = "Ring Weapon", menuName = "Ring Weapon", order = 50)]
public class RingWeaponSettingsAsset : ScriptableObject, ILookupableAsset
{
    public RingWeaponSettings settings;
}