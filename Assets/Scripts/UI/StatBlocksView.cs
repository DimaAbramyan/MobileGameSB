using System;
using System.Collections.Generic;
using UnityEngine;

// Assign either existing Meta category. Sections and rows are independent.
public sealed class StatBlocksView : MonoBehaviour
{
    [SerializeField] private ScriptableObject meta;
    [SerializeField] private StatBox cellPrefab;
    [SerializeField] private RectTransform rowPrefab;
    [SerializeField] private RectTransform baseStatsRoot;
    [SerializeField] private RectTransform secondaryStatsRoot;
    [SerializeField, Min(0)] private int previewMetaLevel;
    [Header("Optional live source (assign only one)")]
    [SerializeField] private Weapon runtimeWeapon;
    [SerializeField] private WeaponController runtimeWeaponController;
    [SerializeField] private ParentShip runtimeShip;
    [SerializeField, HideInInspector] private List<GameObject> generatedRows = new();
    public ScriptableObject Meta => meta;
    public StatValueCatalog Catalog => catalog;
    public IStatValueResolver ValueResolver => resolver;
    public event Action ValuesRefreshed;
    public IReadOnlyList<string> Warnings => warnings;
    private readonly List<string> warnings = new();
    private readonly List<BoundCell> bindings = new();
    private StatValueCatalog catalog;
    private IStatValueResolver resolver;
    private float nextRefresh;
    private Func<string, string> localize;
    private Weapon subscribedWeapon;
    private ParentShip subscribedShip;
    private sealed class BoundCell
    {
        public StatBox Box;
        public StatDisplayCell Cell;
        public StatValueDefinition Definition;
        public bool Warned;
    }

