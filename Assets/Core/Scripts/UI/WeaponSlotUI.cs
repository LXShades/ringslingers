using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    public Text ammoRemaining;
    public Image icon;

    [System.NonSerialized] public EquippedRingWeapon weapon;

    private void Update()
    {
        if (weapon != null)
        {
            if (icon.sprite != weapon.weaponType.uiIcon)
                icon.sprite = weapon.weaponType.uiIcon;
            if (((int)weapon.ammo).ToString() != ammoRemaining.text)
                ammoRemaining.text = ((int)weapon.ammo).ToString();
            if (!ammoRemaining.enabled)
            {
                ammoRemaining.enabled = true;
                icon.enabled = true;
            }
        }
        else
        {
            if (ammoRemaining.enabled)
            {
                ammoRemaining.enabled = false;
                icon.enabled = false;
            }
        }
    }
}
