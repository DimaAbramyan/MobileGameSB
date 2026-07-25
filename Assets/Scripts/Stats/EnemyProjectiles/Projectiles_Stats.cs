using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
public abstract class EnemyProjectile: MonoBehaviour
{
    [Inject] private GameSettings gameSettings;
    protected float _start_pos;
    protected Vector3 _current_pos;
    protected float SpeedMultiplier = 1;
    [SerializeField] protected float Speed;
    [SerializeField] protected float _damage;

    public virtual void TransformPosition(){}
    public virtual void DestroyProjByRange() { }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ParentShip receiver =
            collision.collider.GetComponentInParent<ParentShip>();

        if (receiver != null && !gameSettings.IsGodModeOn)
        {
            receiver.TakeDamage(_damage);
        }

        Destroy(gameObject);
    }
    public void SetMultiplier(float multiplier)
    { this.SpeedMultiplier = multiplier; }
}
