using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

// A release-time binding. Only translation follows the source; the released beam never rotates.
// Captured/random offsets and the layout displacement remain the values of the original shot.
public readonly struct EnemyLaserOrigin
{
    private readonly Transform source, player;
    private readonly Enemy enemy;
    private readonly IEnemyAttackAimTarget target;
    private readonly Entity entity;
    private readonly Vector3 sourceAtRelease, playerAtRelease;
    private readonly EnemyEmissionOriginSource x, y;
    private readonly bool boundaryX, boundaryY, upperX, upperY, hasSource, freezeTranslation;
    public readonly bool CancelWithSource;
    private readonly Rect boundsAtRelease;
    public readonly Camera Camera;

    public EnemyLaserOrigin(Transform source, Vector3 sourcePosition, EnemyEmissionOriginSettings settings,
        Camera camera = null, IEnemyAttackAimTarget target = null, Transform player = null, Entity entity = default,
        bool freezeTranslation = false, bool cancelWithSource = false)
    {
        this.freezeTranslation = freezeTranslation;
        this.source = source; this.player = player; this.target = target; this.entity = entity;
        enemy = source != null ? source.GetComponentInParent<Enemy>() : null;
        // Capture ownership now: Unity's destroyed-object null semantics must not
        // turn an enemy-owned beam into an independent beam after destruction.
        CancelWithSource = cancelWithSource || enemy != null;
        hasSource = source != null || entity != Entity.Null;
        sourceAtRelease = sourcePosition;
        playerAtRelease = player != null ? player.position : target != null ? target.AttackTargetPosition : sourcePosition;
        x = settings?.X.Source ?? EnemyEmissionOriginSource.Enemy; y = settings?.Y.Source ?? EnemyEmissionOriginSource.Enemy;
        boundaryX = settings?.X.RequiresBounds ?? false; boundaryY = settings?.Y.RequiresBounds ?? false;
        upperX = settings?.X.UsesUpperBoundary ?? false; upperY = settings?.Y.UsesUpperBoundary ?? false;
        Camera = camera != null ? camera : UnityEngine.Camera.main;
        EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera, sourcePosition.z, out boundsAtRelease);
    }
    public bool IsAlive(EntityManager manager) => !hasSource || (entity != Entity.Null
        ? manager.Exists(entity) && (!manager.HasComponent<EnemyProjectileResolution>(entity)
            || manager.GetComponentData<EnemyProjectileResolution>(entity).Kind == EnemyProjectileResolutionKind.None)
        : source != null && source.gameObject.activeInHierarchy && (enemy == null || !enemy.isDead));

    public Vector3 Translation(EntityManager manager, Rect bounds)
    {
        if (freezeTranslation) return Vector3.zero;
        Vector3 current = sourceAtRelease;
        if (entity != Entity.Null && manager.Exists(entity) && manager.HasComponent<LocalTransform>(entity))
        {
            var p = manager.GetComponentData<LocalTransform>(entity).Position; current = new Vector3(p.x, p.y, p.z);
        }
        else if (source != null) current = source.position;
        Vector3 targetPosition = player != null ? player.position : TargetAlive() ? target.AttackTargetPosition : playerAtRelease;
        Vector3 delta = current - sourceAtRelease, targetDelta = targetPosition - playerAtRelease;
        return new Vector3(Axis(x, delta.x, targetDelta.x, boundaryX,
                upperX ? bounds.xMax - boundsAtRelease.xMax : bounds.xMin - boundsAtRelease.xMin),
            Axis(y, delta.y, targetDelta.y, boundaryY,
                upperY ? bounds.yMax - boundsAtRelease.yMax : bounds.yMin - boundsAtRelease.yMin), delta.z);
    }
    private bool TargetAlive() => target != null && (!(target is Object obj) || obj != null);
    private static float Axis(EnemyEmissionOriginSource mode, float enemyDelta, float playerDelta, bool boundary, float boundaryDelta)
        => mode == EnemyEmissionOriginSource.Enemy ? enemyDelta : mode == EnemyEmissionOriginSource.Player ? playerDelta : boundary ? boundaryDelta : 0f;
}
