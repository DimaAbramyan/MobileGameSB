using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Zenject;

[DefaultExecutionOrder(-100)]
public sealed class LevelPresentationController : MonoBehaviour
{
    private sealed class RuntimeBackgroundObject
    {
        public Transform Transform;
        public Vector2 Speed;
        public Vector2 PositionMin;
        public Vector2 PositionMax;
        public float RespawnPadding;
    }

    private sealed class RuntimeParallaxLayer
    {
        public Transform Transform;
        public Vector2 ConfiguredSize;
    }

    [Inject] private LevelCatalog levelCatalog;
    [Inject] private AudioVolumeService audioVolumeService;
    [SerializeField] private SpriteRenderer legacyBackground;

    private readonly List<Material> parallaxMaterials = new();
    private readonly List<Mesh> parallaxMeshes = new();
    private readonly List<Vector2> parallaxOffsets = new();
    private readonly List<Vector2> parallaxSpeeds = new();
    private readonly List<RuntimeParallaxLayer> parallaxLayers = new();
    private readonly List<Material> backgroundObjectMaterials = new();
    private readonly List<RuntimeBackgroundObject> backgroundObjects = new();
    private EventInstance musicInstance;
    private bool hasMusicInstance;
    private int viewportWidth;
    private int viewportHeight;

    private void Awake()
    {
        if (legacyBackground != null)
            legacyBackground.enabled = false;

        LevelConfig config = LevelLoader.GetSelectedLevel(levelCatalog);
        if (config == null)
            return;

        BuildParallax(config);
        BuildRandomBackgroundObjects(config);
        StartMusic(config);
    }

    private void BuildParallax(LevelConfig config)
    {
        var root = new GameObject("Parallax Background").transform;
        root.SetParent(transform, false);

        int minSortingOrder = int.MaxValue;
        int maxSortingOrder = int.MinValue;
        foreach (LevelConfig.ParallaxLayer layer in config.ParallaxLayers)
        {
            if (layer == null || layer.sprite == null)
                continue;

            minSortingOrder = Mathf.Min(minSortingOrder, layer.sortingOrder);
            maxSortingOrder = Mathf.Max(maxSortingOrder, layer.sortingOrder);
        }

        foreach (LevelConfig.ParallaxLayer layer in config.ParallaxLayers)
        {
            if (layer == null || layer.sprite == null)
                continue;

            var layerObject = new GameObject(
                string.IsNullOrWhiteSpace(layer.name)
                    ? "Parallax Layer"
                    : layer.name);
            layerObject.transform.SetParent(root, false);
            layerObject.transform.localPosition =
                new Vector3(layer.position.x, layer.position.y, 0f);

            Vector2 spriteSize = layer.sprite.bounds.size;
            Vector2 configuredSize = new Vector2(
                spriteSize.x * layer.scale.x,
                spriteSize.y * layer.scale.y);
            layerObject.transform.localScale = new Vector3(
                configuredSize.x,
                configuredSize.y,
                1f);
            parallaxLayers.Add(new RuntimeParallaxLayer
            {
                Transform = layerObject.transform,
                ConfiguredSize = configuredSize
            });

            Mesh mesh = CreateLayerMesh(layer.sprite, true);
            var meshFilter = layerObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            parallaxMeshes.Add(mesh);

            var meshRenderer = layerObject.AddComponent<MeshRenderer>();
            meshRenderer.sortingOrder = layer.sortingOrder;

            Material material = CreateSpriteMaterial(
                layer.sprite,
                layer.color,
                $"{layer.name} Parallax Material");
            meshRenderer.sharedMaterial = material;
            parallaxMaterials.Add(material);
            parallaxOffsets.Add(Vector2.zero);
            parallaxSpeeds.Add(GetLayerScrollSpeed(
                config,
                layer,
                minSortingOrder,
                maxSortingOrder));
        }
    }

    private static Vector2 GetMinimumViewportSize(
        Transform layerTransform,
        Camera gameplayCamera)
    {
        if (layerTransform == null || gameplayCamera == null)
            return Vector2.zero;

        Vector3 layerPosition = layerTransform.position;
        float cameraDistance = Mathf.Abs(
            layerPosition.z - gameplayCamera.transform.position.z);
        Vector3 bottomLeft = gameplayCamera.ViewportToWorldPoint(
            new Vector3(0f, 0f, cameraDistance));
        Vector3 topRight = gameplayCamera.ViewportToWorldPoint(
            new Vector3(1f, 1f, cameraDistance));

        return new Vector2(
            2f * Mathf.Max(
                Mathf.Abs(layerPosition.x - bottomLeft.x),
                Mathf.Abs(topRight.x - layerPosition.x)),
            2f * Mathf.Max(
                Mathf.Abs(layerPosition.y - bottomLeft.y),
                Mathf.Abs(topRight.y - layerPosition.y)));
    }

