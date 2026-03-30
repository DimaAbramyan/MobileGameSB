using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public abstract class EnemyProjectile: MonoBehaviour
{
    protected float _start_pos;
    protected Vector3 _current_pos;
    protected float SpeedMultiplier = 1;
    [SerializeField] protected float Speed;
    [SerializeField] protected float _damage;

    public virtual void TransformPosition(){}
    public virtual void DestroyProjByRange() { }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ParentShip receiver = collision.collider.GetComponent<ParentShip>();
        Parameters param = FindAnyObjectByType<Parameters>();

        if (receiver != null && !param.IsGodModeOn)
        {
            receiver.TakeDamage(_damage);
        }

        Destroy(gameObject);
    }
    public void SetMultiplier(float multiplier)
    { this.SpeedMultiplier = multiplier; }
}