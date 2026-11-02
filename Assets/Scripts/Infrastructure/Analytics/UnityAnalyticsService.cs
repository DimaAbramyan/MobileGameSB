using System;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using UnityEngine;
using UnityEngine.UnityConsent;
using Zenject;
using Event = Unity.Services.Analytics.Event;

namespace Game.Infrastructure.Analytics
{
    /// <summary>One adapter in ProjectContext. The SDK owns consent storage, event buffering and Android lifecycle.</summary>
    public sealed class UnityAnalyticsService : IAnalyticsService, IInitializable, IDisposable
    {
        private readonly IUnityAnalyticsBackend backend;
        private Task<bool> initialization;
        private bool disposed;

        public AnalyticsInitializationState State { get; private set; }
        public bool IsInitialized => State == AnalyticsInitializationState.Ready;
        public bool CanRecordEvents => IsInitialized && Consent == ConsentStatus.Granted;
        public string EnvironmentName { get; }
        public string LastError { get; private set; }
        public ConsentStatus Consent => backend.Consent;
        public event Action<AnalyticsInitializationState> StateChanged;
        public event Action<ConsentStatus> ConsentChanged;

        public UnityAnalyticsService(IUnityAnalyticsBackend backend, AnalyticsConfiguration configuration)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            EnvironmentName = configuration.ResolveEnvironment(Application.isEditor, Debug.isDebugBuild);
            backend.ConsentChanged += OnConsentChanged;
        }

        // Zenject startup never waits for networking and never observes a faulted startup task.
        public void Initialize() { _ = InitializeAsync(); }

        public Task<bool> InitializeAsync()
        {
            if (disposed)
                return Task.FromResult(false);
            if (initialization != null)
                return initialization;

            // Assign before notifications/SDK calls, including synchronous completion and reentrant subscribers.
            var completion = new TaskCompletionSource<bool>();
            initialization = completion.Task;
            _ = InitializeCoreAsync(completion);
            return initialization;
        }

        public Task<bool> RetryInitializationAsync()
        {
            if (!disposed && State == AnalyticsInitializationState.Failed && initialization.IsCompleted)
                initialization = null;
            return InitializeAsync();
        }

        public bool GrantConsent() => SetConsent(true);
        public bool RevokeConsent() => SetConsent(false);

        public bool TryRecordEvent(Event analyticsEvent)
        {
            if (disposed || analyticsEvent == null || !CanRecordEvents)
                return false;
            try
            {
                backend.RecordEvent(analyticsEvent);
                return true; // Accepted locally by SDK, not a guarantee of server delivery.
            }
            catch (Exception exception)
            {
                ReportError(exception);
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            backend.ConsentChanged -= OnConsentChanged;
            backend.Dispose();
            State = AnalyticsInitializationState.Disposed;
            StateChanged = null;
            ConsentChanged = null;
        }

        private async Task InitializeCoreAsync(TaskCompletionSource<bool> completion)
        {
            LastError = null;
            SetState(AnalyticsInitializationState.Initializing);
            try
            {
                await backend.InitializeAsync(EnvironmentName);
                if (disposed)
                {
                    completion.TrySetResult(false);
                    return;
                }
                SetState(AnalyticsInitializationState.Ready);
                completion.TrySetResult(!disposed);
            }
            catch (Exception exception)
            {
                if (!disposed)
                {
                    ReportError(exception);
                    SetState(AnalyticsInitializationState.Failed);
                }
                completion.TrySetResult(false);
            }
        }

        private bool SetConsent(bool granted)
        {
            if (disposed)
                return false;
            try
            {
                backend.SetConsent(granted);
                return true;
            }
            catch (Exception exception)
            {
                ReportError(exception);
                return false;
            }
        }

        private void SetState(AnalyticsInitializationState state)
        {
            State = state;
            // A faulty consumer must not prevent initialization/completion or another consumer's notification.
            if (StateChanged == null)
                return;
            foreach (Action<AnalyticsInitializationState> callback in StateChanged.GetInvocationList())
            {
                try { callback(state); }
                catch (Exception exception) { Debug.LogWarning("Analytics state subscriber: " + exception.Message); }
            }
        }

        private void OnConsentChanged(ConsentStatus status)
        {
            if (ConsentChanged == null)
                return;
            foreach (Action<ConsentStatus> callback in ConsentChanged.GetInvocationList())
            {
                try { callback(status); }
                catch (Exception exception) { Debug.LogWarning("Analytics consent subscriber: " + exception.Message); }
            }
        }

        private void ReportError(Exception exception)
        {
            LastError = exception.GetType().Name + ": " + exception.Message;
            Debug.LogWarning("Analytics unavailable: " + LastError);
        }
    }
}
