using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBullet : EnemyProjectile
{
    [SerializeField] private float _MaxRange;
    [SerializeField] protected float _speed;
    public Vector3 _position = new Vector3(0, -1, 0);

    // Update is called once per frame
    void FixedUpdate()
    {
        TransformPosition();
    }
    public override void DestroyProjByRange()
    {
        if (Mathf.Abs(transform.position.y - _start_pos) >= _MaxRange)
        {
            Destroy(gameObject);
        }
    }
    public override void TransformPosition()
    {

        transform.position += _position * this._speed * Time.deltaTime;

        DestroyProjByRange();
    }

    public void Launch(Vector3 direction)
    {
        _position = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.down;
        _start_pos = transform.position.y;
    }
}
