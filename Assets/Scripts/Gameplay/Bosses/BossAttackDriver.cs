using System.Collections.Generic;
using UnityEngine;

// Boss-only host: the shared executor owns geometry/emission; the shared schedule and
// Sequence player own shot timing, repeat blocks, pauses and child-completion waits.
public sealed class BossAttackDriver : IDirectedWaveAttackSequenceHost
{
    private sealed class ResolvedAttack
    {
        public DirectedWaveAttackSettings Settings;
        public EnemyBurstAttackSettings Profile;
    }

    private readonly Enemy owner;
    private readonly SimpleEnemyAttackExecutor executor;
    private readonly IEnemyEmissionOriginContext target;
    private readonly Dictionary<object, ResolvedAttack> resolved = new();
    private BossPhaseDefinition phase;
    private DirectedWaveAttackSequencePlayer sequence;
    private Run active;
    private System.Random random;
    private int nextPattern;
    private float time, readyTime;
    private bool started, warned;

    public bool HasActiveAttack => active != null && !active.RootComplete;
    public bool SequenceComplete => sequence != null && sequence.IsComplete;

    public BossAttackDriver(Enemy owner, SimpleEnemyAttackExecutor executor, IEnemyEmissionOriginContext target)
    {
        this.owner = owner;
        this.executor = executor;
        this.target = target;
        if (executor != null) executor.SetWaveAttackControl(true);
    }

    public void Begin(BossPhaseDefinition definition, float attackTime)
    {
        Stop();
        phase = definition;
        time = attackTime;
        readyTime = time + phase.StartDelay;
        nextPattern = 0;
        random = new System.Random(phase.RandomSeed);
        warned = false;
        if (executor == null) return;
        executor.SetWaveAttackControl(true);
        executor.SetWaveAimTarget(target);
        if (phase.ExecutionMode == DirectedWaveAttackExecutionMode.Sequence)
            sequence = new DirectedWaveAttackSequencePlayer(phase.Sequence, this);
    }

    public void Tick(float attackTime)
    {
        time = attackTime;
        if (phase == null || executor == null || owner == null || owner.isDead
            || owner.AttackPermissions != null && owner.AttackPermissions.Interrupted) return;
        // Reassert control after re-enabling the executor; autonomous attacks must never
        // escape during a phase delay or a Sequence pause.
        executor.SetWaveAttackControl(true);
        executor.SetWaveAimTarget(target);
        if (sequence != null)
        {
            if (!started && time >= readyTime) { started = true; sequence.Begin(time, true); }
            if (started) sequence.Tick(time);
            return;
        }
        if (active != null)
        {
            active.Tick();
            if (!active.RootComplete) return;
            active = null;
            readyTime = time + phase.DelayBetweenAttacks;
        }
        if (time < readyTime || owner.AttackPermissions != null && !owner.AttackPermissions.AllowNewAttacks) return;
        var patterns = phase.Attacks;
        if (patterns == null || patterns.Length == 0) return;
        for (int attempt = 0; attempt < patterns.Length; attempt++)
        {
            int index = phase.SelectionMode == BossAttackSelectionMode.Random
                ? random.Next(patterns.Length) : nextPattern++ % patterns.Length;
            if (patterns[index] == null) continue;
            StartRun(Resolve(patterns[index], null));
            break;
        }
    }

    public bool TryStart(DirectedWaveAttackSequenceStep step, out IDirectedWaveAttackSequenceRun execution)
    {
        execution = null;
        if (executor == null || owner == null || owner.isDead || HasActiveAttack
            || owner.AttackPermissions != null && !owner.AttackPermissions.AllowNewAttacks) return false;
        active = StartRun(Resolve(step.pattern, step));
        execution = active;
        return true;
    }

    private ResolvedAttack Resolve(DirectedWaveAttackPattern pattern, DirectedWaveAttackSequenceStep step)
    {
        object key = (object)step ?? pattern;
        if (resolved.TryGetValue(key, out var result)) return result;
        var settings = new DirectedWaveAttackSettings();
        if (step != null) step.CopySettingsTo(settings);
        else pattern.CopyResolvedSettingsTo(settings);
        var source = EnemyAttackSettingsResolver.Resolve(executor, settings.UsesEnemyBurstSettings, settings.WaveBurstSettings);
        var profile = EnemyAttackSettingsResolver.CreateRuntimeCopy(source, null,
            settings.HasRuntimeProjectileBaseSpeedOverride ? settings.RuntimeProjectileBaseSpeedOverride : (float?)null);
        EnemyAttackSettingsResolver.ApplyRuntimeSpeedRandomization(profile, null, settings);
        profile.ScaleAttackTiming(settings.RuntimeAttackSpeedMultiplier);
        result = new ResolvedAttack { Settings = settings, Profile = profile };
        resolved.Add(key, result);
        return result;
    }

    private Run StartRun(ResolvedAttack settings)
    {
        active = new Run(this, settings);
        return active;
    }

    public void Stop()
    {
        sequence?.Stop();
        sequence = null;
        active?.Stop();
        active = null;
        phase = null;
        started = false;
        if (executor == null) return;
        executor.SetAttackCompletionTracker(null);
        executor.SetWaveAttackSettings(null); // Cancels deferred Sectors, not spawned ECS projectiles.
        executor.SetWaveAimTarget(null);
        executor.SetWaveAttackControl(true);
    }

