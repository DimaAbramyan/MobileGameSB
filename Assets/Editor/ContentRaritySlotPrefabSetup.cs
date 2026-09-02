using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ContentRaritySlotPrefabSetup
{
    private const string CraftPrefabPath = "Assets/PrefabUI/Buttons/Craft.prefab";
    private const string CommonSlotPath = "Assets/Waves/2/CommonSlot.png";
    private const string RareSlotPath = "Assets/Waves/2/RareSlot.png";
    private const string EpicSlotPath = "Assets/Waves/2/EpicSlot.png";
    private const string LegendarySlotPath = "Assets/Waves/2/LegendarySlot.png";
    private const string LockedSlotPath = "Assets/Waves/2/LockedSlot.png";
    private const string LockedSlotObjectName = "LockedSlot";

    public static void ConfigureCraftPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(CraftPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Could not load prefab at {CraftPrefabPath}.");
            return;
        }

        try
        {
            CraftUIButton craftButton = prefabRoot.GetComponent<CraftUIButton>();
            Button button = prefabRoot.GetComponentInChildren<Button>(true);
            if (craftButton == null || button == null || button.image == null)
            {
                Debug.LogError("Craft prefab needs CraftUIButton and a Button Image.");
                return;
            }

            ContentRaritySlotController slotController = prefabRoot
                .GetComponent<ContentRaritySlotController>();
            if (slotController == null)
            {
                slotController = prefabRoot.AddComponent<ContentRaritySlotController>();
            }

            Image lockedSlotImage = GetOrCreateLockedSlotImage(prefabRoot, button);

            SerializedObject slotSerializedObject = new SerializedObject(
                slotController);
            slotSerializedObject.FindProperty("slotImage").objectReferenceValue =
                button.image;
            slotSerializedObject.FindProperty("commonSlotTexture").objectReferenceValue =
                LoadSlotSprite(CommonSlotPath);
            slotSerializedObject.FindProperty("rareSlotTexture").objectReferenceValue =
                LoadSlotSprite(RareSlotPath);
            slotSerializedObject.FindProperty("epicSlotTexture").objectReferenceValue =
                LoadSlotSprite(EpicSlotPath);
            slotSerializedObject.FindProperty("legendarySlotTexture").objectReferenceValue =
                LoadSlotSprite(LegendarySlotPath);
            slotSerializedObject.FindProperty("lockedSlotImage").objectReferenceValue =
                lockedSlotImage;
            slotSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject buttonSerializedObject = new SerializedObject(craftButton);
            buttonSerializedObject.FindProperty("raritySlotController")
                .objectReferenceValue = slotController;
            buttonSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, CraftPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured rarity slot textures on Craft.prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Sprite LoadSlotSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"Could not load slot sprite at {path}.");

        return sprite;
    }

    private static Image GetOrCreateLockedSlotImage(
        GameObject prefabRoot,
        Button button)
    {
        Transform lockedSlotTransform = prefabRoot.transform.Find(LockedSlotObjectName);
        GameObject lockedSlotObject;
        if (lockedSlotTransform == null)
        {
            lockedSlotObject = new GameObject(
                LockedSlotObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            lockedSlotObject.transform.SetParent(prefabRoot.transform, false);
        }
        else
        {
            lockedSlotObject = lockedSlotTransform.gameObject;
        }

        RectTransform lockedSlotRect = lockedSlotObject.GetComponent<RectTransform>();
        RectTransform buttonRect = button.image.rectTransform;
        lockedSlotRect.anchorMin = buttonRect.anchorMin;
        lockedSlotRect.anchorMax = buttonRect.anchorMax;
        lockedSlotRect.anchoredPosition = buttonRect.anchoredPosition;
        lockedSlotRect.sizeDelta = buttonRect.sizeDelta;
        lockedSlotRect.pivot = buttonRect.pivot;
        lockedSlotRect.SetAsLastSibling();

        Image lockedSlotImage = lockedSlotObject.GetComponent<Image>();
        lockedSlotImage.sprite = LoadSlotSprite(LockedSlotPath);
        lockedSlotImage.raycastTarget = false;
        lockedSlotObject.SetActive(false);
        return lockedSlotImage;
    }
}
