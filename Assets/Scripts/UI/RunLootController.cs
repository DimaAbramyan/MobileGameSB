using System.Collections.Generic;
using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class RunLootController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform lootGained;
    [SerializeField] private LootView lootPrefab;

    private PlayerResourceWallet resourceWallet;
    private readonly Dictionary<ResourceKind, PlayerResource> runResources = new();
    private readonly Dictionary<ResourceKind, LootView> lootViews = new();

    [Inject]
    private void Construct(PlayerResourceWallet wallet)
    {
        if (resourceWallet == wallet)
            return;

        UnsubscribeWallet();
        resourceWallet = wallet;
        SubscribeWallet();
    }

    private void Awake()
    {
        if (lootGained == null)
            lootGained = transform;
    }

    private void OnEnable()
    {
        SubscribeWallet();
    }

    private void OnDisable()
    {
        UnsubscribeWallet();
    }

    private void SubscribeWallet()
    {
        if (!isActiveAndEnabled || resourceWallet == null)
            return;

        resourceWallet.OnResourceAdded -= HandleResourceAdded;
        resourceWallet.OnResourceAdded += HandleResourceAdded;
    }

    private void UnsubscribeWallet()
    {
        if (resourceWallet != null)
            resourceWallet.OnResourceAdded -= HandleResourceAdded;
    }

    private void HandleResourceAdded(PlayerResource resource, int amount)
    {
        if (resource == null || amount <= 0)
            return;

        if (!runResources.TryGetValue(resource.Kind, out PlayerResource runResource))
        {
            runResource = new PlayerResource(resource.Definition);
            runResources.Add(resource.Kind, runResource);
        }

        runResource.Add(amount);

        if (!lootViews.TryGetValue(resource.Kind, out LootView lootView)
            || lootView == null)
        {
            if (lootPrefab == null || lootGained == null)
            {
                Debug.LogError(
                    $"{nameof(RunLootController)} requires a loot prefab and LootGained container.",
                    this);
                return;
            }

            lootView = Instantiate(lootPrefab, lootGained);
            lootViews[resource.Kind] = lootView;
        }

        lootView.Set(runResource);
    }
}
