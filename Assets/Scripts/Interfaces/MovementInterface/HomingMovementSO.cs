using UnityEngine;

[CreateAssetMenu(menuName = "Movement/Homing")]
public class HomingMovementSO : MovementStrategySO
{
    public float rotationSpeed = 360f;
    Vector3 _direction;
    float _speed;
    Enemy target = null;
    public override void Move(Projectile projectile)
    {
        if (target == null)
        {
            target = EnemyManager.instance.FindNearestEnemy(projectile.transform.position);
        }
        _direction = projectile.direction;
        _speed = projectile.speed;
        if (target == null || !target.gameObject.activeSelf)
        {
            projectile.transform.position += _direction * _speed * Time.deltaTime;
            return;
        }

        Vector2 dir = target.transform.position - projectile.transform.position;

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90;

        Quaternion targetRot = Quaternion.Euler(0, 0, targetAngle);

        projectile.transform.rotation = Quaternion.RotateTowards(
            projectile.transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );

        projectile.transform.position += projectile.transform.up * _speed * Time.deltaTime;
    }
}