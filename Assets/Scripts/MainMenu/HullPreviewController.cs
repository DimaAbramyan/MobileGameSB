using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class HullPreviewController : MonoBehaviour
{
    private const string PreviewSurfaceName = "Hull Preview Surface";
    private static readonly int PrimaryColorId = Shader.PropertyToID("_PrimaryColor");
    private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");

    [SerializeField] private Camera mainMenuCamera;
    [SerializeField] private RectTransform previewHost;
    [SerializeField] private CraftWeaponSlotButton weaponSlotButtonPrefab;
    [SerializeField, Range(8, 31)] private int previewLayer = 12;
    [SerializeField, Min(64)] private int textureResolution = 512;
    [SerializeField, Min(0f)] private float framingPadding = 0.15f;

    private Camera previewCamera;
    private RawImage previewImage;
    private RenderTexture renderTexture;
    private GameObject previewInstance;
    private int mainMenuCameraCullingMask;
    private bool shouldRestoreMainMenuCameraMask;
    private readonly List<Material> previewMaterials = new();
    private readonly Dictionary<Material, Material> materialCopies = new();
    private readonly List<PreviewWeaponSlot> previewWeaponSlots = new();
    private readonly Dictionary<string, PreviewWeaponSlot> previewWeaponSlotsById = new();
    private readonly List<CraftWeaponSlotDefinition> slotDefinitions = new();
    private CraftCreationFlowController craftCreationFlow;

    private sealed class PreviewWeaponSlot
    {
        public string SlotId;
        public Transform WeaponMount;
        public Transform ButtonAnchor;
        public Vector2 ButtonOffset;
        public Vector2 ButtonSize;
        public Vector2 WeaponIconScale;
        public CraftWeaponSlotButton Button;
        public GameObject WeaponPreview;
    }

    private void Awake()
    {
        DisablePreviewRootRaycasts();
        MoveToPreviewHost();
        ConfigurePreviewSurface();
        CreatePreviewCamera();
    }

    private void DisablePreviewRootRaycasts()
    {
        Button rootButton = GetComponent<Button>();
        if (rootButton != null)
            rootButton.enabled = false;

        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
            rootImage.raycastTarget = false;
    }

    private void OnEnable()
    {
        if (previewCamera != null)
            previewCamera.enabled = true;
    }

    private void OnDisable()
    {
        if (previewCamera != null)
            previewCamera.enabled = false;
    }

    private void OnDestroy()
    {
        Clear();
        RestoreMainMenuCameraMask();

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (previewCamera != null)
            Destroy(previewCamera.gameObject);
    }

    public void Show(HullContentDefinition hull)
    {
        Show(hull, hull != null ? hull.DefaultColorPalette : null);
    }

    public void Show(HullContentDefinition hull, ShipColorPalette palette)
    {
        Show(hull, palette, null);
    }

    public void Show(
        HullContentDefinition hull,
        ShipColorPalette palette,
        CraftCreationFlowController flow)
    {
        Clear();
        if (hull == null || hull.Prefab == null)
            return;

        previewInstance = Instantiate(hull.Prefab, Vector3.zero, Quaternion.identity);
        SetLayerRecursively(previewInstance.transform, previewLayer);
        CreatePreviewMaterials();
        ApplyPalette(palette);
        FramePreview(previewInstance);
        CreateWeaponSlotButtons(flow, hull);
    }

    public void ApplyPalette(ShipColorPalette palette)
    {
        if (palette == null)
            return;

        for (int i = 0; i < previewMaterials.Count; i++)
        {
            Material material = previewMaterials[i];
            if (material == null)
                continue;

            if (material.HasProperty(PrimaryColorId))
                material.SetColor(PrimaryColorId, palette.primary);

            if (material.HasProperty(SecondaryColorId))
                material.SetColor(SecondaryColorId, palette.secondary);

            if (material.HasProperty(AccentColorId))
                material.SetColor(AccentColorId, palette.accent);
        }
    }

    public void Clear()
    {
        ClearWeaponSlotButtons();
        DestroyPreviewMaterials();

        if (previewInstance != null)
            Destroy(previewInstance);

        previewInstance = null;
    }

    private void LateUpdate()
    {
        UpdateWeaponSlotPositions();
    }

    private void MoveToPreviewHost()
    {
        if (previewHost == null)
        {
            Debug.LogError("Hull preview requires a persistent CraftMenu preview host.", this);
            return;
        }

        if (transform.parent == previewHost)
            return;

        transform.SetParent(previewHost, true);
        transform.SetAsLastSibling();
    }

    private void ConfigurePreviewSurface()
    {
        Transform existingSurface = transform.Find(PreviewSurfaceName);
        if (existingSurface != null)
            previewImage = existingSurface.GetComponent<RawImage>();

        if (previewImage == null)
        {
            GameObject surface = new GameObject(PreviewSurfaceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform surfaceTransform = surface.GetComponent<RectTransform>();
            surfaceTransform.SetParent(transform, false);
            surfaceTransform.anchorMin = Vector2.zero;
            surfaceTransform.anchorMax = Vector2.one;
            surfaceTransform.offsetMin = Vector2.zero;
            surfaceTransform.offsetMax = Vector2.zero;

            previewImage = surface.GetComponent<RawImage>();
        }

        previewImage.raycastTarget = false;
        previewImage.color = Color.white;
        previewImage.transform.SetAsLastSibling();
    }

    private void CreatePreviewCamera()
    {
        renderTexture = new RenderTexture(textureResolution, textureResolution, 24)
        {
            name = "Hull Preview Render Texture",
            antiAliasing = 1
        };
        renderTexture.Create();

        GameObject cameraObject = new GameObject("Hull Preview Camera");
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Color.clear;
        previewCamera.cullingMask = 1 << previewLayer;
        previewCamera.targetTexture = renderTexture;
        previewCamera.transform.position = new Vector3(0f, 0f, -10f);

        ExcludePreviewLayerFromMainMenuCamera();

        previewImage.texture = renderTexture;
    }

    private void ExcludePreviewLayerFromMainMenuCamera()
    {
        if (mainMenuCamera == null)
        {
            Debug.LogError("Hull preview requires the main menu camera reference.", this);
            return;
        }

        mainMenuCameraCullingMask = mainMenuCamera.cullingMask;
        mainMenuCamera.cullingMask = mainMenuCameraCullingMask & ~(1 << previewLayer);
        shouldRestoreMainMenuCameraMask = true;
    }

    private void RestoreMainMenuCameraMask()
    {
        if (!shouldRestoreMainMenuCameraMask || mainMenuCamera == null)
            return;

        mainMenuCamera.cullingMask = mainMenuCameraCullingMask;
        shouldRestoreMainMenuCameraMask = false;
    }

    private void CreatePreviewMaterials()
    {
        if (previewInstance == null)
            return;

        Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
                continue;

            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
                continue;

            Material[] instanceMaterials = new Material[sourceMaterials.Length];
            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = sourceMaterials[materialIndex];
                if (sourceMaterial == null)
                    continue;

                if (!materialCopies.TryGetValue(sourceMaterial, out Material materialCopy))
                {
                    materialCopy = new Material(sourceMaterial)
                    {
                        name = sourceMaterial.name + " (Hull Preview)"
                    };
                    materialCopies.Add(sourceMaterial, materialCopy);
                    previewMaterials.Add(materialCopy);
                }

                instanceMaterials[materialIndex] = materialCopy;
            }

            renderer.sharedMaterials = instanceMaterials;
        }
    }

    private void DestroyPreviewMaterials()
    {
        for (int i = 0; i < previewMaterials.Count; i++)
        {
            if (previewMaterials[i] != null)
                Destroy(previewMaterials[i]);
        }

        previewMaterials.Clear();
        materialCopies.Clear();
    }

    private void CreateWeaponSlotButtons(
        CraftCreationFlowController flow,
        HullContentDefinition hull)
    {
        if (flow == null)
            return;

        craftCreationFlow = flow;
        slotDefinitions.Clear();

        HullLoadoutDefinition loadout = previewInstance != null
            ? previewInstance.GetComponent<HullLoadoutDefinition>()
            : null;

        if (loadout != null)
        {
            CollectSlotsFromLoadout(loadout, hull);
        }
        else
        {
            CollectSlotsFromLegacyAnchors();
        }

        craftCreationFlow.SetWeaponSlotDefinitions(slotDefinitions);

        if (slotDefinitions.Count == 0)
            return;

        if (weaponSlotButtonPrefab == null || previewImage == null)
        {
            Debug.LogError("Hull preview requires a CraftWeaponSlotButton prefab.", this);
            return;
        }

        craftCreationFlow.WeaponAssignmentChanged += HandleWeaponAssignmentChanged;
        craftCreationFlow.WeaponSlotFocusChanged += HandleWeaponSlotFocusChanged;

        for (int i = 0; i < previewWeaponSlots.Count; i++)
        {
            PreviewWeaponSlot slot = previewWeaponSlots[i];
            string slotId = slot.SlotId;

            CraftWeaponSlotButton button = Instantiate(weaponSlotButtonPrefab, previewImage.rectTransform);
            button.Initialize(() => craftCreationFlow?.SelectWeaponSlot(slotId));
            button.SetWeaponIconScale(slot.WeaponIconScale);
            slot.Button = button;
            previewWeaponSlotsById.Add(slotId, slot);

            WeaponContentDefinition weapon = craftCreationFlow.GetWeaponForSlot(slotId);
            button.SetWeapon(weapon);
            ReplaceWeaponPreview(slot, weapon);
        }

        HandleWeaponSlotFocusChanged(craftCreationFlow.FocusedWeaponSlotId);
        UpdateWeaponSlotPositions();
    }

    private void CollectSlotsFromLoadout(
        HullLoadoutDefinition loadout,
        HullContentDefinition hull)
    {
        if (!loadout.IsConfigurationValid(out string error))
        {
            Debug.LogWarning(
                $"Hull loadout on '{previewInstance.name}' is invalid: {error}",
                previewInstance);
            return;
        }

        if (hull != null
            && hull.Data != null
            && loadout.ShipData != null
            && loadout.ShipData != hull.Data)
        {
            Debug.LogWarning(
                $"Hull loadout ShipData does not match HullContentDefinition '{hull.DisplayName}'.",
                previewInstance);
        }

        int hullLevel = loadout.ShipData != null
            ? loadout.ShipData.currentLvl
            : 0;
        CraftWeaponSlotAnchor[] slotAnchors = previewInstance.GetComponentsInChildren<CraftWeaponSlotAnchor>(true);
        IReadOnlyList<HullWeaponPlatform> platforms = loadout.WeaponPlatforms;
        for (int i = 0; i < platforms.Count; i++)
        {
            HullWeaponPlatform platform = platforms[i];
            Vector2 weaponIconScale = GetWeaponIconScale(
                slotAnchors,
                platform.SlotId,
                platform.PreviewWeaponIconScale);
            Vector3 localPosition = previewInstance.transform.InverseTransformPoint(
                platform.WeaponMount.position);
            slotDefinitions.Add(new CraftWeaponSlotDefinition(
                platform.SlotId,
                localPosition,
                platform.GetMaxWeaponTier(hullLevel)));
            previewWeaponSlots.Add(new PreviewWeaponSlot
            {
                SlotId = platform.SlotId,
                WeaponMount = platform.WeaponMount,
                ButtonAnchor = platform.WeaponMount,
                ButtonOffset = platform.PreviewButtonOffset,
                ButtonSize = platform.PreviewButtonSize,
                WeaponIconScale = weaponIconScale
            });
        }
    }

    private static Vector2 GetWeaponIconScale(
        IReadOnlyList<CraftWeaponSlotAnchor> anchors,
        string slotId,
        Vector2 fallbackScale)
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            CraftWeaponSlotAnchor anchor = anchors[i];
            if (anchor != null && anchor.SlotId == slotId)
                return anchor.WeaponIconScale;
        }

        return fallbackScale;
    }

    private void CollectSlotsFromLegacyAnchors()
    {
        CraftWeaponSlotAnchor[] anchors = previewInstance != null
            ? previewInstance.GetComponentsInChildren<CraftWeaponSlotAnchor>(true)
            : System.Array.Empty<CraftWeaponSlotAnchor>();

        for (int i = 0; i < anchors.Length; i++)
        {
            CraftWeaponSlotAnchor anchor = anchors[i];
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.SlotId))
            {
                Debug.LogWarning(
                    "A craft weapon slot anchor has no slot id and was ignored.",
                    previewInstance);
                continue;
            }

            if (ContainsSlot(slotDefinitions, anchor.SlotId))
            {
                Debug.LogWarning(
                    $"The hull preview has duplicate weapon slot id '{anchor.SlotId}'.",
                    previewInstance);
                continue;
            }

            Vector3 localPosition = previewInstance.transform.InverseTransformPoint(
                anchor.WeaponMount.position);
            slotDefinitions.Add(new CraftWeaponSlotDefinition(
                anchor.SlotId,
                localPosition));
            previewWeaponSlots.Add(new PreviewWeaponSlot
            {
                SlotId = anchor.SlotId,
                WeaponMount = anchor.WeaponMount,
                ButtonAnchor = anchor.transform,
                ButtonOffset = anchor.ButtonOffset,
                ButtonSize = anchor.ButtonSize,
                WeaponIconScale = anchor.WeaponIconScale
            });
        }
    }

    private void ClearWeaponSlotButtons()
    {
        if (craftCreationFlow != null)
        {
            craftCreationFlow.WeaponAssignmentChanged -= HandleWeaponAssignmentChanged;
            craftCreationFlow.WeaponSlotFocusChanged -= HandleWeaponSlotFocusChanged;
            craftCreationFlow.ClearWeaponSlots();
            craftCreationFlow = null;
        }

        for (int i = 0; i < previewWeaponSlots.Count; i++)
        {
            PreviewWeaponSlot slot = previewWeaponSlots[i];
            if (slot.WeaponPreview != null)
                Destroy(slot.WeaponPreview);

            if (slot.Button != null)
                Destroy(slot.Button.gameObject);
        }

        previewWeaponSlots.Clear();
        previewWeaponSlotsById.Clear();
        slotDefinitions.Clear();
    }

    private void HandleWeaponAssignmentChanged(string slotId, WeaponContentDefinition weapon)
    {
        if (string.IsNullOrEmpty(slotId)
            || !previewWeaponSlotsById.TryGetValue(slotId, out PreviewWeaponSlot slot))
        {
            return;
        }

        slot.Button.SetWeapon(weapon);
        ReplaceWeaponPreview(slot, weapon);
    }

    private static bool ContainsSlot(
        IReadOnlyList<CraftWeaponSlotDefinition> slotDefinitions,
        string slotId)
    {
        for (int i = 0; i < slotDefinitions.Count; i++)
        {
            if (slotDefinitions[i].SlotId == slotId)
                return true;
        }

        return false;
    }

    private void HandleWeaponSlotFocusChanged(string focusedSlotId)
    {
        for (int i = 0; i < previewWeaponSlots.Count; i++)
        {
            PreviewWeaponSlot slot = previewWeaponSlots[i];
            if (slot.Button != null)
                slot.Button.SetFocused(slot.SlotId == focusedSlotId);
        }
    }

    private void ReplaceWeaponPreview(PreviewWeaponSlot slot, WeaponContentDefinition weapon)
    {
        if (slot.WeaponPreview != null)
        {
            Destroy(slot.WeaponPreview);
            slot.WeaponPreview = null;
        }

        if (weapon == null || weapon.Prefab == null || slot.WeaponMount == null)
            return;

        GameObject weaponPreview = Instantiate(weapon.Prefab, slot.WeaponMount, false);
        Transform weaponTransform = weaponPreview.transform;
        weaponTransform.localPosition = Vector3.zero;
        weaponTransform.localRotation = Quaternion.identity;

        SetLayerRecursively(weaponTransform, previewLayer);
        DisableWeaponGameplay(weaponPreview);
        slot.WeaponPreview = weaponPreview;
    }

    private void UpdateWeaponSlotPositions()
    {
        if (previewCamera == null)
            return;

        for (int i = 0; i < previewWeaponSlots.Count; i++)
        {
            PreviewWeaponSlot slot = previewWeaponSlots[i];
            if (slot.Button == null || slot.ButtonAnchor == null)
                continue;

            Vector3 viewportPosition = previewCamera.WorldToViewportPoint(slot.ButtonAnchor.position);
            bool isVisible = slot.ButtonAnchor.gameObject.activeInHierarchy
                && viewportPosition.z > 0f
                && viewportPosition.x >= 0f
                && viewportPosition.x <= 1f
                && viewportPosition.y >= 0f
                && viewportPosition.y <= 1f;

            slot.Button.SetViewportPosition(
                new Vector2(viewportPosition.x, viewportPosition.y),
                slot.ButtonOffset,
                slot.ButtonSize,
                isVisible);
        }
    }

    private static void DisableWeaponGameplay(GameObject weaponPreview)
    {
        Weapon[] weapons = weaponPreview.GetComponentsInChildren<Weapon>(true);
        for (int i = 0; i < weapons.Length; i++)
            weapons[i].enabled = false;

        Collider2D[] colliders = weaponPreview.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody2D[] rigidbodies = weaponPreview.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            rigidbodies[i].simulated = false;
    }

    private void FramePreview(GameObject instance)
    {
        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            previewCamera.orthographicSize = 1f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        instance.transform.position -= bounds.center;

        float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x) + framingPadding;
        previewCamera.orthographicSize = Mathf.Max(0.01f, halfHeight);
    }

    private static void SetLayerRecursively(Transform current, int layer)
    {
        current.gameObject.layer = layer;
        for (int i = 0; i < current.childCount; i++)
            SetLayerRecursively(current.GetChild(i), layer);
    }
}
