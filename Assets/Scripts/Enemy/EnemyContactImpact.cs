using UnityEngine;

public sealed class EnemyContactImpact : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Min(0f)] private float contactDamage = 5f;
    [SerializeField, Min(0f)] private float impactCooldown = 0.5f;

    [Header("Control")]
    [SerializeField, Min(0f)] private float controlLockDuration = 0.5f;

    [Header("Knockback")]
    [SerializeField, Min(0f)] private float baseImpulse = 30f;
    [SerializeField, Min(0f)] private float velocityMultiplier = 3.75f;
    [SerializeField, Min(0f)] private float maximumImpulse = 70f;

    private float nextImpactTime;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Time.time < nextImpactTime)
            return;

        ParentShip playerShip =
            collision.collider.GetComponentInParent<ParentShip>();
        Rigidbody2D playerBody = collision.collider.attachedRigidbody;

        if (playerShip == null || playerBody == null)
            return;

        nextImpactTime = Time.time + impactCooldown;

        playerShip.TakeDamage(contactDamage);
        playerBody.GetComponent<PlayerController>()
            ?.LockControls(controlLockDuration);
        ApplyKnockback(playerBody, collision.relativeVelocity.magnitude);
    }

    private void ApplyKnockback(Rigidbody2D playerBody, float relativeSpeed)
    {
        Vector2 direction =
            playerBody.worldCenterOfMass - (Vector2)transform.position;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        direction.Normalize();

        float impulse = Mathf.Clamp(
            baseImpulse + relativeSpeed * velocityMultiplier,
            0f,
            maximumImpulse);

        playerBody.AddForce(direction * impulse, ForceMode2D.Impulse);
    }
}
