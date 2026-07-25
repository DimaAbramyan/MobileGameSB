using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public sealed class EnemyContactImpact : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Min(0f)] private float contactDamage = 5f;
    [SerializeField, Min(0f)] private float impactCooldown = 0.5f;

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

    private float nextImpactTime;

    private ShipKnockbackService KnockbackService =>
        knockbackService ??= new ShipKnockbackService();

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < nextImpactTime)
            return;

        ParentShip playerShip =
            collision.collider.GetComponentInParent<ParentShip>();
        Rigidbody2D playerBody = collision.collider.attachedRigidbody;

        if (playerShip == null || playerBody == null)
            return;
        if (collision.collider.GetComponentInParent<TwinCloneController>() != null)
            return;

        nextImpactTime = Time.time + impactCooldown;

        playerShip.TakeDamage(contactDamage);
        playerBody.GetComponent<PlayerController>()
            ?.LockControls(controlLockDuration);
        ApplyKnockback(
            playerShip,
            playerBody,
            collision.relativeVelocity.magnitude);
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
        referenceMass = Mathf.Max(0.001f, referenceMass);
        referenceSpeed = Mathf.Max(0.001f, referenceSpeed);
    }
}
