using System.Collections.Generic;
using UnityEngine;
using Zenject;

[DefaultExecutionOrder(-600)]
public sealed class OrbitPassiveAbility : PassiveAbility, IStatValueResolver
{
    public override IStatValueProvider DisplayStatSnapshot => settings;
    public float RuntimeContactDamage => settings != null ? settings.ContactDamage * damageMultiplier : 0f;
    public bool IsRuntime => true;
    public bool TryRead(StatValueDefinition definition, out StatValueResult result)
    {
        bool found = settings != null && definition.ProviderType == typeof(OrbitShipMetaContract)
            && definition.Property == nameof(OrbitShipMetaContract.ContactDamage);
        result = new StatValueResult(RuntimeContactDamage, StatValueState.Runtime);
        return found;
    }
    [SerializeField, Tooltip("Separate circular satellite prefab, without a ParentShip component.")]
    private OrbitSatellite satellitePrefab;

    [InjectOptional] private EnemyProjectileCollisionRegistry projectileRegistry;
    [Inject] private DealDamageManager damageManager;
    private readonly Dictionary<Enemy, float> nextEnemyHitTimes = new();
    private readonly List<Enemy> expiredEnemies = new();
    private OrbitSatellite[] satellites;
    private OrbitShipMetaContract settings;
    private float maximumHoldTime;
    private float heldTime;
    private float holdStartIntensity;
    private float intensity;
    private float angle;
    private float damageMultiplier = 1f;
    private bool isHeld;

    public float CurrentOrbitRadius => settings == null ? 0f
        : Mathf.Lerp(settings.BaseOrbitRadius, settings.MaximumOrbitRadius, intensity);
    public float CurrentAngularSpeed => settings == null ? 0f
        : Mathf.Lerp(settings.BaseAngularSpeed, settings.MaximumAngularSpeed, intensity);
    public float CurrentObjectRadius => settings == null ? 0f : settings.EvaluateObjectRadius(intensity);
    public float Intensity => intensity;
    public bool IsHeld => isHeld;

