using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyProjectileAuthoring : MonoBehaviour
{
    [Header("Legacy Source")]
    [SerializeField] private EnemyBullet legacyProjectile;
    [SerializeField] private SpriteRenderer visualRenderer;

      [Header("Static Projectile Data")]
      [SerializeField, Min(0f)] private float baseDamage = 1f;
      [SerializeField, Min(0.001f)] private float radius = 0.1f;
      [SerializeField, Min(0.01f)] private float baseLifetime = 1f;
      [SerializeField, Min(0f)] private float baseSpeed = 1f;

      [Header("ECS Presentation")]
      [SerializeField] private Vector2 visualScale = Vector2.one;
      [SerializeField, Min(0.01f)] private float collisionScale = 1f;

    public EnemyBullet LegacyProjectile => legacyProjectile;
    public SpriteRenderer VisualRenderer => visualRenderer;
    public float BaseDamage => baseDamage;
      public float Radius => radius;
      public float BaseLifetime => baseLifetime;
      public float BaseSpeed => baseSpeed;
      public Vector2 VisualScale => visualScale;
      public float CollisionScale => collisionScale;
      public float EffectiveRadius => radius * collisionScale;

    public bool IsConfigured => visualRenderer != null
        && visualRenderer.sprite != null
        && radius > 0f
        && baseLifetime > 0f;

    private void Reset()
    {
        legacyProjectile = GetComponent<EnemyBullet>();
        visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnValidate()
    {
        if (legacyProjectile == null)
            legacyProjectile = GetComponent<EnemyBullet>();

        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<SpriteRenderer>(true);

          baseDamage = Mathf.Max(0f, baseDamage);
          radius = Mathf.Max(0.001f, radius);
          baseLifetime = Mathf.Max(0.01f, baseLifetime);
          baseSpeed = Mathf.Max(0f, baseSpeed);
          visualScale.x = Mathf.Max(0.01f, visualScale.x);
          visualScale.y = Mathf.Max(0.01f, visualScale.y);
          collisionScale = Mathf.Max(0.01f, collisionScale);
    }

    private sealed class Baker : Baker<EnemyProjectileAuthoring>
    {
        public override void Bake(EnemyProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EnemyProjectileStaticData
            {
                  BaseDamage = authoring.baseDamage,
                  Radius = authoring.EffectiveRadius,
                BaseLifetime = authoring.baseLifetime,
                BaseSpeed = authoring.baseSpeed
            });

            Vector2 spriteSize = authoring.visualRenderer != null
                && authoring.visualRenderer.sprite != null
                ? authoring.visualRenderer.sprite.bounds.size
                : Vector2.one;
            AddComponent(entity, new EnemyProjectileVisualStaticData
            {
                Size = new float2(spriteSize.x, spriteSize.y)
            });
        }
    }
}
