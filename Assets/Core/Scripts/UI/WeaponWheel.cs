using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponWheel : MonoBehaviour
{
    public RingWeaponSettingsAsset[] ringWeapons = new RingWeaponSettingsAsset[0];
    public RingWeaponSettingsAsset defaultWeapon;

    public WeaponSlotUI weaponSlotPrefab;
    public Sprite weaponSelectionSprite;

    public float normalizedIconSize = 0.2f;
    public float normalizedWeaponOptionRadius = 0.9f;
    public float normalizedMinimumWeaponHighlightRadius = 0.5f;
    public float highlightedWeaponScale = 1.5f;
    public float weaponSelectionSpriteScale = 1.5f;

    private readonly List<WeaponSlotUI> spawnedWeaponSlots = new List<WeaponSlotUI>();
    private readonly List<Image> spawnedWeaponIcons = new List<Image>();
    private readonly List<bool> weaponAvailabilities = new List<bool>();
    private readonly List<Image> spawnedSelectionIcons = new List<Image>();

    private readonly List<RingWeaponSettingsAsset> selectedWeapons = new List<RingWeaponSettingsAsset>();

    private bool hasStartedSelecting = false;

    private void Start()
    {
        float radiansPerWeapon = Mathf.PI * 2f / ringWeapons.Length;

        for (int i = 0; i < ringWeapons.Length; i++)
        {
            // Spawn weapon slot and weapon availability
            WeaponSlotUI slot = Instantiate(weaponSlotPrefab);
            Vector2 normalizedPos = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Sin(radiansPerWeapon * i), Mathf.Cos(radiansPerWeapon * i)) * (normalizedWeaponOptionRadius * 0.5f);
            RectTransform slotRectTransform = slot.transform as RectTransform;
            RectTransform iconRectTransform = slot.icon.rectTransform;

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

            // Spawn selection icon
            Image selectionIcon = new GameObject("WeaponWheelSelection", new System.Type[] { typeof(CanvasRenderer), typeof(Image) }).GetComponent<Image>();
            RectTransform selectionIconRect = selectionIcon.rectTransform;

            selectionIcon.enabled = false;

            selectionIcon.sprite = weaponSelectionSprite;
            selectionIcon.preserveAspect = true;
            selectionIconRect.SetParent(spawnedWeaponSlots[i].transform, false);
            selectionIconRect.SetAsFirstSibling(); // im the boss now
            selectionIconRect.anchorMin = iconRectTransform.anchorMin;
            selectionIconRect.anchorMax = iconRectTransform.anchorMax;
            selectionIconRect.anchoredPosition = iconRectTransform.anchoredPosition;
            selectionIconRect.sizeDelta = iconRectTransform.sizeDelta;
            selectionIconRect.localScale = new Vector3(weaponSelectionSpriteScale, weaponSelectionSpriteScale, weaponSelectionSpriteScale);

            spawnedSelectionIcons.Add(selectionIcon);

            // and weapon availability
            weaponAvailabilities.Add(true);
        }
    }

    private void OnEnable()
    {
        hasStartedSelecting = false;
        selectedWeapons.Clear();

        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
            selectedWeapons.AddRange(shooting.localSelectedWeapons);
    }

    private void OnDisable()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
            shooting.localSelectedWeapons = selectedWeapons.ToArray();
    }

    private void Update()
    {
        UpdateWeaponAvailabilities();

        HandleWeaponMouseSelection();

        UpdateSelectionIcons();
    }

    private void UpdateWeaponAvailabilities()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
        {
            for (int j = 0; j < ringWeapons.Length; j++)
            {
                spawnedWeaponSlots[j].hasWeapon = false;
                spawnedWeaponSlots[j].weapon.ammo = 0f;

                for (int i = 0; i < shooting.weapons.Count; i++)
                {
                    if (shooting.weapons[i].weaponType == ringWeapons[j])
                    {
                        spawnedWeaponSlots[j].hasWeapon = true;
                        spawnedWeaponSlots[j].weapon.ammo = shooting.weapons[i].ammo;
                        break;
                    }
                }

                weaponAvailabilities[j] = spawnedWeaponSlots[j].hasWeapon;

                // default weapon ammo = rings in general
                if (ringWeapons[j] == defaultWeapon)
                    spawnedWeaponSlots[j].weapon.ammo = Netplay.singleton.localPlayer.numRings + 0.9f; // HACK, BIG HACK: time-based ring modes will tick down immediately causing flicker...lazy solution here.
            }
        }
    }

    private void HandleWeaponMouseSelection()
    {
        Vector2 mousePosition = Input.mousePosition;
        float normalizedMouseDistanceFromCentre = Vector2.Distance(new Vector2(transform.position.x, transform.position.y), mousePosition) / ((transform as RectTransform).sizeDelta.x * 0.5f);
        float closestDistance = float.MaxValue;
        int closestIndex = -1;

        for (int i = 0; i < spawnedWeaponIcons.Count; i++)
        {
            float distanceFromMouse = Vector2.Distance(mousePosition, spawnedWeaponIcons[i].transform.position);

            spawnedWeaponIcons[i].transform.localScale = new Vector3(1f, 1f, 1f);

            if (distanceFromMouse < closestDistance)
            {
                closestDistance = distanceFromMouse;
                closestIndex = i;
            }
        }

        if (normalizedMouseDistanceFromCentre > normalizedMinimumWeaponHighlightRadius)
        {
            if (closestIndex != -1 && weaponAvailabilities[closestIndex])
            {
                spawnedWeaponIcons[closestIndex].transform.localScale = new Vector3(highlightedWeaponScale, highlightedWeaponScale, 1f);

                if (!hasStartedSelecting)
                {
                    // when we first start selecting we'll clear all the selections first
                    hasStartedSelecting = true;
                    selectedWeapons.Clear();
                }

                if (!selectedWeapons.Contains(ringWeapons[closestIndex]) && Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
                    selectedWeapons.Add(ringWeapons[closestIndex]);
            }
        }
    }

    private void UpdateSelectionIcons()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
        {
            if (shooting.localSelectedWeapons != null)
            {
                // drop selection icons onto the weapons
                for (int i = 0; i < ringWeapons.Length; i++)
                {
                    RingWeaponSettingsAsset weapon = ringWeapons[i];
                    if (selectedWeapons.Contains(weapon) || selectedWeapons.Count == 0)
                    {
                        if (!spawnedSelectionIcons[i].enabled)
                            spawnedSelectionIcons[i].enabled = true;
                    }
                    else
                    {
                        if (spawnedSelectionIcons[i].enabled)
                            spawnedSelectionIcons[i].enabled = false;
                    }
                }
            }
        }
    }
}