    public override void Init(ParentShip ship) => owner = ship;

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        stats.TryGetContract(out settings);
        if (satellites != null && settings != null)
        {
            for (int i = 0; i < satellites.Length; i++)
                if (satellites[i] != null)
                    satellites[i].ApplyShipMetaStats(settings);
        }
        maximumHoldTime = 0f;
        if (stats.TryGetContract(out AbilityRecoveryShipMetaContract recovery)
            && recovery.AbilityMode == UltimateAbilityMode.Toggle)
        {
            AbilityMetaRecoverySettings resource = recovery.Settings;
            maximumHoldTime = resource.ToggleMaximumTime / resource.ToggleTimeCostPerSecond;
        }
    }

    public override void ApplyBattleAbilityDamageMultiplier(float value)
    {
        damageMultiplier = Mathf.Max(1f, value);
    }

    public override void On()
    {
        if (owner == null)
            owner = GetComponent<ParentShip>();
        if (owner == null || settings == null || satellitePrefab == null
            || maximumHoldTime <= 0f)
        {
            Debug.LogError("Orbit requires its satellite prefab, Orbit Meta contract and Toggle recovery contract.", this);
            return;
        }

        base.On();
        EnsureSatellites();
        PositionSatellites();
        for (int i = 0; i < satellites.Length; i++)
            satellites[i].gameObject.SetActive(true);
    }

    public override void Off()
    {
        base.Off();
        isHeld = false;
        heldTime = intensity = 0f;
        nextEnemyHitTimes.Clear();
        if (satellites == null)
            return;
        for (int i = 0; i < satellites.Length; i++)
            if (satellites[i] != null)
                satellites[i].gameObject.SetActive(false);
    }

    public bool BeginHold()
    {
        if (!isActive || owner == null || !owner.IsVisible || owner.IsDestroyed)
            return false;
        isHeld = true;
        heldTime = 0f;
        holdStartIntensity = intensity;
        return true;
    }

    public void EndHold() => isHeld = false;

    private void FixedUpdate()
    {
        if (!isActive || settings == null || owner == null || !owner.IsVisible)
            return;

        float dt = Time.fixedDeltaTime;
        if (isHeld)
        {
            float duration = settings.ChargeUpDuration;
            heldTime = Mathf.Min(duration, heldTime + dt);
            intensity = Mathf.Lerp(holdStartIntensity, 1f, heldTime / duration);
        }
        else
        {
            intensity = Mathf.MoveTowards(intensity, 0f, settings.CollapseSpeed * dt);
        }
        float direction = settings.Clockwise ? -1f : 1f;
        angle = Mathf.Repeat(angle + direction * CurrentAngularSpeed * dt, 360f);
        PositionSatellites();
        PruneDamageTimers();
    }

    private void EnsureSatellites()
    {
        if (satellites != null && satellites.Length == settings.ObjectCount)
            return;
        DestroySatellites();
        satellites = new OrbitSatellite[settings.ObjectCount];
        for (int i = 0; i < satellites.Length; i++)
        {
            OrbitSatellite satellite = Instantiate(satellitePrefab);
            satellite.gameObject.SetActive(false);
            satellite.name = $"{owner.name} Orbit Satellite {i + 1}";
            satellite.Configure(this, projectileRegistry, settings.ObjectRadius, settings.ProjectileDestructionPolicy);
            satellites[i] = satellite;
        }
    }

    private void PositionSatellites()
    {
        float step = 360f / satellites.Length;
        Vector3 center = owner.transform.position;
        float radius = CurrentOrbitRadius;
        float objectRadius = CurrentObjectRadius;
        for (int i = 0; i < satellites.Length; i++)
        {
            float radians = (angle + i * step) * Mathf.Deg2Rad;
            satellites[i].SetRadius(objectRadius);
            satellites[i].SetPosition(center + new Vector3(
                Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f));
        }
    }

    internal void NotifyEnemyProjectileIntercepted()
    {
        if (!isActive || settings == null || owner == null
            || !owner.IsVisible || owner.IsDestroyed)
            return;

        owner.ActiveAbility?.ReduceCooldown(settings.CooldownReductionPerProjectile);
    }

    internal void TryDamageEnemy(Enemy enemy)
    {
        if (!isActive || enemy == null || enemy.isDead || settings.ContactDamage <= 0f)
            return;
        if (nextEnemyHitTimes.TryGetValue(enemy, out float nextHit) && Time.time < nextHit)
            return;

        nextEnemyHitTimes[enemy] = Time.time + settings.DamageInterval;
        float damage = RuntimeContactDamage;
        damageManager.DealDamage(enemy, owner, damage, settings.DamageType);
    }

    private void PruneDamageTimers()
    {
        expiredEnemies.Clear();
        foreach (var entry in nextEnemyHitTimes)
            if (entry.Key == null || entry.Key.isDead || entry.Value <= Time.time)
                expiredEnemies.Add(entry.Key);
        for (int i = 0; i < expiredEnemies.Count; i++)
            nextEnemyHitTimes.Remove(expiredEnemies[i]);
    }

    private void OnDisable() => Off();
    private void OnDestroy() => DestroySatellites();

    private void DestroySatellites()
    {
        if (satellites == null)
            return;
        for (int i = 0; i < satellites.Length; i++)
        {
            if (satellites[i] == null)
                continue;
            satellites[i].gameObject.SetActive(false);
            Destroy(satellites[i].gameObject);
        }
        satellites = null;
    }

    private void OnDrawGizmosSelected()
    {
        ShipMetaConfig meta = owner != null && owner.ShipData != null
            ? owner.ShipData.ShipMetaConfig : GetComponent<ParentShip>()?.ShipData?.ShipMetaConfig;
        if (meta == null || !meta.GetLevel(0).TryGetContract(out OrbitShipMetaContract preview))
            return;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.7f);
        float radius = Application.isPlaying ? CurrentOrbitRadius : preview.BaseOrbitRadius;
        float objectRadius = Application.isPlaying ? CurrentObjectRadius : preview.ObjectRadius;
        for (int i = 0; i < preview.ObjectCount; i++)
        {
            float radians = (angle + i * 360f / preview.ObjectCount) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius;
            Gizmos.DrawWireSphere(transform.position + offset, objectRadius);
            float direction = preview.Clockwise ? -1f : 1f;
            Vector3 tangent = new Vector3(-Mathf.Sin(radians), Mathf.Cos(radians), 0f) * direction;
            Vector3 position = transform.position + offset;
            Vector3 arrowEnd = position + tangent * objectRadius * 3f;
            Vector3 side = new Vector3(-tangent.y, tangent.x, 0f) * objectRadius * 0.4f;
            Gizmos.DrawLine(position + tangent * objectRadius, arrowEnd);
            Gizmos.DrawLine(arrowEnd, arrowEnd - tangent * objectRadius + side);
            Gizmos.DrawLine(arrowEnd, arrowEnd - tangent * objectRadius - side);
        }
    }
}
