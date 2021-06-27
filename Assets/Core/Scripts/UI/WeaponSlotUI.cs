using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    public bool hasWeapon;
    public Text ammoRemaining;
    public Image icon;

    [Range(0f, 1f)]
    public float alphaWhenUnavailable = 0.25f;
    [Range(0f, 1f)]
    public float alphaWhenAvailable = 1f;

    private int ammoRemainingValue
    {
        get => _ammoRemainingString;
        set
        {
            if (_ammoRemainingString != value)
            {
                _ammoRemainingString = value;
                ammoRemaining.text = value.ToString();
            }
        }
    }
    private int _ammoRemainingString = int.MaxValue;

    [System.NonSerialized] public RingWeapon weapon;

    private void Update()
    {
        ammoRemainingValue = (int)weapon.ammo;

        if (hasWeapon)
        {
            if (icon.sprite != weapon.weaponType.settings.uiIcon)
                icon.sprite = weapon.weaponType.settings.uiIcon;

            if (!ammoRemaining.enabled)
            {
                ammoRemaining.enabled = true;
                icon.enabled = true;
            }

            if (icon.color.a != alphaWhenAvailable)
            {
                icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, alphaWhenAvailable);
                ammoRemaining.color = new Color(ammoRemaining.color.r, ammoRemaining.color.g, ammoRemaining.color.b, alphaWhenAvailable);

            }
        }
        else
        {
            if (ammoRemaining.enabled && alphaWhenUnavailable <= 0f)
            {
                ammoRemaining.enabled = false;
                icon.enabled = false;
            }
            else if (icon.color.a != alphaWhenUnavailable)
            {
                icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, alphaWhenUnavailable);
                ammoRemaining.color = new Color(ammoRemaining.color.r, ammoRemaining.color.g, ammoRemaining.color.b, alphaWhenUnavailable);
            }
        }
    }
}
