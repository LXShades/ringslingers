using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    public bool hasWeapon;
    public bool isEquipped;

    public TextMeshProUGUI ammoRemaining;
    public Image icon;
    public Image equipHighlight;

    public Color colorWhenUnavailable = Color.black;
    public Color colorWhenAvailable = Color.white;
    public Color equipColorWhenAvailable = Color.white;
    public Color equipColorWhenUnavailable = Color.white;

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

    private void Start()
    {
        if (equipHighlight)
            equipHighlight.color = equipColorWhenAvailable;
    }

    private void Update()
    {
        ammoRemainingValue = (int)weapon.ammo;

        if (weapon.weaponType != null && icon.sprite != weapon.weaponType.settings.uiIcon)
            icon.sprite = weapon.weaponType.settings.uiIcon;

        if (hasWeapon)
        {
            if (!ammoRemaining.enabled)
            {
                ammoRemaining.enabled = true;
                icon.enabled = true;
            }

            if (icon.color != colorWhenAvailable)
            {
                icon.color = colorWhenAvailable;
                ammoRemaining.color = new Color(ammoRemaining.color.r, ammoRemaining.color.g, ammoRemaining.color.b, colorWhenAvailable.a);

                if (equipHighlight)
                    equipHighlight.color = equipColorWhenAvailable;
            }
        }
        else
        {
            if (icon.color != colorWhenUnavailable)
            {
                icon.color = colorWhenUnavailable;
                //ammoRemaining.color = new Color(ammoRemaining.color.r, ammoRemaining.color.g, ammoRemaining.color.b, colorWhenUnavailable.a);
                ammoRemaining.enabled = false;

                if (equipHighlight)
                    equipHighlight.color = equipColorWhenUnavailable;
            }
        }

        if (equipHighlight)
        {
            if (isEquipped != equipHighlight.enabled)
                equipHighlight.enabled = isEquipped;
        }
    }
}
