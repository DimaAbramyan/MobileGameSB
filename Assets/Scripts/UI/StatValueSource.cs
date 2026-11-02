using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// A provider declares only its own readable values. No stat-name switch or reflection
// is needed to discover a new contract: implement this interface on the contract.
public interface IStatValueProvider
{
    void CollectDisplayStats(StatSourceBuilder source);
}

public enum StatValueKind { Number, Text, Enumeration, Boolean }
public enum StatValueState { Preview, Runtime, BaseConfiguration, Missing }

public interface IStatValueResolver
{
    bool IsRuntime { get; }
    bool TryRead(StatValueDefinition definition, out StatValueResult result);
}

public readonly struct StatValueResult
{
    public readonly object Value;
    public readonly StatValueState State;
    public StatValueResult(object value, StatValueState state) { Value = value; State = state; }
}

public sealed class StatValueDefinition
{
    public string SourcePath, SourceLabel, Id, Name, LocalizationKey, Format, Property;
    public UnityEngine.Object SourceAsset;
    public Type ProviderType;
    public StatValueKind Kind;
    public ProjectileData Projectile;
    public string RootSlot;
    public int ProjectileDepth;
    internal Func<object> ReadBase;
    public StatValueResult Read(IStatValueResolver context)
    {
        if (!ReferenceEquals(SourceAsset, null) && SourceAsset == null)
            return new StatValueResult(null, StatValueState.Missing);
        if (context != null && context.TryRead(this, out var result)) return result;
        return new StatValueResult(ReadBase(), context != null && context.IsRuntime
            ? StatValueState.BaseConfiguration : StatValueState.Preview);
    }
    public string FormatValue(object value, string formatOverride, IFormatProvider provider = null)
    {
        provider ??= StatValueFormatter.Culture;
        string format = string.IsNullOrWhiteSpace(formatOverride) ? Format : formatOverride;
        if (string.IsNullOrEmpty(format)) return Convert.ToString(value, provider) ?? "";
        if (format.Contains("{")) return string.Format(provider, format, value);
        return value is IFormattable f ? f.ToString(format, provider) : Convert.ToString(value, provider);
    }
}

public sealed class StatValueCatalog
{
    public readonly HashSet<UnityEngine.Object> Dependencies = new();
    public readonly List<StatValueDefinition> Values = new();
    public readonly List<string> Warnings = new();
    private readonly Dictionary<string, StatValueDefinition> index = new();
    private readonly HashSet<string> ambiguous = new();
    internal void Add(StatValueDefinition value)
    {
        if (value.SourceAsset != null) Dependencies.Add(value.SourceAsset);
        string key = value.SourcePath + "|" + value.Id;
        if (ambiguous.Contains(key)) return;
        if (index.TryGetValue(key, out var old))
        {
            index.Remove(key); Values.Remove(old); ambiguous.Add(key);
            Warnings.Add("Ambiguous source: " + value.SourceLabel + "/" + value.Name); return;
        }
        index.Add(key, value); Values.Add(value);
    }
    public bool TryFind(StatDisplayCell cell, out StatValueDefinition value)
    {
        if (index.TryGetValue(cell.sourcePath + "|" + cell.statId, out value)
            && cell.sourceAsset == value.SourceAsset) return true;
        value = null; return false;
    }
    public bool TryFind(string path, string id, out StatValueDefinition value) => index.TryGetValue(path + "|" + id, out value);
}

public sealed class StatSourceBuilder
{
    public static string ShortId(string id) => string.IsNullOrEmpty(id) ? "missing ID" : id.Substring(0, Math.Min(6, id.Length));
    public void Warn(string message) => catalog.Warnings.Add(message);
    private readonly StatValueCatalog catalog;
    private readonly IStatValueProvider provider;
    private readonly string path, label;
    private readonly UnityEngine.Object asset;
    private readonly ProjectileData projectile;
    private readonly string slot;
    private readonly int depth;
    public StatSourceBuilder(StatValueCatalog catalog, IStatValueProvider provider, string path, string label,
        UnityEngine.Object asset, ProjectileData projectile = null, string slot = null, int depth = 0)
    {
        this.catalog = catalog; this.provider = provider; this.path = path; this.label = label;
        this.asset = asset; this.projectile = projectile; this.slot = slot; this.depth = depth;
    }
    public void Add<T>(string property, string name, Func<T> getter, string format = null, string localizationKey = null, string stableId = null)
    {
        Type type = typeof(T);
        var kind = type.IsEnum ? StatValueKind.Enumeration : type == typeof(bool) ? StatValueKind.Boolean
            : type == typeof(string) ? StatValueKind.Text : StatValueKind.Number;
        catalog.Add(new StatValueDefinition { SourcePath = path, SourceLabel = label, SourceAsset = asset,
            ProviderType = provider.GetType(), Id = stableId ?? provider.GetType().FullName + "." + property, Property = property,
            Name = name, LocalizationKey = localizationKey ?? "stats." + provider.GetType().Name + "." + property,
            Kind = kind, Format = format ?? (kind == StatValueKind.Number ? "0.##" : ""), ReadBase = () => getter(),
            Projectile = projectile, RootSlot = slot, ProjectileDepth = depth });
    }
    public void Nested(IStatValueProvider child, string id, string name, UnityEngine.Object sourceAsset = null)
    {
        if (child == null) return;
        child.CollectDisplayStats(new StatSourceBuilder(catalog, child, path + "/" + id,
            label + "/" + name, sourceAsset != null ? sourceAsset : asset, projectile, slot, depth));
    }
}
