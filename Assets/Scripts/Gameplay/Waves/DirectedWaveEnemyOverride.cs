using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DirectedWaveEnemyOverride : MonoBehaviour
{
    [SerializeField] private Enemy enemyPrefabOverride;

    [Header("Visual")]
    [SerializeField] private bool overrideSpriteTint;
    [SerializeField] private Color spriteTint = Color.white;

    [Header("Attack")]
    [SerializeField] private bool overrideBurstAttackSettings;
    [SerializeField] private EnemyBurstAttackSettings burstAttackSettings =
        new EnemyBurstAttackSettings();

    [Header("Four Way Rotation")]
    [SerializeField] private bool overrideFourWayRotation;
    [SerializeField] private DirectedWaveFourWayRotationOverride fourWayRotation =
        new DirectedWaveFourWayRotationOverride();

    private readonly List<MonoBehaviour> attackComponents = new(4);
    private readonly List<SpriteRenderer> spriteRenderers = new(4);

    public Enemy EnemyPrefabOverride => enemyPrefabOverride;

    public void ApplyTo(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (overrideSpriteTint)
            ApplySpriteTint(enemy);

        if (overrideBurstAttackSettings)
            ApplyBurstAttackSettings(enemy);

        if (overrideFourWayRotation)
        {
            FourWayEnemyRotationController rotationController =
                enemy.GetComponent<FourWayEnemyRotationController>();
            rotationController?.ApplyOverride(fourWayRotation);
        }
    }

    private void OnValidate()
    {
        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.Validate();
        fourWayRotation ??= new DirectedWaveFourWayRotationOverride();
        fourWayRotation.Validate();
    }

    private void ApplySpriteTint(Enemy enemy)
    {
        spriteRenderers.Clear();
        enemy.GetComponentsInChildren(true, spriteRenderers);
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer != null)
                renderer.color *= spriteTint;
        }

        spriteRenderers.Clear();
    }

    private void ApplyBurstAttackSettings(Enemy enemy)
    {
        attackComponents.Clear();
        enemy.GetComponents(attackComponents);
        for (int i = 0; i < attackComponents.Count; i++)
        {
            if (attackComponents[i] is IEnemyBurstAttackSettingsOverrideReceiver receiver)
                receiver.ApplyBurstAttackSettingsOverride(burstAttackSettings);
        }

        attackComponents.Clear();
    }
}

[System.Serializable]
public sealed class DirectedWaveFourWayRotationOverride
{
    [SerializeField] private FourWayEnemyRotationDirection direction =
        FourWayEnemyRotationDirection.Clockwise;
    [SerializeField] private FourWayEnemyRotationMode rotationMode =
        FourWayEnemyRotationMode.Continuous;
    [SerializeField, Min(0.01f)] private float rotationSpeedDegreesPerSecond = 90f;
    [SerializeField] private AnimationCurve rotationProgressCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private float rotationAngle = 360f;
    [SerializeField] private float rotationFromAngle;
    [SerializeField] private bool resetRotationOnEnable = true;

    public FourWayEnemyRotationDirection Direction => direction;
    public FourWayEnemyRotationMode RotationMode => rotationMode;
    public float RotationSpeedDegreesPerSecond =>
        Mathf.Max(0.01f, rotationSpeedDegreesPerSecond);
    public float RotationAngle => rotationAngle;
    public float RotationFromAngle => rotationFromAngle;
    public bool ResetRotationOnEnable => resetRotationOnEnable;

    public AnimationCurve CreateRotationProgressCurve()
    {
        AnimationCurve source = rotationProgressCurve != null
            && rotationProgressCurve.length > 0
            ? rotationProgressCurve
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);
        AnimationCurve copy = new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
        return copy;
    }

    public void Validate()
    {
        rotationSpeedDegreesPerSecond = Mathf.Max(0.01f, rotationSpeedDegreesPerSecond);
        if (rotationProgressCurve == null || rotationProgressCurve.length == 0)
            rotationProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}
