using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeaponWheel : MonoBehaviour
{
    public RingWeaponSettingsAsset[] ringWeapons = new RingWeaponSettingsAsset[0];

    public float normalizedIconSize = 0.2f;
    public float normalizedWeaponOptionRadius = 0.9f;
    public float normalizedMinimumWeaponHighlightRadius = 0.5f;
    public float highlightedWeaponScale = 1.5f;

    [Range(0f, 1f)]
    public float availableWeaponAlpha = 1f;
    [Range(0f, 1f)]
    public float unavailableWeaponAlpha = 0.25f;

    private readonly List<Image> spawnedWeaponIcons = new List<Image>();
    private readonly List<bool> weaponAvailabilities = new List<bool>();

    private void Start()
    {
        float radiansPerWeapon = Mathf.PI * 2f / ringWeapons.Length;

        for (int i = 0; i < ringWeapons.Length; i++)
        {
            Image icon = new GameObject($"WeaponWheelOption {ringWeapons[i].settings.name}", new System.Type[] { typeof(RectTransform), typeof (CanvasRenderer), typeof(Image) }).GetComponent<Image>();
            Vector2 normalizedPos = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Sin(radiansPerWeapon * i), Mathf.Cos(radiansPerWeapon * i)) * (normalizedWeaponOptionRadius * 0.5f);

            icon.sprite = ringWeapons[i].settings.uiIcon;
            icon.rectTransform.SetParent(transform);
            icon.rectTransform.anchorMin = normalizedPos;
            icon.rectTransform.anchorMax = normalizedPos;
            icon.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            icon.rectTransform.sizeDelta = (transform as RectTransform).sizeDelta * normalizedIconSize;

            spawnedWeaponIcons.Add(icon);
            weaponAvailabilities.Add(true);
        }
    }

    private void Update()
    {
        UpdateWeaponAvailabilities();

        UpdateWeaponVisibilities();

        HandleWeaponMouseSelection();
    }

    private void UpdateWeaponAvailabilities()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
        {
            for (int j = 0; j < ringWeapons.Length; j++)
            {
                weaponAvailabilities[j] = false;

                for (int i = 0; i < shooting.weapons.Count; i++)
                {
                    if (shooting.weapons[i].weaponType == ringWeapons[j])
                    {
                        weaponAvailabilities[j] = true;
                        break;
                    }
                }
            }
        }
    }

    private void UpdateWeaponVisibilities()
    {
        for (int j = 0; j < ringWeapons.Length; j++)
        {
            float alpha = weaponAvailabilities[j] ? availableWeaponAlpha : unavailableWeaponAlpha;
            if (spawnedWeaponIcons[j].color.a != alpha)
                spawnedWeaponIcons[j].color = new Color(spawnedWeaponIcons[j].color.r, spawnedWeaponIcons[j].color.g, spawnedWeaponIcons[j].color.b, alpha);
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

                if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.TryGetComponent(out RingShooting shooting))
                    shooting.localSelectedWeapon = ringWeapons[closestIndex];
            }
        }
    }
}
