using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CarrySlotHUD : MonoBehaviour
{
    public Carryable.CarrySlot carrySlot;
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    private int lastQuantity = -1;

    private void Update()
    {
        Character hudPlayer = GameManager.singleton.camera?.currentPlayer;
        if (hudPlayer)
        {
            List<Carryable> carriedStuff = Carryable.GetAllCarriedByPlayer(hudPlayer);
            int nextQuantity = 0;

            foreach (Carryable carryable in carriedStuff)
            {
                if (carryable.carrySlot == carrySlot)
                    nextQuantity++;
            }

            if (nextQuantity != lastQuantity)
            {
                lastQuantity = nextQuantity;
                iconImage.enabled = quantityText.enabled = nextQuantity > 0;
                quantityText.text = $"x{nextQuantity.ToString()}";
            }
        }
    }
}
