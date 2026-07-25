using UnityEngine;
using Zenject;

public class EnemyTurretBullet : EnemyProjectile
{
    [Inject] private PlayerController playerController;
    private Rigidbody2D rb;
    private Transform target;
    private Vector2 direction;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        target = playerController.transform;

        // вычисляем направление один раз
        direction = (target.position - transform.position).normalized;

        Destroy(gameObject, 6f);
    }

    private void FixedUpdate()
    {
        MovePosition();
    }

    void MovePosition()
    {
        Vector2 newPosition =
            rb.position + direction * Speed * SpeedMultiplier * Time.fixedDeltaTime;

        rb.MovePosition(newPosition);
    }
}
