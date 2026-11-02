using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Game.Infrastructure.Analytics;
using Unity.Services.Analytics;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;
using UnityEngine.UnityConsent;
using Zenject;
using Event = Unity.Services.Analytics.Event;
using ProjectAnalytics = Game.Infrastructure.Analytics.IAnalyticsService;

public static class VerifyAnalyticsFoundation
{
    private sealed class FakeBackend : IUnityAnalyticsBackend
    {
        private Action<ConsentStatus> changed;
        public ConsentStatus Consent { get; private set; }
        public int Subscribers, Initializations, Records, Disposals, Writes;
        public string Environment;
        public bool FailInitialization, FailRecord, FailConsent;
        public TaskCompletionSource<bool> Pending;
        public event Action<ConsentStatus> ConsentChanged
        {
            add { changed += value; Subscribers++; }
            remove { changed -= value; Subscribers--; }
        }
        public Task InitializeAsync(string environment)
        {
            Initializations++;
            Environment = environment;
            if (FailInitialization)
                throw new IOException("Simulated offline initialization failure");
            return Pending == null ? Task.CompletedTask : Pending.Task;
        }
        public void SetConsent(bool granted)
        {
            if (FailConsent) throw new InvalidOperationException("Simulated consent failure");
            Writes++;
            Consent = granted ? ConsentStatus.Granted : ConsentStatus.Denied;
            changed?.Invoke(Consent);
        }
        public void RecordEvent(Event e)
        {
            if (FailRecord) throw new IOException("Simulated SDK recording failure");
            Records++;
        }
        public void Dispose() { Disposals++; }
    }

