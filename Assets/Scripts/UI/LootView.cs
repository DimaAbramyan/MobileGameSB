using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LootView : MonoBehaviour
{
    [SerializeField] private Image lootImage;
    [SerializeField] private TMP_Text amountText;

    public void Set(PlayerResource resource)
    {
        if (resource == null)
            return;

        if (lootImage != null)
            lootImage.sprite = resource.Icon;

        if (amountText != null)
            amountText.text = resource.Amount.ToString();
    }
}
