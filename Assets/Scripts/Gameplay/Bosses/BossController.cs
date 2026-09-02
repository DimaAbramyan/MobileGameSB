using System;
using System.Collections;

using UnityEngine;
using Zenject;

public enum BossAttackSelectionMode
{
    Sequential,
    Random
}

[Serializable]
public sealed class BossPhaseDefinition
{
    [SerializeField] private string phaseName = "Phase";
    [SerializeField, Range(0f, 100f)]
    [Tooltip("Health percentage at which the next phase begins. Ignored for the last phase.")]
    private float nextPhaseHealthThresholdPercent;
    [SerializeField, Min(0f)] private float startDelay = 0.5f;
    [SerializeField, Min(0f)] private float delayBetweenAttacks = 1f;
    [SerializeField] private BossAttackSelectionMode selectionMode;
    [SerializeField] private BossRadialAttackPattern[] attacks =
        Array.Empty<BossRadialAttackPattern>();

    public string PhaseName => phaseName;
    public float NextPhaseHealthThresholdPercent =>
        nextPhaseHealthThresholdPercent;
    public float StartDelay => startDelay;
    public float DelayBetweenAttacks => delayBetweenAttacks;
    public BossAttackSelectionMode SelectionMode => selectionMode;
    public BossRadialAttackPattern[] Attacks => attacks;

    public void Validate()
    {
        nextPhaseHealthThresholdPercent = Mathf.Clamp(
            nextPhaseHealthThresholdPercent,
            0f,
            100f);
        startDelay = Mathf.Max(0f, startDelay);
        delayBetweenAttacks = Mathf.Max(0f, delayBetweenAttacks);
        attacks ??= Array.Empty<BossRadialAttackPattern>();
    }
}

public sealed class BossController : Enemy
{
    [Inject] private BossProjectilePool projectilePool;
    [Inject] private PlayerController playerController;

    [Header("Boss phases")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private BossPhaseDefinition[] phases =
        Array.Empty<BossPhaseDefinition>();

    private Coroutine attackRoutine;
    private int currentPhaseIndex = -1;
    private int sequentialAttackIndex;

    public int CurrentPhaseIndex => currentPhaseIndex;
    public event Action<int, BossPhaseDefinition> PhaseChanged;
    public event Action<BossController> Defeated;

    public override void Awake()
    {
        base.Awake();
        currentPhaseIndex = -1;
        sequentialAttackIndex = 0;
    }

    private void Start()
    {
        if (phases.Length == 0)
        {
            Debug.LogWarning($"Boss '{name}' has no configured phases.", this);
            return;
        }

        EnterPhase(0);
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        if (isDead || currentPhaseIndex < 0)
            return;

        EvaluatePhaseTransition();
    }

    public override void Dying()
    {
        if (isDead)
            return;

        StopAttackRoutine();
        Defeated?.Invoke(this);
        base.Dying();
    }

    private void EvaluatePhaseTransition()
    {
        if (_maxHealth <= 0f)
            return;

        float healthPercent = Mathf.Max(0f, _currentHealth)
            / _maxHealth
            * 100f;
        int targetPhase = currentPhaseIndex;

        while (targetPhase < phases.Length - 1
            && healthPercent
            <= phases[targetPhase].NextPhaseHealthThresholdPercent)
        {
            targetPhase++;
        }

        if (targetPhase != currentPhaseIndex)
            EnterPhase(targetPhase);
    }

    private void EnterPhase(int phaseIndex)
    {
        StopAttackRoutine();
        currentPhaseIndex = Mathf.Clamp(phaseIndex, 0, phases.Length - 1);
        sequentialAttackIndex = 0;

        BossPhaseDefinition phase = phases[currentPhaseIndex];
        PhaseChanged?.Invoke(currentPhaseIndex, phase);
        attackRoutine = StartCoroutine(RunPhaseAttacks(currentPhaseIndex));
    }

    private IEnumerator RunPhaseAttacks(int phaseIndex)
    {
        BossPhaseDefinition phase = phases[phaseIndex];

        if (phase.StartDelay > 0f)
            yield return new WaitForSeconds(phase.StartDelay);

        while (!isDead && currentPhaseIndex == phaseIndex)
        {
            BossRadialAttackPattern attack = SelectAttack(phase);
            if (attack == null)
            {
                yield return null;
                continue;
            }

            yield return FireAttack(attack, phaseIndex);

            if (phase.DelayBetweenAttacks > 0f)
                yield return new WaitForSeconds(phase.DelayBetweenAttacks);
            else
                yield return null;
        }
    }

    private BossRadialAttackPattern SelectAttack(BossPhaseDefinition phase)
    {
        BossRadialAttackPattern[] attacks = phase.Attacks;
        if (attacks == null || attacks.Length == 0)
            return null;

        if (phase.SelectionMode == BossAttackSelectionMode.Random)
            return attacks[UnityEngine.Random.Range(0, attacks.Length)];

        BossRadialAttackPattern result =
            attacks[sequentialAttackIndex % attacks.Length];
        sequentialAttackIndex++;
        return result;
    }

    private IEnumerator FireAttack(
        BossRadialAttackPattern attack,
        int phaseIndex)
    {
        for (int volley = 0; volley < attack.VolleyCount; volley++)
        {
            if (isDead || currentPhaseIndex != phaseIndex)
                yield break;

            FireVolley(attack, volley);

            if (volley < attack.VolleyCount - 1
                && attack.DelayBetweenVolleys > 0f)
            {
                yield return new WaitForSeconds(
                    attack.DelayBetweenVolleys);
            }
        }
    }

    private void FireVolley(BossRadialAttackPattern attack, int volleyIndex)
    {
        if (attack.ProjectilePrefab == null)
        {
            Debug.LogError(
                $"Boss attack '{attack.name}' has no projectile prefab.",
                attack);
            return;
        }

        Transform originTransform = attackOrigin != null
            ? attackOrigin
            : transform;
        Vector2 origin = originTransform.position;
        Transform target = GetTarget();
        Vector2 targetPosition = target != null
            ? target.position
            : origin + Vector2.down;
        float aimAngle = attack.GetAimAngleDegrees(origin, targetPosition);

        for (int i = 0; i < attack.ProjectileCount; i++)
        {
            float angle = attack.GetProjectileAngleDegrees(
                i,
                volleyIndex,
                aimAngle);
            Vector2 direction = DirectionFromAngle(angle);
            Vector3 spawnPosition = origin + direction * attack.SpawnRadius;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            BossProjectileLaunchData launchData =
                new BossProjectileLaunchData(
                    direction,
                    target,
                    attack.FlightMode,
                    attack.InitialSpeed,
                    attack.Acceleration,
                    attack.AngularVelocityDegrees,
                    attack.HomingTurnSpeedDegrees,
                    attack.Lifetime,
                    attack.Damage,
                    attack.ProjectileScale,
                    attack.SpeedOverLifetime);

            projectilePool.Spawn(
                attack.ProjectilePrefab,
                spawnPosition,
                rotation,
                launchData);
        }
    }

    private Transform GetTarget()
    {
        return playerController != null ? playerController.transform : null;
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void StopAttackRoutine()
    {
        if (attackRoutine == null)
            return;

        StopCoroutine(attackRoutine);
        attackRoutine = null;
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
