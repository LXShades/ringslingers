using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeaponWheel : MonoBehaviour
{
    public RingWeaponSettingsAsset[] ringWeapons = new RingWeaponSettingsAsset[0];

    public WeaponSlotUI weaponSlotPrefab;
    public LineGraphic selectionLine;

    public Image noWeaponIcon;
    public float noWeaponIconUnselectedOpacity = 0.25f;
    public float noWeaponIconSelectedOpacity = 1f;

    public float normalizedIconSize = 0.2f;
    public float normalizedWeaponOptionRadius = 0.9f;
    public float normalizedMinimumWeaponHighlightRadius = 0.5f;
    public float highlightedWeaponScale = 1.5f;
    public float weaponSelectionSpriteScale = 1.5f;

    public bool requireClickToSelect = true;

    private readonly List<WeaponSlotUI> spawnedWeaponSlots = new List<WeaponSlotUI>();
    private readonly List<Image> spawnedWeaponIcons = new List<Image>();
    private readonly List<bool> weaponAvailabilities = new List<bool>();
    private readonly List<bool> weaponCompatibilities = new List<bool>();

    private readonly List<RingWeaponSettingsAsset> selectedWeapons = new List<RingWeaponSettingsAsset>();
    private readonly List<int> selectedWeaponIndexes = new List<int>();

    private bool hasStartedSelecting = false;
    private bool hasSelectedNakedWeapon = false;

    private void Start()
    {
        float radiansPerWeapon = Mathf.PI * 2f / ringWeapons.Length;

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

            // and weapon availability
            weaponAvailabilities.Add(true);
            weaponCompatibilities.Add(true);
        }
    }

    private void OnEnable()
    {
        if (Netplay.singleton && Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
            selectedWeapons.AddRange(shooting.localSelectedWeapons);
    }

    private void OnDisable()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
        {
            if (hasSelectedNakedWeapon)
                shooting.localSelectedWeapons = new RingWeaponSettingsAsset[] { shooting.defaultWeapon.weaponType };
            else
                shooting.localSelectedWeapons = selectedWeapons.ToArray();
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
                spawnedWeaponSlots[j].hasWeapon = false;
                spawnedWeaponSlots[j].weapon.ammo = 0f;

                if (weaponCompatibilities[j] || !Mouse.current.leftButton.isPressed)
                {
                    for (int i = 0; i < shooting.weapons.Count; i++)
                    {
                        if (shooting.weapons[i].weaponType == ringWeapons[j])
                        {
                            spawnedWeaponSlots[j].hasWeapon = true;
                            spawnedWeaponSlots[j].weapon.ammo = shooting.weapons[i].ammo;
                            break;
                        }
                    }
                }

                weaponAvailabilities[j] = spawnedWeaponSlots[j].hasWeapon;
            }
        }
    }

    private void HandleWeaponMouseSelection()
    {
        Vector2 mousePosition = Input.mousePosition;
        float normalizedMouseDistanceFromCentre = Vector2.Distance(new Vector2(transform.position.x, transform.position.y), mousePosition) / ((transform as RectTransform).sizeDelta.x * 0.5f);
        float closestDistance = float.MaxValue;
        int closestIndex = -1;
        bool isNakedWeaponHighlighted = false;

        if (normalizedMouseDistanceFromCentre > normalizedMinimumWeaponHighlightRadius)
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
        
        if (normalizedMouseDistanceFromCentre <= normalizedMinimumWeaponHighlightRadius / 2 && (selectedWeaponIndexes.Count == 0 || (!Mouse.current.leftButton.isPressed && requireClickToSelect)))
        {
            noWeaponIcon.transform.localScale = new Vector3(highlightedWeaponScale, highlightedWeaponScale, 0f);
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

        if (Mouse.current.leftButton.isPressed)
        {
            // draw the selection line
            selectionLine.points.Clear();

            if (selectedWeaponIndexes.Count > 0)
            {
                for (int i = 0; i < selectedWeaponIndexes.Count; i++)
                    selectionLine.points.Add(selectionLine.transform.InverseTransformPoint(spawnedWeaponIcons[selectedWeaponIndexes[i]].transform.position));
            }
            else
            {
                selectionLine.points.Add(Vector2.zero);
            }

            selectionLine.points.Add(selectionLine.transform.InverseTransformPoint(mousePosition));
            selectionLine.Redraw();
        }
        else if (!Mouse.current.leftButton.isPressed && selectionLine.points.Count > 0 && selectedWeapons.Count == 0)
        {
            // clear the selection line
            selectionLine.points.Clear();
            selectionLine.Redraw();
        }

        // Highlight selected weapons
        for (int i = 0; i < spawnedWeaponIcons.Count; i++)
            spawnedWeaponIcons[i].transform.localScale = new Vector3(1f, 1f, 1f);

        for (int i = 0; i < selectedWeaponIndexes.Count; i++)
            spawnedWeaponIcons[selectedWeaponIndexes[i]].transform.localScale = new Vector3(highlightedWeaponScale, highlightedWeaponScale, 1f);

        // Highlight hovered weapon
        if (closestIndex != -1)
            spawnedWeaponIcons[closestIndex].transform.localScale = new Vector3(highlightedWeaponScale, highlightedWeaponScale, 1f);
    }

    private void UpdateSelectionIcons()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out CharacterShooting shooting))
        {
            if (shooting.localSelectedWeapons != null)
            {
                // drop selection icons onto the weapons
                for (int i = 0; i < ringWeapons.Length; i++)
                    spawnedWeaponSlots[i].isEquipped = selectedWeapons.Contains(ringWeapons[i]) || (shooting.localSelectedWeapons.Length == 0);
            }
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