    private void OnEnable()
    {
        if (Application.isPlaying && UnityEngine.Localization.Settings.LocalizationSettings.HasSettings)
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        if (!Application.isPlaying) return;
        if (runtimeWeapon != null) Bind(runtimeWeapon, runtimeWeaponController);
        else if (runtimeShip != null) Bind(runtimeShip);
        else Rebuild();
    }
    private void OnDisable()
    {
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        Unsubscribe();
    }
    private void OnLocaleChanged(UnityEngine.Localization.Locale _) => RefreshValues();
    private void Update()
    {
        if (resolver == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .2f;
        RefreshValues(); // Only selected getters. No configuration traversal here.
    }
    public void SetLabelLocalizer(Func<string, string> labelLocalizer)
    { localize = labelLocalizer; RefreshValues(); }

    public void Bind(Weapon source, WeaponController controller = null)
    {
        if (source == null || source.weaponData == null) throw new ArgumentException("Weapon requires WeaponData.");
        if (subscribedWeapon == source && runtimeWeaponController == controller && meta == source.weaponData.WeaponMetaConfig)
        { RefreshValues(); return; }
        Unsubscribe(); runtimeWeapon = source; runtimeShip = null; runtimeWeaponController = controller;
        meta = source.weaponData.WeaponMetaConfig; previewMetaLevel = 0;
        resolver = new StatRuntimeContext(source, controller);
        subscribedWeapon = source; source.OnLevelChanged += LevelChanged;
        Rebuild();
    }
    public void Bind(ParentShip source)
    {
        if (source == null || source.ShipData == null) throw new ArgumentException("Ship requires ShipData.");
        if (subscribedShip == source && meta == source.ShipData.ShipMetaConfig)
        { RefreshValues(); return; }
        Unsubscribe(); runtimeShip = source; runtimeWeapon = null;
        meta = source.ShipData.ShipMetaConfig; previewMetaLevel = 0;
        resolver = new StatRuntimeContext(source);
        subscribedShip = source;
        source.OnLevelChanged += LevelChanged;
        source.OnMaxHealthChanged += StatChanged; source.OnMaxShieldChanged += StatChanged;
        Rebuild();
    }
    public void Show(ScriptableObject source, IStatValueResolver valueResolver)
    { Show(source); resolver = valueResolver; RefreshValues(); }
    public void Preview(ScriptableObject source, int metaLevel)
    {
        previewMetaLevel = Mathf.Max(0, metaLevel);
        if (meta != source || resolver != null || catalog == null) { Show(source); return; }
        // Upgrades change getters/values, not the configured row/cell structure.
        catalog = StatCatalogBuilder.Build(meta, previewMetaLevel);
        warnings.Clear(); warnings.AddRange(catalog.Warnings);
        for (int i = 0; i < bindings.Count; i++)
        {
            catalog.TryFind(bindings[i].Cell, out bindings[i].Definition);
            bindings[i].Warned = false;
        }
        RefreshValues();
    }
    private void LevelChanged(int _) => RefreshValues();
    private void StatChanged(float _) => RefreshValues();
    private void Unsubscribe()
    {
        if (subscribedWeapon != null) subscribedWeapon.OnLevelChanged -= LevelChanged;
        if (subscribedShip != null)
        {
            subscribedShip.OnLevelChanged -= LevelChanged;
            subscribedShip.OnMaxHealthChanged -= StatChanged;
            subscribedShip.OnMaxShieldChanged -= StatChanged;
        }
        subscribedWeapon = null; subscribedShip = null;
    }

    public void Show(ScriptableObject source)
    {
        if (source != null && !(source is IStatDisplaySource))
            throw new ArgumentException("Meta must implement IStatDisplaySource.", nameof(source));
        Unsubscribe(); resolver = null; runtimeWeapon = null; runtimeShip = null;
        meta = source;
        Rebuild();
    }

    public void Rebuild() => Rebuild(null);
    // Editor supplies Undo-aware destruction; runtime needs no UnityEditor APIs.
    public void Rebuild(Action<GameObject> release)
    {
        bindings.Clear(); warnings.Clear();
        catalog = StatCatalogBuilder.Build(meta, previewMetaLevel);
        warnings.AddRange(catalog.Warnings);
        for (int i = 0; i < generatedRows.Count; i++)
        {
            var row = generatedRows[i];
            if (row == null) continue;
            row.SetActive(false);
            if (release != null) release(row);
            else if (Application.isPlaying) Destroy(row);
            else DestroyImmediate(row);
        }
        generatedRows.Clear();
        if (cellPrefab == null || rowPrefab == null || baseStatsRoot == null || secondaryStatsRoot == null)
        {
            Debug.LogError("StatBlocksView requires cell/row prefabs and both section containers.", this);
            return;
        }
        if (meta != null && !(meta is IStatDisplaySource))
        {
            Debug.LogError("Assign WeaponMetaConfig or ShipMetaConfig to StatBlocksView.", this);
            return;
        }
        var settings = (meta as IStatDisplaySource)?.StatDisplay;
        BuildSection(settings?.baseStats, baseStatsRoot);
        BuildSection(settings?.secondaryStats, secondaryStatsRoot);
        RefreshValues();
    }

    private void BuildSection(StatDisplayLayout layout, RectTransform container)
    {
        bool any = false;
        if (layout?.rows != null)
            for (int i = 0; i < layout.rows.Count; i++)
            {
                var cells = layout.rows[i]?.cells;
                if (cells == null || cells.Count == 0) continue;
                RectTransform row = null;
                for (int j = 0; j < cells.Count; j++)
                {
                    var cell = cells[j];
                    if (cell == null) continue;
                    if (row == null)
                    {
                        row = Instantiate(rowPrefab, container, false);
                        row.name = "Stat Row " + (i + 1);
                        generatedRows.Add(row.gameObject);
                        any = true;
                    }
                    var box = Instantiate(cellPrefab, row, false);
                    catalog.TryFind(cell, out var definition);
                    bindings.Add(new BoundCell { Box = box, Cell = cell, Definition = definition });
                }
            }
        container.gameObject.SetActive(any);
    }

    public void RefreshValues()
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.Box == null) continue;
            var cell = binding.Cell;
            var definition = binding.Definition;
            string label = cell.label;
            string key = cell.labelLocalizationKey;
            string value = cell.value;
            if (cell.useBinding)
            {
                if (definition == null)
                {
                    value = "— [missing]";
                    Warn(binding, "Missing stat binding: " + cell.sourcePath + "/" + cell.statId);
                }
                else
                {
                    if (!cell.overrideLabel) { label = definition.Name; key = definition.LocalizationKey; }
                    value = StatValueFormatter.Format(definition, resolver, cell.valueFormat, out var warning);
                    if (warning != null) Warn(binding, warning);
                }
            }
            if (localize != null && !string.IsNullOrWhiteSpace(key))
            {
                string translated = localize(key);
                if (!string.IsNullOrEmpty(translated)) label = translated;
            }
            binding.Box.SetContent(label, value);
        }
        ValuesRefreshed?.Invoke();
    }
    private void Warn(BoundCell binding, string message)
    {
        if (binding.Warned) return;
        binding.Warned = true; warnings.Add(message); Debug.LogWarning(message, this);
    }
}
