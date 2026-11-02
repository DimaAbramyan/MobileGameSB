using System;
using System.Collections.Generic;
using UnityEngine;

public enum WaveAttackGroupCompletion { Timed, AttackCycles, AllCompleted, FirstCompleted }
public enum WaveAttackEnemyDeath { SkipParticipant, CompleteGroup }
public enum WaveAttackParticipantState { Pending, Ready, Gone }

[Serializable]
public sealed class WaveAttackSubWaveReference
{
    [Min(0)] public int scheduleIndex;
    public GameObject subWavePrefab;
}

[Serializable]
public sealed class WaveAttackCoordinatorGroup
{
    public string groupName = "Group";
    public List<WaveAttackSubWaveReference> controlledSubWaves = new();
    [Min(0f)] public float startDelay;
    public bool waitUntilReady = true;
    public WaveAttackGroupCompletion completionMode = WaveAttackGroupCompletion.AllCompleted;
    [Min(1)] public int attackCyclesCount = 1;
    [Min(0f)] public float duration = 5f;
    [Min(0f), Tooltip("Zero disables the timeout. Includes waiting for spawn / entry readiness.")]
    public float maxWaitDuration = 30f;
    [Min(0f)] public float delayAfterGroup;
    [Min(0f), Tooltip("Timed: start the following group this much before the timed window ends. Other modes: allow the outgoing attack to drain concurrently, with this timeout.")]
    public float overlapDuration;
    [Tooltip("Individual dead enemy slots are skipped by AttackBehaviour. This policy applies when a controlled subwave has no living enemies left.")]
    public WaveAttackEnemyDeath onEnemyDeath;
}

[Serializable]
public sealed class WaveAttackCoordinatorSettings
{
    public bool enabled = true;
    [Min(0f)] public float startDelay;
    public bool loopSequence = true;
    [Min(1)] public int loopCount = 1;
    [Min(0f)] public float globalCooldown;
    [Min(0)] public int startGroupIndex;
    [Tooltip("Reserved for randomized coordinator ordering. Does not replace seeds authored in attack patterns.")]
    public int randomSeed = 12345;
    public WaveAttackSwitchMode switchMode = WaveAttackSwitchMode.FinishCurrentAttack;
    public WaveAttackReactivation reactivation = WaveAttackReactivation.Restart;
    public List<WaveAttackCoordinatorGroup> groups = new();
}

// A scheduling client of AttackBehaviour, never a projectile emitter.
[DisallowMultipleComponent, RequireComponent(typeof(Wave))]
public sealed class WaveAttackCoordinator : MonoBehaviour
{
    [SerializeField] private WaveAttackCoordinatorSettings settings = new();
    private readonly Dictionary<int, IWaveAttackCoordinatorParticipant> participants = new();
    private readonly Dictionary<int, GameObject> sources = new();
    private WaveAttackCoordinatorPlayer player;
    private bool runningRequested;
    public WaveAttackCoordinatorSettings Settings => settings ??= new();
    public int ConfigurationRevision { get; private set; }
    public WaveAttackCoordinatorPlayer Player => player;
    public bool CoordinationEnabled => isActiveAndEnabled && Settings.enabled;

    internal void Register(int scheduleIndex, InfoAboutSubWave subWave, GameObject source)
    {
        sources[scheduleIndex] = source;
        if (subWave is DirectedEnemySubWave directed && directed.AttackPatternBehaviour != null)
            participants[scheduleIndex] = new RuntimeParticipant(directed);
    }
    internal void BeginCoordination(float time)
    {
        DisposePlayer();
        runningRequested = true;
        if (!CoordinationEnabled) return;
        player = new WaveAttackCoordinatorPlayer(Settings, ResolveParticipant);
        player.Begin(time);
    }
    private void Update()
    {
#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
        using var performanceScope = PerformanceMarker.Auto();
#endif
        if (!CoordinationEnabled) { DisposePlayer(); return; }
        if (runningRequested && player == null) BeginCoordination(Time.time);
        player?.Tick(Time.time);
    }
    private IWaveAttackCoordinatorParticipant ResolveParticipant(int index)
    {
        if (!sources.TryGetValue(index, out var source)) return null;
        foreach (var group in Settings.groups)
            if (group?.controlledSubWaves != null)
                foreach (var reference in group.controlledSubWaves)
                    if (reference != null && reference.scheduleIndex == index && reference.subWavePrefab != null && reference.subWavePrefab != source) return null;
        return participants.TryGetValue(index, out var participant) ? participant : null;
    }
    public void StopCoordination() { runningRequested = false; DisposePlayer(); }
#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
    private static readonly Unity.Profiling.ProfilerMarker PerformanceMarker = new("PerformanceSuite.WaveAttackCoordinator");
#endif
    public void RestartCoordination() => BeginCoordination(Time.time);
    private void DisposePlayer() { player?.Dispose(); player = null; }
    private void OnDisable() => DisposePlayer();
    private void OnDestroy() => StopCoordination();
    public void InvalidateConfiguration() => ConfigurationRevision++;
    private void OnValidate() => InvalidateConfiguration();

    private sealed class RuntimeParticipant : IWaveAttackCoordinatorParticipant
    {
        private readonly DirectedEnemySubWave wave;
        private readonly DirectedWaveAttackBehaviour attack;
        public RuntimeParticipant(DirectedEnemySubWave wave) { this.wave = wave; attack = wave.AttackPatternBehaviour; }
        public WaveAttackParticipantState State => wave == null || attack == null || wave.IsAttackParticipantGone
            ? WaveAttackParticipantState.Gone : wave.IsAttackReady && attack.isActiveAndEnabled
                ? WaveAttackParticipantState.Ready : WaveAttackParticipantState.Pending;
        public int CompletedCycles => attack != null ? attack.CompletedAttackCycles : 0;
        public bool IsIdle => attack == null || attack.IsAttackIdle;
        public void SetPermission(object owner, WaveAttackPermission permission)
        { if (attack != null) attack.SetExternalAttackPermission(owner, permission); }
        public void ReleasePermission(object owner) { if (attack != null) attack.ReleaseExternalAttackPermission(owner); }
        public void Reactivate(WaveAttackReactivation mode) { if (attack != null) attack.ReactivateCoordinatedAttack(mode); }
        public void SetCycleLimit(int limit) { if (attack != null) attack.SetCoordinatedCycleLimit(limit); }
    }
}
