using System;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.UnityConsent;
using SdkAnalytics = Unity.Services.Analytics.AnalyticsService;
using Event = Unity.Services.Analytics.Event;

namespace Game.Infrastructure.Analytics
{
    /// <summary>Thin SDK boundary, also allowing offline/consent diagnostics without real data collection.</summary>
    public interface IUnityAnalyticsBackend : IDisposable
    {
        ConsentStatus Consent { get; }
        event Action<ConsentStatus> ConsentChanged;
        Task InitializeAsync(string environmentName);
        void SetConsent(bool granted);
        void RecordEvent(Event analyticsEvent);
    }

    public sealed class UnityAnalyticsBackend : IUnityAnalyticsBackend
    {
        private Task initialization;
        private string environment;
        private bool disposed;

        public ConsentStatus Consent => EndUserConsent.GetConsentState().AnalyticsIntent;
        public event Action<ConsentStatus> ConsentChanged;

        public UnityAnalyticsBackend() => EndUserConsent.consentStateChanged += OnConsentChanged;

        public async Task InitializeAsync(string environmentName)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(UnityAnalyticsBackend));
            if (!Guid.TryParse(Application.cloudProjectId, out var projectId) || projectId == Guid.Empty)
                throw new InvalidOperationException("Unity Cloud Project ID is missing. Link this project in Project Settings > Services.");

            // Never silently accept an SDK initialized elsewhere into an unknown (possibly production) environment.
            if (UnityServices.State != ServicesInitializationState.Uninitialized)
            {
                if (environment != environmentName || initialization == null)
                    throw new InvalidOperationException("UGS was initialized outside the Analytics adapter. Its environment cannot be verified. Use this initialization path for UGS.");
                await initialization;
            }
            else
            {
                environment = environmentName;
                initialization = UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(environmentName));
                await initialization;
            }
            // Resolve the official Analytics service; no StartDataCollection/Flush or synthetic event is used.
            _ = SdkAnalytics.Instance;
        }

        public void SetConsent(bool granted)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(UnityAnalyticsBackend));
            if (granted && UnityServices.State != ServicesInitializationState.Uninitialized &&
                (initialization?.Status != TaskStatus.RanToCompletion || environment == null))
                throw new InvalidOperationException("Cannot grant consent while the UGS initialization environment is unverified.");
            var state = EndUserConsent.GetConsentState();
            state.AnalyticsIntent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
            EndUserConsent.SetConsentState(state); // Preserve AdsIntent and Unity's own persistence.
        }

        public void RecordEvent(Event analyticsEvent) => SdkAnalytics.Instance.RecordEvent(analyticsEvent);

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            EndUserConsent.consentStateChanged -= OnConsentChanged;
            ConsentChanged = null;
            // SDK owns its container, pause/quit caching, upload cadence and process lifetime.
        }

        private void OnConsentChanged(ConsentState state) => ConsentChanged?.Invoke(state.AnalyticsIntent);
    }
}