    private sealed class Run : IDirectedWaveAttackSequenceRun
    {
        private struct PatternState
        {
            public EnemyAttackPatternSchedule Schedule;
            public DirectedWaveAttackAimState Aim;
            public bool CoolingDown;
            public float ReadyTime;
        }
        private readonly BossAttackDriver host;
        private readonly ResolvedAttack attack;
        private readonly PatternState[] patterns;
        private readonly EnemyAttackCompletionTracker completion = new();
        public bool RootComplete { get; private set; }
        public bool ChildrenComplete => completion.IsComplete;

        public Run(BossAttackDriver host, ResolvedAttack attack)
        {
            this.host = host;
            this.attack = attack;
            var executor = host.executor;
            executor.SetWaveAttackSettings(attack.Profile);
            executor.SetAttackCompletionTracker(completion);
            executor.BeginAttackSequence(attack.Profile);
            executor.BeginEmissionOriginAttack(attack.Profile);
            patterns = new PatternState[attack.Profile.PatternCount];
            for (int i = 0; i < patterns.Length; i++)
            {
                var profile = attack.Profile.GetPattern(i);
                patterns[i].Schedule.Begin(profile, host.time, host.owner.FireRateMultiplier, true,
                    executor.NumericRegistry.Get(profile));
            }
        }

        public void Tick()
        {
            if (RootComplete) return;
            if (host.owner == null || host.owner.isDead || !host.executor.CanPerformWaveAttack)
            {
                if (host.owner != null && !host.owner.isDead && !host.warned)
                {
                    host.warned = true;
                    Debug.LogWarning("Boss cannot fire: check the shared executor's projectile references and ECS spawner injection.", host.owner);
                }
                Stop();
                return;
            }
            if (host.owner.AttackPermissions != null && host.owner.AttackPermissions.Interrupted) return;
            bool complete = true;
            for (int i = 0; i < patterns.Length; i++)
            {
                ref var state = ref patterns[i];
                var profile = attack.Profile.GetPattern(i);
                // Match the common schedule's per-frame work limit for randomized zero intervals.
                int budget = 128;
                while (budget-- > 0 && state.Schedule.IsDue(host.time))
                {
                    if (!Fire(profile, ref state.Aim, state.Schedule.IsBurstStart)) { Stop(); return; }
                    state.Schedule.Advance(profile, host.time, host.owner.FireRateMultiplier, 0f);
                }
                if (!state.Schedule.IsComplete || host.executor.HasPendingEmissions(profile)) { complete = false; continue; }
                if (!state.CoolingDown)
                {
                    state.CoolingDown = true;
                    state.ReadyTime = host.time + EnemyAttackCooldownSampler.Sample(profile,
                        host.executor.NumericRegistry, attack.Settings) / Mathf.Max(.01f, host.owner.FireRateMultiplier);
                }
                if (host.time < state.ReadyTime) complete = false;
            }
            if (complete) Stop();
        }

        private bool Fire(EnemyBurstAttackSettings profile, ref DirectedWaveAttackAimState aim, bool burstStart)
        {
            if (!host.executor.PrepareEmissionOriginShot(profile, burstStart)) return false;
            var firing = profile.Shooting.PresetFiring;
            var mode = firing.enabled ? firing.fireMode : attack.Settings.FireMode;
            var forward = host.executor.AttackForward;
            if (mode == DirectedWaveAttackFireMode.None) return true;
            if (mode == DirectedWaveAttackFireMode.Stationary)
                return (firing.enabled ? firing.stationaryFireSettings : attack.Settings.StationaryFireSettings)
                    .TryFire(host.executor, profile, forward);
            if (mode == DirectedWaveAttackFireMode.Default) return host.executor.TryFireInDirection(forward, profile);
            if (host.target == null || !host.target.HasEmissionOriginPlayer)
            {
                if (!host.warned)
                {
                    host.warned = true;
                    Debug.LogWarning("Boss aimed attack requires a PlayerController injected by Zenject. Stationary and Default firing do not require a player.", host.owner);
                }
                return false;
            }
            if (mode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
            {
                Vector3 direction = host.target.AttackTargetPosition - host.owner.transform.position;
                float halfAngle = firing.enabled ? firing.forwardFireHalfAngle : attack.Settings.ForwardFireHalfAngle;
                if (direction.sqrMagnitude > .0001f && Vector3.Dot(direction.normalized, forward.normalized)
                    < Mathf.Cos(halfAngle * Mathf.Deg2Rad)) return true;
                return host.executor.TryFireInDirection(forward, profile);
            }
            var aimMode = firing.enabled ? firing.aimMode : attack.Settings.AimMode;
            if (aimMode == DirectedWaveAttackAimMode.AimContinuous)
                return host.executor.TryFireAt(host.target.AttackTargetPosition, profile);
            return aim.TryFire(host.owner, host.executor, profile, host.target.AttackTargetPosition,
                aimMode == DirectedWaveAttackAimMode.AimOnBurst && profile.RepeatBurst && burstStart);
        }

        public void Stop()
        {
            if (RootComplete) return;
            RootComplete = true;
            host.executor.EndAttackSequence(attack.Profile);
            host.executor.SetAttackCompletionTracker(null);
            host.executor.SetWaveAttackSettings(null);
        }
    }
}
