using System;
using UnityEngine;
using Zenject;

public enum BossAttackSelectionMode { Sequential, Random }

[Serializable]
public sealed class BossPhaseDefinition
{
    [SerializeField] private string phaseName = "Phase";
    [SerializeField, Range(0f, 100f), Tooltip("HP percentage that starts the NEXT phase. Ignored on the last phase.")]
    private float nextPhaseHealthThresholdPercent;
    [SerializeField, Min(0f)] private float startDelay = .5f;
    [SerializeField, Min(0f), Tooltip("Additional pause after the attack's own cooldown. Sequence uses its step delays instead.")]
    private float delayBetweenAttacks = 1f;
    [SerializeField] private BossAttackSelectionMode selectionMode;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private DirectedWaveAttackExecutionMode executionMode;
    [SerializeField] private DirectedWaveAttackPattern[] attacks = Array.Empty<DirectedWaveAttackPattern>();
    [SerializeField] private DirectedWaveAttackSequenceSettings sequence = new();

    public string PhaseName => phaseName;
    public float NextPhaseHealthThresholdPercent => nextPhaseHealthThresholdPercent;
    public float StartDelay => startDelay;
    public float DelayBetweenAttacks => delayBetweenAttacks;
    public BossAttackSelectionMode SelectionMode => selectionMode;
    public int RandomSeed => randomSeed;
    public DirectedWaveAttackExecutionMode ExecutionMode => executionMode;
    public DirectedWaveAttackPattern[] Attacks => attacks;
    public DirectedWaveAttackSequenceSettings Sequence => sequence;

    public void Validate()
    {
        nextPhaseHealthThresholdPercent = Mathf.Clamp(nextPhaseHealthThresholdPercent, 0f, 100f);
        startDelay = Mathf.Max(0f, startDelay);
        delayBetweenAttacks = Mathf.Max(0f, delayBetweenAttacks);
        attacks ??= Array.Empty<DirectedWaveAttackPattern>();
        sequence ??= new DirectedWaveAttackSequenceSettings();
    }
}

[RequireComponent(typeof(SimpleEnemyAttackExecutor))]
public sealed class BossController : Enemy, IEnemyEmissionOriginContext
{
    [InjectOptional] private PlayerController playerController;
    [SerializeField] private BossPhaseDefinition[] phases = Array.Empty<BossPhaseDefinition>();

    private BossAttackDriver attacks;
    private int currentPhaseIndex = -1;
    private bool started, defeatNotified;

    public int CurrentPhaseIndex => currentPhaseIndex;
    public BossPhaseDefinition[] Phases => phases;
    public Vector3 AttackTargetPosition => playerController != null
        ? playerController.transform.position : transform.position + Vector3.down;
    public bool HasEmissionOriginPlayer => playerController != null;
    public Camera EmissionOriginCamera => playerController != null ? playerController.GameplayCamera : Camera.main;
    public event Action<int, BossPhaseDefinition> PhaseChanged;
    public event Action<BossController> Defeated;

    public override void Awake()
    {
        base.Awake();
        attacks = new BossAttackDriver(this, GetComponent<SimpleEnemyAttackExecutor>(), this);
    }

    private void Start()
    {
        started = true;
        if (phases.Length == 0)
        {
            Debug.LogWarning($"Boss '{name}' has no configured phases.", this);
            return;
        }
        EnterPhase(0);
    }

    private void OnEnable()
    {
        if (started && !isDead && currentPhaseIndex >= 0) EnterPhase(currentPhaseIndex);
    }

    private void Update()
    {
        if (!isDead) attacks?.Tick(AttackTime);
    }

    private void OnDisable() => attacks?.Stop();
    private void OnDestroy() => attacks?.Stop();

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        if (isDead || currentPhaseIndex < 0 || _maxHealth <= 0f) return;
        float healthPercent = Mathf.Max(0f, _currentHealth) / _maxHealth * 100f;
        int next = currentPhaseIndex;
        while (next < phases.Length - 1 && healthPercent <= phases[next].NextPhaseHealthThresholdPercent) next++;
        if (next != currentPhaseIndex) EnterPhase(next);
    }

    public override void Dying()
    {
        if (isDead || defeatNotified) return;
        defeatNotified = true;
        attacks?.Stop();
        base.Dying();
        Defeated?.Invoke(this);
    }

    private void EnterPhase(int index)
    {
        currentPhaseIndex = index;
        var phase = phases[index];
        if (phase == null) { attacks.Stop(); return; }
        attacks.Begin(phase, AttackTime);
        PhaseChanged?.Invoke(index, phase);
    }

    private void OnValidate()
    {
        phases ??= Array.Empty<BossPhaseDefinition>();
        for (int i = 0; i < phases.Length; i++)
        {
            phases[i] ??= new BossPhaseDefinition();
            phases[i].Validate();
        }
    }
}
