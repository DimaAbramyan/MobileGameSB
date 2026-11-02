using UnityEngine;

public sealed partial class EnemyChildAttackRunner
{
    private int boundaryObservers;
    private Camera boundaryCamera;
    private Rect boundaryBounds;
    private float boundaryBoundsZ;
    private bool boundaryBoundsInitialized, hasBoundaryBounds;

    private bool TryBoundaryBounds(float z, out Rect bounds)
    {
        if (!boundaryBoundsInitialized || boundaryBoundsZ != z)
        {
            boundaryBoundsInitialized = true;
            boundaryBoundsZ = z;
            hasBoundaryBounds = EnemyEmissionOriginSettings.TryGetGameplayBounds(boundaryCamera, z, out boundaryBounds);
        }
        bounds = boundaryBounds;
        return hasBoundaryBounds;
    }

    private void UpdateBoundaryObserver(ParentState state, float time, float delta, bool preview)
    {
        if (!manager.Exists(state.Parent)) { state.Complete = true; return; }
        var resolution = manager.GetComponentData<EnemyProjectileResolution>(state.Parent);
        // Natural expiry on the crossing frame can still be owned by Destroy Parent, as for other triggers.
        if (resolution.Kind != EnemyProjectileResolutionKind.None
            && !(state.Settings.DestroysParentOnCompletion && resolution.LifetimeExpired != 0))
        { state.Complete = true; return; }

        RefreshParentPose(state);
        float fraction = 0f;
        Vector3 reachedPosition = default;
        bool crossed = false;
        if ((!state.Settings.TriggerOnce || state.BoundaryEvents == 0) && TryBoundaryBounds(state.Position.z, out Rect bounds))
        {
            var screen=manager.GetComponentData<EnemyProjectileScreenState>(state.Parent);
            crossed=EnemyProjectileTargetPositionUtility.TryBoundaryContact(state.Previous,state.Position,screen,bounds,
                state.Settings.TriggerBoundaries,state.Settings.BoundaryOffset,out fraction,out reachedPosition);
        }
        if (crossed)
        {
            // An occurrence is ordinary scheduled child work. It shares lineage/caps/emission code,
            // but owns its delay and captured pose so overlapping crossings cannot overwrite each other.
            float reachedAt = time - Mathf.Max(0f, delta) * (1f - fraction);
            StartBoundaryAttack(state, reachedPosition, reachedAt + state.Settings.TimeOffset, time, delta, preview);
        }
        if(!crossed && EnemyProjectileTargetPositionUtility.IsHidden(manager,state.Parent) && state.ActiveBoundaryAttacks==0)
            state.Complete=true;
        state.Previous = state.Position;
        if (resolution.Kind != EnemyProjectileResolutionKind.None && state.BoundaryEvents == 0)
            state.Complete = true;
    }

    private void StartBoundaryAttack(ParentState owner, Vector3 crossing, float start, float time, float delta, bool preview)
    {
        if (parents.Count >= EnemyChildAttackSettings.MaximumTrackedParents || owner.Budget.Remaining <= 0)
        { Limit(1); return; }
        owner.BoundaryEvents++;
        if (owner.Settings.DestroysParentOnCompletion)
        {
            manager.SetComponentData(owner.Parent, default(EnemyProjectileResolution));
            manager.SetComponentEnabled<EnemyChildAttackLifetimeHold>(owner.Parent, true);
        }
        var attack = new ParentState
        {
            Parent = owner.Parent, Settings = owner.Settings, Prefabs = owner.Prefabs, Budget = owner.Budget,
            Completion = owner.Completion, Generation = owner.Generation, DamageMultiplier = owner.DamageMultiplier,
            SectorOrders = owner.Settings.Pattern.Shooting.EmissionLayout.UsesPingPong
                && !owner.Settings.Pattern.Shooting.EmissionLayout.ResetOrderOnAttackStart
                ? owner.SectorOrders ??= new EnemySectorLaunchOrderRegistry() : null,
            BoundaryOwner = owner, Started = true, Birth = owner.Birth,
            Position = owner.Position, Previous = owner.Previous, Forward = owner.Forward,
            CapturedOrigin = owner.Settings.ResolveOrigin(crossing, owner.Forward), CapturedForward = owner.Forward,
            Numeric = new EnemyAttackNumericState(owner.Settings.Pattern.Shooting.NumericRandomSeed,
                unchecked(owner.NumericKey + owner.BoundaryEvents * 486187739), (uint)(owner.Generation + 1))
        };
        owner.ActiveBoundaryAttacks++;
        attack.Completion?.BeginChild();
        BeginAttack(attack, start);
        parents.Add(attack);
        // Time Offset = 0 emits this step, before cleanup, without recursively ticking new descendants.
        Update(attack, time, preview, delta);
    }

    private void FinishBoundaryObservation(ParentState owner)
    {
        if (owner.ActiveBoundaryAttacks > 0 || owner.BoundaryEvents == 0) return;
        if (owner.Settings.DestroysParentOnCompletion && manager.Exists(owner.Parent)
            && manager.GetComponentData<EnemyProjectileResolution>(owner.Parent).Kind == EnemyProjectileResolutionKind.None)
            manager.SetComponentData(owner.Parent, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
        if (owner.Settings.TriggerOnce || owner.Settings.DestroysParentOnCompletion || owner.Budget.Remaining <= 0
            || EnemyProjectileTargetPositionUtility.IsHidden(manager,owner.Parent))
            owner.Complete = true;
    }
}