    public static async Task<object> Run()
    {
        var checks = new List<string>();
        var actualConsent = EndUserConsent.GetConsentState();
        var configuration = new AnalyticsConfiguration();
        Check(configuration.ResolveEnvironment(false, false) == "development", "Release defaults to development", checks);
        configuration.releaseEnvironment = AnalyticsReleaseEnvironment.Production;
        Check(configuration.ResolveEnvironment(true, false) == "development", "Editor cannot select production", checks);
        Check(configuration.ResolveEnvironment(false, true) == "development", "Development player cannot select production", checks);
        Check(configuration.ResolveEnvironment(false, false) == "production", "Explicit release production supported", checks);

        var backend = new FakeBackend { Pending = new TaskCompletionSource<bool>() };
        var service = new UnityAnalyticsService(backend, configuration);
        var sample = new AcquisitionSourceEvent(); // Existing SDK model, passed only to FakeBackend.
        Check(service.State == AnalyticsInitializationState.NotInitialized && !service.CanRecordEvents, "Initially inactive", checks);
        Check(!service.TryRecordEvent(sample) && backend.Records == 0, "Pre-init event rejected without buffering", checks);
        Task<bool> reentrant = null;
        service.StateChanged += state => { if (state == AnalyticsInitializationState.Initializing) reentrant = service.InitializeAsync(); };
        var first = service.InitializeAsync();
        Check(!first.IsCompleted && service.State == AnalyticsInitializationState.Initializing, "Async initialization does not block", checks);
        Check(ReferenceEquals(first, service.InitializeAsync()) && ReferenceEquals(first, reentrant), "Concurrent and reentrant calls share task", checks);
        Check(ReferenceEquals(first, service.RetryInitializationAsync()) && backend.Initializations == 1, "Retry while pending does not duplicate initialization", checks);
        backend.Pending.SetResult(true);
        Check(await first && service.IsInitialized, "Initialization becomes ready", checks);
        Check(backend.Environment == "development", "Actual Editor adapter selects development", checks);
        Check(await service.InitializeAsync() && backend.Initializations == 1, "Repeated successful initialization cached", checks);
        Check(!service.CanRecordEvents && backend.Writes == 0, "Initialization never grants consent", checks);
        Check(!service.TryRecordEvent(sample) && backend.Records == 0, "No-consent record rejected", checks);
        int notices = 0;
        service.ConsentChanged += _ => notices++;
        Check(service.GrantConsent() && service.CanRecordEvents && notices == 1, "Explicit grant works on fake only", checks);
        Check(service.TryRecordEvent(sample) && backend.Records == 1, "Granted SDK API forwarded to fake", checks);
        Check(service.RevokeConsent() && !service.CanRecordEvents && notices == 2, "Revoke immediately gates events", checks);
        Check(!service.TryRecordEvent(sample) && backend.Records == 1, "No queued replay after revoke", checks);
        Check(service.GrantConsent() && notices == 3, "Consent decision may change again", checks);
        backend.FailRecord = true;
        Check(!service.TryRecordEvent(sample) && service.LastError.Contains("Simulated"), "SDK record errors do not escape", checks);
        backend.FailConsent = true;
        Check(!service.RevokeConsent(), "Consent errors do not escape", checks);
        Check(!service.TryRecordEvent(null), "Null event rejected", checks);
        service.Dispose();
        service.Dispose();
        Check(backend.Disposals == 1 && backend.Subscribers == 0 && service.State == AnalyticsInitializationState.Disposed, "Dispose idempotent and unsubscribes", checks);
        Check(!await service.InitializeAsync() && !await service.RetryInitializationAsync() && !service.GrantConsent() && !service.CanRecordEvents,
            "Disposed service cannot restart", checks);

        backend = new FakeBackend { FailInitialization = true };
        service = new UnityAnalyticsService(backend, configuration);
        ((IInitializable)service).Initialize();
        Check(!await service.InitializeAsync() && service.State == AnalyticsInitializationState.Failed && service.LastError.Contains("offline"), "Offline startup nonfatal", checks);
        Check(!await service.InitializeAsync() && backend.Initializations == 1, "Failure cached without retry storm", checks);
        backend.FailInitialization = false;
        Check(await service.RetryInitializationAsync() && backend.Initializations == 2 && service.LastError == null, "Explicit retry recovers", checks);
        service.Dispose();

        backend = new FakeBackend { Pending = new TaskCompletionSource<bool>() };
        service = new UnityAnalyticsService(backend, configuration);
        var pending = service.InitializeAsync();
        service.Dispose();
        backend.Pending.SetResult(true);
        Check(!await pending && service.State == AnalyticsInitializationState.Disposed, "Completion after teardown cannot resurrect service", checks);

        backend = new FakeBackend();
        service = new UnityAnalyticsService(backend, configuration);
        service.StateChanged += state => { if (state == AnalyticsInitializationState.Ready) service.Dispose(); };
        Check(!await service.InitializeAsync() && service.State == AnalyticsInitializationState.Disposed,
            "Disposal inside readiness notification cannot report live readiness", checks);

        backend = new FakeBackend();
        service = new UnityAnalyticsService(backend, configuration);
        service.StateChanged += _ => throw new InvalidOperationException("Isolated bad state subscriber");
        service.ConsentChanged += _ => throw new InvalidOperationException("Isolated bad consent subscriber");
        Check(await service.InitializeAsync(), "Consumer exception cannot break readiness", checks);
        Check(service.RevokeConsent(), "Consumer exception cannot interrupt consent dispatch", checks);
        service.Dispose();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ProjectContext.prefab");
        Check(prefab != null && prefab.GetComponents<Zenject.ProjectContext>().Length == 1, "Existing ProjectContext reused", checks);
        Check(prefab.GetComponents<ProjectManagerInstaller>().Length == 1, "One global installer", checks);
        var container = new DiContainer();
        var installer = prefab.GetComponent<ProjectManagerInstaller>();
        container.Inject(installer);
        installer.InstallBindings();
        var bound = container.Resolve<ProjectAnalytics>();
        Check(bound is UnityAnalyticsService && ReferenceEquals(bound, container.Resolve<ProjectAnalytics>()), "Real installer binds one service", checks);
        var child = container.CreateSubContainer();
        Check(ReferenceEquals(bound, child.Resolve<ProjectAnalytics>()), "Scene child container shares project instance", checks);
        Check(container.Resolve<AnalyticsConfiguration>().ResolveEnvironment(true, false) == "development", "Serialized/default configuration safe", checks);
        Check(bound.State == AnalyticsInitializationState.NotInitialized, "Construction alone does not initialize SDK", checks);
        ((IDisposable)bound).Dispose();

        // Native EndUserConsent revoke can be tested without ever granting real consent or initializing the SDK.
        if (actualConsent.AnalyticsIntent != ConsentStatus.Granted)
        {
            using (var real = new UnityAnalyticsBackend())
            {
                int changed = 0;
                real.ConsentChanged += _ => changed++;
                try
                {
                    real.SetConsent(false);
                    var revoked = EndUserConsent.GetConsentState();
                    Check(revoked.AnalyticsIntent == ConsentStatus.Denied && revoked.AdsIntent == actualConsent.AdsIntent,
                        "Real revoke uses AnalyticsIntent and preserves AdsIntent", checks);
                    Check(real.Consent == ConsentStatus.Denied, "Native consent visible to adapter", checks);
                }
                finally { EndUserConsent.SetConsentState(actualConsent); }
            }
        }
        Check(EndUserConsent.GetConsentState().AnalyticsIntent == actualConsent.AnalyticsIntent &&
              EndUserConsent.GetConsentState().AdsIntent == actualConsent.AdsIntent, "Original native consent restored", checks);
        return new { success = true, count = checks.Count, checks, realConsentNeverGranted = true, realEventsRecorded = 0 };
    }

    public static async Task<object> Dashboard()
    {
        try
        {
            var api = EnvironmentsApi.Instance;
            var refresh = api.RefreshAsync();
            if (await Task.WhenAny(refresh, Task.Delay(15000)) != refresh)
                return new { verified = false, reason = "Dashboard environment refresh timed out", cloudProjectId = Application.cloudProjectId };
            await refresh;
            var environments = new List<object>();
            bool development = false, production = false;
            foreach (var environment in api.Environments)
            {
                environments.Add(new { environment.Name, environment.Id, environment.IsDefault });
                development |= environment.Name == "development";
                production |= environment.Name == "production";
            }
            return new { verified = true, cloudProjectId = Application.cloudProjectId, development, production, environments };
        }
        catch (Exception exception)
        {
            return new { verified = false, reason = exception.GetType().Name + ": " + exception.Message, cloudProjectId = Application.cloudProjectId };
        }
    }

    private static void Check(bool condition, string name, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks.Add(name);
    }
}
