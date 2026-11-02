using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
public abstract class EnemyProjectile: MonoBehaviour
{
    [Inject] private GameSettings gameSettings;
    [InjectOptional] private EnemyProjectileCollisionRegistry projectileCollisionRegistry;
    protected float EffectiveSpeedMultiplier => SpeedMultiplier
        * (projectileCollisionRegistry != null
            ? projectileCollisionRegistry.EvaluateActiveSpeedMultiplier(transform.position) : 1f);
    protected float _start_pos;
    protected Vector3 _current_pos;
    protected float SpeedMultiplier = 1;
    [SerializeField] protected float Speed;
    [SerializeField] protected float _damage;
    private SpriteRenderer fragileRenderer;
    private Color originalColour;
    public EnemyProjectileCharacteristic Characteristic =>
        TryGetComponent(out EnemyProjectileAuthoring authoring)
            ? authoring.Characteristic : EnemyProjectileCharacteristic.Fragile;

    protected virtual void OnEnable()
    {
        if (Characteristic == EnemyProjectileCharacteristic.Fragile
            && TryGetComponent(out EnemyProjectileAuthoring authoring)
            && authoring.VisualRenderer != null)
        {
            fragileRenderer = authoring.VisualRenderer;
            originalColour = fragileRenderer.color;
            fragileRenderer.color = EnemyProjectileCharacteristics.FragileColour;
        }
    }

    protected virtual void OnDisable()
    {
        if (fragileRenderer == null) return;
        fragileRenderer.color = originalColour;
        fragileRenderer = null;
    }

    public bool TryDestroyByAbility(IEnemyProjectileDestructionAbility ability)
    {
        if (!ability.CanDestroyProjectile(Characteristic)) return false;
        return TryDestroyByAbility(
            ability.ProjectileDestructionPolicy == EnemyProjectileDestructionPolicy.NormalAndFragile);
    }

    public bool TryDestroyByAbility(bool destroysNormal = false)
    {
        if (!gameObject.activeInHierarchy
            || !EnemyProjectileCharacteristics.CanBeDestroyed(Characteristic, destroysNormal))
            return false;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    public void DestroyForSystemCleanup()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public virtual void TransformPosition(){}
    public virtual void DestroyProjByRange() { }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EnemyProjectileInterceptor interceptor =
            collision.collider.GetComponentInParent<EnemyProjectileInterceptor>();
        if (interceptor != null)
        {
            interceptor.TryDestroyEnemyProjectile(this);
            return;
        }

        ParentShip receiver =
            collision.collider.GetComponentInParent<ParentShip>();

        if (receiver != null && !gameSettings.IsGodModeOn)
        {
            receiver.TryTakeDamage(_damage);
        }

        if (Characteristic != EnemyProjectileCharacteristic.Indestructible) Destroy(gameObject);
    }
    public void SetMultiplier(float multiplier)
    { this.SpeedMultiplier = multiplier; }

    public void SetDamageMultiplier(float multiplier)
    {
        _damage *= Mathf.Max(0.01f, multiplier);
    }
}
