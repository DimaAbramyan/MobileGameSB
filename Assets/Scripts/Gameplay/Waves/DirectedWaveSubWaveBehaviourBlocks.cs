using UnityEngine;

// These components own the designer-facing data for a directed subwave.
// DirectedEnemySubWave remains the runtime conductor: it spawns enemies and
// coordinates the optional blocks in their configured order.

[DisallowMultipleComponent]
public sealed class DirectedWaveFormationBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWaveFormationConfiguration configuration = new();

    public DirectedWaveFormationConfiguration Configuration =>
        configuration ??= new DirectedWaveFormationConfiguration();
}

[DisallowMultipleComponent]
public sealed class DirectedWavePhaseEntryBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWavePhaseEntryConfiguration configuration = new();

    public DirectedWavePhaseEntryConfiguration Configuration =>
        configuration ??= new DirectedWavePhaseEntryConfiguration();
}

[DisallowMultipleComponent]
public sealed class DirectedWavePostBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWavePostBehaviourConfiguration configuration = new();

    public DirectedWavePostBehaviourConfiguration Configuration =>
        configuration ??= new DirectedWavePostBehaviourConfiguration();
}

[DisallowMultipleComponent]
public sealed class DirectedWaveCompletionBehaviour : MonoBehaviour
{
    [SerializeField] private DirectedWaveCompletionConfiguration configuration = new();

    public DirectedWaveCompletionConfiguration Configuration =>
        configuration ??= new DirectedWaveCompletionConfiguration();
}
