using UnityEngine;

public sealed class ChronosTimeFractureActiveAbility : ActiveAbility
{
    [SerializeField] private ChronosTimeFractureField fieldPrefab;
    [SerializeField] private Vector2 fieldSpawnOffset = new Vector2(0f, 1.2f);
    [SerializeField, Min(0.1f)] private float duration = 4f;
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField, Range(0.05f, 1f)] private float enemySpeedMultiplier = 0.4f;
    [SerializeField, Range(0.05f, 1f)] private float enemyProjectileSpeedMultiplier = 0.3f;
    [SerializeField, Min(1f)] private float playerProjectileSpeedMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float playerProjectileDamageMultiplier = 1.35f;
    [SerializeField, Min(0f)] private float collapseDamage = 45f;

    public override bool Activate(ParentShip owner)
    {
        if (owner == null)
            return false;

        ChronosTimeFractureField field = CreateField();
        if (field == null)
            return false;

        field.transform.position = owner.transform.position + (Vector3)fieldSpawnOffset;
        field.Configure(
            duration,
            radius,
            enemySpeedMultiplier,
            enemyProjectileSpeedMultiplier,
            playerProjectileSpeedMultiplier,
            playerProjectileDamageMultiplier,
            collapseDamage,
            owner);

        return true;
    }

    private ChronosTimeFractureField CreateField()
    {
        if (fieldPrefab != null)
            return Instantiate(fieldPrefab);

        GameObject fieldObject = new GameObject("Chronos Time Fracture Field");
        return fieldObject.AddComponent<ChronosTimeFractureField>();
    }
}
