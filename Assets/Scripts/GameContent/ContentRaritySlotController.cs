using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ContentRaritySlotController : MonoBehaviour
{
    [SerializeField] private Image slotImage;
    [SerializeField] private Image lockedSlotImage;

    [Header("Slot Textures")]
    [SerializeField] private Sprite commonSlotTexture;
    [SerializeField] private Sprite rareSlotTexture;
    [SerializeField] private Sprite epicSlotTexture;
    [SerializeField] private Sprite legendarySlotTexture;

    public void SetLocked(bool isLocked)
    {
        if (lockedSlotImage != null)
            lockedSlotImage.gameObject.SetActive(isLocked);
    }

    public void Apply(CraftContentDefinition content)
    {
        if (content != null)
            Apply(content.Rarity);
    }

    public void Apply(ContentRarity rarity)
    {
        if (slotImage == null)
            return;

        Sprite texture = rarity switch
        {
            ContentRarity.Rare => rareSlotTexture,
            ContentRarity.Epic => epicSlotTexture,
            ContentRarity.Legendary => legendarySlotTexture,
            _ => commonSlotTexture
        };

        if (texture != null)
            slotImage.sprite = texture;
    }
}