    private static float GetAtLeastViewportSize(
        float configuredSize,
        float minimumViewportSize)
    {
        float sign = configuredSize < 0f ? -1f : 1f;
        return sign * Mathf.Max(
            Mathf.Abs(configuredSize),
            minimumViewportSize);
    }

    private void Start()
    {
        ResizeParallaxLayersToViewport();
    }

    private void BuildRandomBackgroundObjects(LevelConfig config)
    {
        if (config.RandomBackgroundObjects.Count == 0)
            return;

        var root = new GameObject("Random Background Objects").transform;
        root.SetParent(transform, false);

        foreach (LevelConfig.RandomBackgroundObject entry
                 in config.RandomBackgroundObjects)
        {
            if (entry == null || entry.sprite == null || entry.count <= 0)
                continue;

            Vector2 positionMin = Vector2.Min(
                entry.positionMin,
                entry.positionMax);
            Vector2 positionMax = Vector2.Max(
                entry.positionMin,
                entry.positionMax);

            for (int i = 0; i < entry.count; i++)
            {
                var instance = new GameObject(
                    string.IsNullOrWhiteSpace(entry.name)
                        ? entry.sprite.name
                        : $"{entry.name} {i + 1}");
                instance.transform.SetParent(root, false);
                instance.name = string.IsNullOrWhiteSpace(entry.name)
                    ? entry.sprite.name
                    : $"{entry.name} {i + 1}";

                instance.transform.localPosition = new Vector3(
                    Random.Range(positionMin.x, positionMax.x),
                    Random.Range(positionMin.y, positionMax.y),
                    0f);

                float scale = Random.Range(
                    Mathf.Min(entry.scaleMin, entry.scaleMax),
                    Mathf.Max(entry.scaleMin, entry.scaleMax));

                Vector2 spriteSize = entry.sprite.bounds.size;
                instance.transform.localScale = new Vector3(
                    spriteSize.x * scale,
                    spriteSize.y * scale,
                    1f);

                Mesh mesh = CreateLayerMesh(entry.sprite, false);
                var meshFilter = instance.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                parallaxMeshes.Add(mesh);

                var meshRenderer = instance.AddComponent<MeshRenderer>();
                meshRenderer.sortingOrder = entry.sortingOrder;

                Material material = CreateSpriteMaterial(
                    entry.sprite,
                    entry.color,
                    $"{entry.name} Background Object Material");
                meshRenderer.sharedMaterial = material;
                backgroundObjectMaterials.Add(material);

                float ySpeedMin = Mathf.Min(entry.ySpeedMin, entry.ySpeedMax);
                float ySpeedMax = Mathf.Max(entry.ySpeedMin, entry.ySpeedMax);

                backgroundObjects.Add(new RuntimeBackgroundObject
                {
                    Transform = instance.transform,
                    Speed = new Vector2(
                        0f,
                        Random.Range(ySpeedMin, ySpeedMax)),
                    PositionMin = positionMin,
                    PositionMax = positionMax,
                    RespawnPadding = entry.respawnPadding
                });
            }
        }
    }

    private static Mesh CreateLayerMesh(Sprite sprite, bool useFullTexture)
    {
        Vector2 uvMin = Vector2.zero;
        Vector2 uvMax = Vector2.one;

        if (!useFullTexture)
        {
            Rect textureRect = sprite.textureRect;
            Texture texture = sprite.texture;

            uvMin = new Vector2(
                textureRect.xMin / texture.width,
                textureRect.yMin / texture.height);
            uvMax = new Vector2(
                textureRect.xMax / texture.width,
                textureRect.yMax / texture.height);
        }

        var mesh = new Mesh
        {
            name = $"{sprite.name} Parallax Mesh",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(uvMin.x, uvMin.y),
                new Vector2(uvMin.x, uvMax.y),
                new Vector2(uvMax.x, uvMax.y),
                new Vector2(uvMax.x, uvMin.y)
            },
            triangles = new[]
            {
                0, 1, 2,
                0, 2, 3
            }
        };
        mesh.RecalculateBounds();

