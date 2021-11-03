using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeaponWheel : MonoBehaviour
{
    public RingWeaponSettingsAsset[] ringWeapons = new RingWeaponSettingsAsset[0];

    public Image weaponWheelSlicePrefab;
    public float sliceFillRatio = 0.9f;
    public WeaponSlotUI weaponSlotPrefab;

    public Image noWeaponBackground;
    public Image noWeaponIcon;
    public float noWeaponIconUnselectedOpacity = 0.25f;
    public float noWeaponIconSelectedOpacity = 1f;

    public float normalizedIconSize = 0.2f;
    public float normalizedWeaponOptionRadius = 0.9f;
    public Color highlightedSliceColour = Color.white;
    public Color selectedSliceColour = Color.yellow;

    public bool requireClickToSelect = true;

    private readonly List<WeaponSlotUI> spawnedWeaponSlots = new List<WeaponSlotUI>();
    private readonly List<Image> spawnedWeaponWheelSlices = new List<Image>();
    private readonly List<Image> spawnedWeaponIcons = new List<Image>();
    private readonly List<bool> weaponCompatibilities = new List<bool>();

    private readonly List<RingWeaponSettingsAsset> selectedWeapons = new List<RingWeaponSettingsAsset>();
    private readonly List<int> selectedWeaponIndexes = new List<int>();

    private bool hasStartedSelecting = false;
    private bool hasSelectedNakedWeapon = false;

    private void Start()
    {
        float radiansPerWeapon = Mathf.PI * 2f / ringWeapons.Length;

        // Spawn circle slice background
        for (int i = 0; i < ringWeapons.Length; i++)
        {
            Image slice = Instantiate(weaponWheelSlicePrefab);

            slice.transform.SetParent(transform, false);
            slice.fillAmount = 1f / ringWeapons.Length * sliceFillRatio;

            slice.transform.rotation = Quaternion.Euler(0f, 0f, -(i - 0.5f) * 360f / ringWeapons.Length - 360f / ringWeapons.Length * (1f - sliceFillRatio) * 0.5f);
            slice.transform.SetAsFirstSibling();

            spawnedWeaponWheelSlices.Add(slice);
        }

        // Spawn weapon slots
        for (int i = 0; i < ringWeapons.Length; i++)
        {
            // Spawn weapon slot and weapon availability
            WeaponSlotUI slot = Instantiate(weaponSlotPrefab);
            Vector2 normalizedPos = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Sin(radiansPerWeapon * i), Mathf.Cos(radiansPerWeapon * i)) * (normalizedWeaponOptionRadius * 0.5f);
            RectTransform slotRectTransform = slot.transform as RectTransform;

            slot.gameObject.name = $"WeaponWheelOption {ringWeapons[i].settings.name}";
            slot.weapon.weaponType = ringWeapons[i];
            slot.hasWeapon = true; // I guess
            slotRectTransform.SetParent(transform);
            slotRectTransform.anchorMin = normalizedPos;
            slotRectTransform.anchorMax = normalizedPos;
            slotRectTransform.anchoredPosition = new Vector2(0f, 0f);
            slotRectTransform.sizeDelta = (transform as RectTransform).sizeDelta * normalizedIconSize;

            spawnedWeaponSlots.Add(slot);
            spawnedWeaponIcons.Add(slot.icon);

            weaponCompatibilities.Add(true);
        }
    }

    private void OnEnable()
    {
        hasStartedSelecting = false;
        if (Netplay.singleton && Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
            selectedWeapons.AddRange(shooting.equippedWeapons);
    }

    private void OnDisable()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
        {
            if (hasSelectedNakedWeapon)
                shooting.LocalSetSelectedWeapons(new RingWeaponSettingsAsset[] { shooting.defaultWeapon.weaponType });
            else
                shooting.LocalSetSelectedWeapons(selectedWeapons);
        }
    }

    private void Update()
    {
        UpdateWeaponAvailabilities();

        HandleWeaponMouseSelection();

        UpdateSelectionIcons();
    }

    private void UpdateWeaponAvailabilities()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
        {
            for (int j = 0; j < ringWeapons.Length; j++)
            {
                spawnedWeaponSlots[j].hasWeapon = weaponCompatibilities[j] || !Mouse.current.leftButton.isPressed;
                spawnedWeaponSlots[j].weapon.ammo = 0f;

                for (int i = 0; i < shooting.weapons.Count; i++)
                {
                    if (shooting.weapons[i].weaponType == ringWeapons[j])
                    {
                        spawnedWeaponSlots[j].weapon.ammo = shooting.weapons[i].ammo;
                        break;
                    }
                }
            }
        }
    }

    private void HandleWeaponMouseSelection()
    {
        Vector2 mousePosition = Input.mousePosition;
        float mouseDistanceFromCentre = Vector2.Distance(new Vector2(transform.position.x, transform.position.y), mousePosition);
        float noWeaponRadius = (noWeaponBackground.rectTransform.rect.width * noWeaponBackground.rectTransform.lossyScale.x) * 0.5f;
        float closestDistance = float.MaxValue;
        int closestIndex = -1;
        bool isNakedWeaponHighlighted = false;

        if (mouseDistanceFromCentre > noWeaponRadius)
        {
            for (int i = 0; i < spawnedWeaponIcons.Count; i++)
            {
                float distanceFromMouse = Vector2.Distance(mousePosition, spawnedWeaponIcons[i].transform.position);

                if (distanceFromMouse < closestDistance)
                {
                    closestDistance = distanceFromMouse;
                    closestIndex = i;
                }
            }
        }
        
        if (mouseDistanceFromCentre <= noWeaponRadius && !(selectedWeaponIndexes.Count > 0 && hasStartedSelecting))
        {
            noWeaponIcon.color = new Color(1, 1, 1, noWeaponIconSelectedOpacity);
            isNakedWeaponHighlighted = true;
        }
        else
        {
            float opacity = noWeaponIconUnselectedOpacity; // if we have other weapons selected hide the noweapons bit

            if (selectedWeapons.Count >= 0 && (!requireClickToSelect || Mouse.current.leftButton.isPressed))
                opacity = 0f;

            if (noWeaponIcon.transform.localScale != Vector3.one)
                noWeaponIcon.transform.localScale = Vector3.one;
            if (noWeaponIcon.color.a != opacity)
                noWeaponIcon.color = new Color(1, 1, 1, opacity);
        }

        if (!requireClickToSelect || Mouse.current.leftButton.isPressed)
        {
            // clear weapons when starting selection
            if ((!requireClickToSelect && !hasStartedSelecting) || (requireClickToSelect && Mouse.current.leftButton.wasPressedThisFrame))
            {
                // when we first start selecting we'll clear all the selections first
                ClearSelections();

                hasStartedSelecting = true;
                hasSelectedNakedWeapon = isNakedWeaponHighlighted;
            }

            if (closestIndex != -1)
            {
                // add weapons as we hover over them
                if (!selectedWeapons.Contains(ringWeapons[closestIndex]))
                {
                    selectedWeapons.Add(ringWeapons[closestIndex]);
                    selectedWeaponIndexes.Add(closestIndex);

                    // update weapon compatibilities
                    for (int i = 0; i < spawnedWeaponSlots.Count; i++)
                    {
                        if (!selectedWeapons.Contains(spawnedWeaponSlots[i].weapon.weaponType))
                            weaponCompatibilities[i] = spawnedWeaponSlots[i].weapon.weaponType.CanBeCombinedWith(selectedWeapons);
                    }
                }

                // if we've selected a proper weapon, we're no longer naked
                hasSelectedNakedWeapon = false;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && selectedWeapons.Count == 0)
        {
            for (int i = 0; i < weaponCompatibilities.Count; i++)
                weaponCompatibilities[i] = true;
        }

        // Unhighlight selected weapons
        for (int i = 0; i < spawnedWeaponIcons.Count; i++)
            spawnedWeaponWheelSlices[i].color = weaponWheelSlicePrefab.color;

        // Highlight hovered weapon
        if (closestIndex != -1)
            spawnedWeaponWheelSlices[closestIndex].color = highlightedSliceColour;

        // Highlight selected weapons
        for (int i = 0; i < selectedWeaponIndexes.Count; i++)
            spawnedWeaponWheelSlices[selectedWeaponIndexes[i]].color = selectedSliceColour;
    }

    private void UpdateSelectionIcons()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
        {
            // drop selection icons onto the weapons
            for (int i = 0; i < ringWeapons.Length; i++)
                spawnedWeaponSlots[i].isEquipped = selectedWeapons.Contains(ringWeapons[i]) || (selectedWeapons.Count == 0 && !hasSelectedNakedWeapon);
        }
    }

    private void ClearSelections()
    {
        hasStartedSelecting = false;
        selectedWeapons.Clear();
        selectedWeaponIndexes.Clear();
        hasSelectedNakedWeapon = false;

        for (int i = 0; i < weaponCompatibilities.Count; i++)
            weaponCompatibilities[i] = true;
    }
}
