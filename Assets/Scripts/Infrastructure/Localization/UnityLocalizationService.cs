using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>A project-lifetime adapter; loading, formatting and fallback remain in Unity Localization.</summary>
public sealed class UnityLocalizationService : ILocalizationService, IDisposable
{
    private readonly LocalizationSettings settings;
    private Task initialization;
    private bool disposed;

    public bool IsInitialized => !disposed && initialization?.Status == TaskStatus.RanToCompletion;
    public Locale SelectedLocale => IsInitialized ? settings.GetSelectedLocale() : null;
    public event Action<Locale> LocaleChanged;

    public UnityLocalizationService()
    {
        if (!LocalizationSettings.HasSettings)
            throw new InvalidOperationException("Configure the project's Unity Localization Settings before using localization.");

        settings = LocalizationSettings.Instance;
        settings.OnSelectedLocaleChanged += OnLocaleChanged;
    }

    // All callers share initialization; consumers may request strings before it has completed.
    public Task InitializeAsync()
    {
        ThrowIfDisposed();
        return initialization ??= InitializeCoreAsync();
    }

    public async Task SetLocaleAsync(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
            throw new ArgumentException("A locale code is required.", nameof(localeCode));

        await InitializeAsync();
        ThrowIfDisposed();
        Locale locale = settings.GetAvailableLocales().GetLocale(new LocaleIdentifier(localeCode));
        if (locale == null)
            throw new ArgumentException($"Locale '{localeCode}' is not configured.", nameof(localeCode));

        settings.SetSelectedLocale(locale);
        await InitializeCoreAsync();
        ThrowIfDisposed();
    }

    public async Task<string> GetStringAsync(
        TableReference table, TableEntryReference key, params object[] arguments)
    {
        ValidateReference(table, key);
        await InitializeAsync();
        ThrowIfDisposed();

        var operation = settings.GetStringDatabase().GetLocalizedStringAsync(
            table, key, arguments, settings.GetSelectedLocale());
        // Keep our own reference while awaiting; the package releases its reference next frame.
        if (Application.isPlaying)
            Addressables.ResourceManager.Acquire(operation);
        try
        {
            string result = await operation.Task;
            ThrowIfDisposed();
            if (operation.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Cannot localize '{table}/{key}'.", operation.OperationException);
            return result;
        }
        finally
        {
            // Outside Play Mode the package transfers the original reference to the caller.
            if (operation.IsValid())
                Addressables.Release(operation);
        }
    }

    public LocalizedString CreateString(
        TableReference table, TableEntryReference key, params object[] arguments)
    {
        ThrowIfDisposed();
        ValidateReference(table, key);
        return new LocalizedString { TableReference = table, TableEntryReference = key, Arguments = arguments };
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        settings.OnSelectedLocaleChanged -= OnLocaleChanged;
        LocaleChanged = null;
        // Settings and cached tables are owned by the package, not this adapter.
    }

    private async Task InitializeCoreAsync()
    {
        var operation = settings.GetInitializationOperation();
        await operation.Task;
        if (operation.Status != AsyncOperationStatus.Succeeded || settings.GetSelectedLocale() == null)
            throw new InvalidOperationException("Unity Localization initialization failed.", operation.OperationException);
    }

    private void OnLocaleChanged(Locale locale) => LocaleChanged?.Invoke(locale);

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(UnityLocalizationService));
    }

    private static void ValidateReference(TableReference table, TableEntryReference key)
    {
        if (table.ReferenceType == TableReference.Type.Empty)
            throw new ArgumentException("A string table reference is required.", nameof(table));
        if (key.ReferenceType == TableEntryReference.Type.Empty)
            throw new ArgumentException("A string entry key or ID is required.", nameof(key));
    }
}
