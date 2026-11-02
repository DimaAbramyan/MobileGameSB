using System;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using UnityEngine.UnityConsent;

namespace Game.Infrastructure.Analytics
{
    public enum AnalyticsInitializationState { NotInitialized, Initializing, Ready, Failed, Disposed }

    /// <summary>Project-lifetime API. Call on Unity's main thread. Events use SDK Event models.</summary>
    public interface IAnalyticsService
    {
        AnalyticsInitializationState State { get; }
        bool IsInitialized { get; }
        bool CanRecordEvents { get; }
        string EnvironmentName { get; }
        string LastError { get; }
        ConsentStatus Consent { get; }
        event Action<AnalyticsInitializationState> StateChanged;
        event Action<ConsentStatus> ConsentChanged;
        Task<bool> InitializeAsync();
        Task<bool> RetryInitializationAsync();
        bool GrantConsent();
        bool RevokeConsent();
        bool TryRecordEvent(Event analyticsEvent);
    }
}