        return mesh;
    }

    private static Material CreateSpriteMaterial(
        Sprite sprite,
        Color color,
        string materialName)
    {
        Shader shader = Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
            ?? Shader.Find("Sprites/Default");

        var material = new Material(shader)
        {
            name = materialName,
            mainTexture = sprite.texture,
            color = color
        };

        material.mainTexture.wrapMode = TextureWrapMode.Repeat;
        material.SetTexture("_MainTex", sprite.texture);
        material.SetTextureOffset("_MainTex", Vector2.zero);
        material.SetTextureScale("_MainTex", Vector2.one);

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", sprite.texture);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetTextureScale("_BaseMap", Vector2.one);
        }

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        return material;
    }

    private static Vector2 GetLayerScrollSpeed(
        LevelConfig config,
        LevelConfig.ParallaxLayer layer,
        int minSortingOrder,
        int maxSortingOrder)
    {
        if (!config.AutoScaleParallaxSpeedByDepth
            || minSortingOrder == int.MaxValue
            || minSortingOrder == maxSortingOrder)
        {
            return layer.scrollSpeed;
        }

        float depth =
            Mathf.InverseLerp(minSortingOrder, maxSortingOrder, layer.sortingOrder);
        float multiplier = Mathf.Lerp(
            config.FarthestLayerSpeedMultiplier,
            1f,
            depth);

        return layer.scrollSpeed * multiplier;
    }

    private void StartMusic(LevelConfig config)
    {
        musicInstance = audioVolumeService.PlayMusic(config.Music);
        hasMusicInstance = musicInstance.isValid();
    }

    private void Update()
    {
        if (viewportWidth != Screen.width || viewportHeight != Screen.height)
            ResizeParallaxLayersToViewport();

        UpdateRandomBackgroundObjects();
        UpdateParallaxLayers();
    }

    private void ResizeParallaxLayersToViewport()
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
            return;

        viewportWidth = Screen.width;
        viewportHeight = Screen.height;

        for (int i = parallaxLayers.Count - 1; i >= 0; i--)
        {
            RuntimeParallaxLayer layer = parallaxLayers[i];
            if (layer.Transform == null)
            {
                parallaxLayers.RemoveAt(i);
                continue;
            }

            Vector2 minimumViewportSize = GetMinimumViewportSize(
                layer.Transform,
                gameplayCamera);
            layer.Transform.localScale = new Vector3(
                GetAtLeastViewportSize(
                    layer.ConfiguredSize.x,
                    minimumViewportSize.x),
                GetAtLeastViewportSize(
                    layer.ConfiguredSize.y,
                    minimumViewportSize.y),
                1f);
        }
    }

    private void UpdateParallaxLayers()
    {
        for (int i = 0; i < parallaxMaterials.Count; i++)
        {
            Material material = parallaxMaterials[i];
            if (material == null)
                continue;

            parallaxOffsets[i] += parallaxSpeeds[i] * Time.deltaTime;
            parallaxOffsets[i] = new Vector2(
                Mathf.Repeat(parallaxOffsets[i].x, 1f),
                Mathf.Repeat(parallaxOffsets[i].y, 1f));

            ApplyTextureOffset(material, parallaxOffsets[i]);

            if (i < parallaxMeshes.Count && parallaxMeshes[i] != null)
                ApplyMeshUvOffset(parallaxMeshes[i], parallaxOffsets[i]);
        }
    }

    private void UpdateRandomBackgroundObjects()
    {
        for (int i = backgroundObjects.Count - 1; i >= 0; i--)
        {
            RuntimeBackgroundObject backgroundObject = backgroundObjects[i];
            if (backgroundObject.Transform == null)
            {
                backgroundObjects.RemoveAt(i);
                continue;
            }

            Vector3 position = backgroundObject.Transform.localPosition;
            position +=
                (Vector3)(backgroundObject.Speed * Time.deltaTime);

            position = WrapBackgroundObjectPosition(
                position,
                backgroundObject);

            backgroundObject.Transform.localPosition = position;
        }
    }

    private static Vector3 WrapBackgroundObjectPosition(
        Vector3 position,
        RuntimeBackgroundObject backgroundObject)
    {
        Vector2 min = backgroundObject.PositionMin;
        Vector2 max = backgroundObject.PositionMax;
        float padding = backgroundObject.RespawnPadding;

        if (position.x < min.x - padding)
        {
            position.x = max.x + padding;
            position.y = Random.Range(min.y, max.y);
        }
        else if (position.x > max.x + padding)
        {
            position.x = min.x - padding;
            position.y = Random.Range(min.y, max.y);
        }

        if (position.y < min.y - padding)
        {
            position.y = max.y + padding;
            position.x = Random.Range(min.x, max.x);
        }
        else if (position.y > max.y + padding)
        {
            position.y = min.y - padding;
            position.x = Random.Range(min.x, max.x);
        }

        return position;
    }

    private static void ApplyTextureOffset(Material material, Vector2 offset)
    {
        material.mainTextureOffset = offset;

        if (material.HasProperty("_MainTex"))
            material.SetTextureOffset("_MainTex", offset);

        if (material.HasProperty("_BaseMap"))
            material.SetTextureOffset("_BaseMap", offset);
    }

    private static void ApplyMeshUvOffset(Mesh mesh, Vector2 offset)
    {
        mesh.uv = new[]
        {
            new Vector2(offset.x, offset.y),
            new Vector2(offset.x, offset.y + 1f),
            new Vector2(offset.x + 1f, offset.y + 1f),
            new Vector2(offset.x + 1f, offset.y)
        };
    }

    private void OnDestroy()
    {
        if (hasMusicInstance)
            audioVolumeService.StopAndRelease(musicInstance);

        foreach (Material material in parallaxMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        foreach (Material material in backgroundObjectMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        foreach (Mesh mesh in parallaxMeshes)
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
