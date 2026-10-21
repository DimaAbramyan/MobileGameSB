using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public sealed class EnemyContactImpact : MonoBehaviour
{
    // Retained for serialized prefab compatibility; incoming hits are fixed.
#pragma warning disable CS0414 // Legacy serialized data is intentionally no longer read.
    [SerializeField, HideInInspector] private float contactDamage = ParentShip.EnemyHitDamage;
#pragma warning restore CS0414

    [Header("Control")]
    [SerializeField, Min(0f)] private float controlLockDuration = 0.5f;

    [Header("Enemy Impact")]
    [SerializeField, Min(0f)] private float baseImpulse = 30f;
    [FormerlySerializedAs("velocityMultiplier")]
    [SerializeField, Min(0f)] private float relativeSpeedMultiplier = 3.75f;
    [SerializeField, Min(0f)] private float maximumImpulse = 70f;

    [Header("Ship Resistance")]
    [SerializeField, Min(0.001f)] private float referenceMass = 5f;
    [SerializeField, Min(0.001f)] private float referenceSpeed = 100f;
    [SerializeField, Min(0f)] private float massInfluence = 2f;
    [SerializeField, Min(0f)] private float speedInfluence = 0.1f;
    [SerializeField, Min(0f)] private float dragInfluence = 0.02f;

    [InjectOptional] private ShipKnockbackService knockbackService;

    private ShipKnockbackService KnockbackService =>
        knockbackService ??= new ShipKnockbackService();

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ParentShip playerShip =
            collision.collider.GetComponentInParent<ParentShip>();
        Rigidbody2D playerBody = collision.collider.attachedRigidbody;

        if (playerShip == null || playerBody == null)
            return;
        if (collision.collider.GetComponentInParent<TwinCloneController>() != null)
            return;

        if (!playerShip.TryTakeDamage(ParentShip.EnemyHitDamage))
            return;

        PlayerController controller = playerBody.GetComponent<PlayerController>();
        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (controller != null && controller.UsesDirectMovement)
        {
            Rigidbody2D enemyBody = GetComponent<Rigidbody2D>();
            Vector2 enemyVelocity = enemyBody != null ? enemyBody.linearVelocity : Vector2.zero;
            relativeSpeed = Mathf.Max(relativeSpeed, (controller.CurrentVelocity - enemyVelocity).magnitude);
        }
        controller?.ApplyControlLoss(playerShip, controlLockDuration);
        ApplyKnockback(
            playerShip,
            playerBody,
            relativeSpeed);
    }

    private void ApplyKnockback(
        ParentShip playerShip,
        Rigidbody2D playerBody,
        float relativeSpeed)
    {
        Vector2 direction =
            playerBody.worldCenterOfMass - (Vector2)transform.position;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        float impulse = KnockbackService.CalculateImpulse(
            playerShip.ShipData,
            relativeSpeed,
            CreateKnockbackSettings());

        PlayerController controller = playerBody.GetComponent<PlayerController>();
        if (controller != null)
            controller.ApplyKnockback(direction * impulse);
        else
            playerBody.AddForce(direction * impulse, ForceMode2D.Impulse);
    }

    private ShipKnockbackService.Settings CreateKnockbackSettings()
    {
        return new ShipKnockbackService.Settings
        {
            baseImpulse = baseImpulse,
            relativeSpeedMultiplier = relativeSpeedMultiplier,
            maximumImpulse = maximumImpulse,
            referenceMass = referenceMass,
            referenceSpeed = referenceSpeed,
            massInfluence = massInfluence,
            speedInfluence = speedInfluence,
            dragInfluence = dragInfluence
        };
    }

    private void OnValidate()
    {
        contactDamage = ParentShip.EnemyHitDamage;
        referenceMass = Mathf.Max(0.001f, referenceMass);
        referenceSpeed = Mathf.Max(0.001f, referenceSpeed);
    }
}
