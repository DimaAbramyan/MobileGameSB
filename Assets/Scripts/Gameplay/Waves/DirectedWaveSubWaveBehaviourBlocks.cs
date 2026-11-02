using UnityEngine;

// These components own the designer-facing data for a directed subwave.
// DirectedEnemySubWave remains the runtime conductor: it spawns enemies and
// coordinates the optional blocks in their configured order.

[ExecuteAlways, DisallowMultipleComponent]
public sealed class DirectedWaveFormationBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWaveFormationConfiguration configuration = new();

    public DirectedWaveFormationConfiguration Configuration =>
        configuration ??= new DirectedWaveFormationConfiguration();

    private void Reset() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnEnable() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnDestroy() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
}

[ExecuteAlways, DisallowMultipleComponent]
public sealed class DirectedWavePhaseEntryBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWavePhaseEntryConfiguration configuration = new();

    public DirectedWavePhaseEntryConfiguration Configuration =>
        configuration ??= new DirectedWavePhaseEntryConfiguration();

    private void Reset() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnEnable() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnDestroy() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
}

[ExecuteAlways, DisallowMultipleComponent]
public sealed class DirectedWavePostBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWavePostBehaviourConfiguration configuration = new();

    public DirectedWavePostBehaviourConfiguration Configuration =>
        configuration ??= new DirectedWavePostBehaviourConfiguration();

    private void Reset() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnEnable() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnDestroy() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
}

[ExecuteAlways, DisallowMultipleComponent]
public sealed class DirectedWaveCompletionBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWaveCompletionConfiguration configuration = new();

    public DirectedWaveCompletionConfiguration Configuration =>
        configuration ??= new DirectedWaveCompletionConfiguration();

    private void Reset() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnEnable() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
    private void OnDestroy() => DirectedWaveBehaviourCacheInvalidation.Notify(this);
}

internal static class DirectedWaveBehaviourCacheInvalidation
{
    public static void Notify(Component component)
    {
        if (component.TryGetComponent(out DirectedEnemySubWave wave))
            wave.InvalidateBehaviourComponentCache();
    }
}
