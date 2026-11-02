using UnityEditor;
using UnityEngine;
using Unity.Mathematics;

public struct EnemyCompositeSilhouette
{
    public EnemyCompositeProjectileSpec Spec;
    public float ComponentRadius, Radius, Angle;
    public Vector3 ComponentPosition(Vector3 center, int index)
    {
        float2 offset = EnemyCompositeProjectileSpec.Rotate(Spec.Offset(index, Radius), Angle);
        return center + new Vector3(offset.x, offset.y, 0f);
    }
}

public static partial class EnemyShootingPreview
{
    private static EnemyBullet compositePreviewPrefab;
    private const int CompositeSilhouetteComponentBudget = 8192;
    public static bool ShowAllCompositeSilhouettes
    {
        get => SessionState.GetBool("EnemyShootingPreview.ShowAllCompositeSilhouettes", false);
        set { SessionState.SetBool("EnemyShootingPreview.ShowAllCompositeSilhouettes", value); SceneView.RepaintAll(); }
    }

    // Lower middle for even counts, first Stack, first angular emitter: stable through repaints/seed changes.
    public static int RepresentativeCompositeEmitter(EnemyShootingSettings shooting)
        => shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Sector ? (shooting.DirectionCount - 1) / 2 : 0;
    public static int RepresentativeCompositeProjectile(EnemyShootingSettings shooting)
        => shooting.UsesFan ? (shooting.FanProjectileCount - 1) / 2 : 0;

    public static bool TryGetCompositeSilhouette(EnemyShootingSettings shooting, EnemyBullet fallback,
        Vector3 direction, out EnemyCompositeSilhouette silhouette, EnemyNumericSample numeric = default)
    {
        silhouette = default;
        if (!shooting.IsComposite) return false;
        var spec = shooting.Composite.Resolve(shooting.ProjectileType, numeric);
        var prefab = shooting.ResolveProjectilePrefab(fallback);
        var authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        if (!spec.OverrideHitRadius && authoring == null) return false;
        float componentRadius = (spec.OverrideHitRadius ? spec.HitRadius : authoring.EffectiveRadius) * spec.ComponentScale;
        silhouette = new EnemyCompositeSilhouette { Spec = spec, ComponentRadius = componentRadius,
            Radius = spec.ResolveRadius(componentRadius),
            Angle = spec.Rotation(0f, new float2(direction.x, direction.y)) };
        return true;
    }

    private static void DrawCompositeSilhouettes(EnemyShootingSettings shooting, EnemyEmissionPreviewFrame frame,
        Quaternion basis, float time, EnemyRotationSettings rotation, Vector3? direction,
        bool aimed, bool stationary, bool detailed, int rayBudget)
    {
        bool all = ShowAllCompositeSilhouettes;
        int directions = Mathf.Min(shooting.DirectionCount, Mathf.Min(MaxPreviewDirections, Mathf.Max(1, rayBudget)));
        int perDirection = Mathf.Max(1, rayBudget / directions), componentsLeft = CompositeSilhouetteComponentBudget;
        bool limited = false;
        // Resolve prefab radius once for the whole volley, not for every center or component.
        bool hasGeometry = TryGetCompositeSilhouette(shooting, compositePreviewPrefab, Vector3.up, out var template);
        for (int sample = 0; sample < directions; sample++)
        {
            int arm = SampleIndex(sample, directions, shooting.DirectionCount);
            var numeric = frame.NumericSpeed.Numeric.ForDirection(arm);
            var geometry = shooting.SampleGeometry(numeric, true);
            var emitter = EnemyShootingPreviewGeometry.GetEmitter(geometry, arm, frame, basis, time, rotation, direction, aimed, stationary);
            int unique = geometry.ProjectilesPerDirection / EnemyShootingPreviewGeometry.CoincidentStackCount(geometry);
            int samples = Mathf.Min(unique, perDirection);
            for (int member = 0; member < samples; member++)
            {
                int index = SampleIndex(member, samples, unique);
                var projectile = EnemyShootingPreviewGeometry.GetProjectile(geometry, emitter, index, numeric.ForProjectile(index));
                Handles.color = ProjectileColor;
                Handles.DrawSolidDisc(projectile.Position, Vector3.forward, .035f);
                if (all && hasGeometry)
                {
                    if (componentsLeft < template.Spec.ComponentCount) { limited = true; continue; }
                    var shape = template;
                    shape.Spec = geometry.Composite.Resolve(geometry.ProjectileType, numeric.ForProjectile(index));
                    shape.Angle = shape.Spec.Rotation(0f, new float2(projectile.Direction.x, projectile.Direction.y));
                    DrawCompositeSilhouette(shape, projectile.Position);
                    componentsLeft -= shape.Spec.ComponentCount;
                }
            }
        }
        // The complete representative is calculated explicitly, so ray-budget sampling cannot omit it.
        // A From/To diagram has one full representative at To; From centers/directions remain visible.
        if (!all && hasGeometry && (!drawingRotationPose || !drawingRotationFrom))
        {
            int arm = RepresentativeCompositeEmitter(shooting);
            var numeric = frame.NumericSpeed.Numeric.ForDirection(arm);
            var geometry = shooting.SampleGeometry(numeric, true);
            var emitter = EnemyShootingPreviewGeometry.GetEmitter(geometry, arm, frame, basis, time, rotation, direction, aimed, stationary);
            int index = RepresentativeCompositeProjectile(geometry);
            var projectile = EnemyShootingPreviewGeometry.GetProjectile(geometry, emitter, index, numeric.ForProjectile(index));
            template.Spec = geometry.Composite.Resolve(geometry.ProjectileType, numeric.ForProjectile(index));
            template.Angle = template.Spec.Rotation(0f, new float2(projectile.Direction.x, projectile.Direction.y));
            DrawCompositeSilhouette(template, projectile.Position);
        }
        if (detailed && !drawingRotationFrom)
        {
            Vector3 origin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame);
            if (!hasGeometry) Handles.Label(origin + Vector3.down * .6f, "Assign Component Prefab or Override Hit Radius to preview the shape.");
            else if (limited) Handles.Label(origin + Vector3.down * .6f, "Dense composite volley: silhouette component budget reached; remaining centers shown.");
        }
    }

    private static void DrawCompositeSilhouette(EnemyCompositeSilhouette shape, Vector3 center)
    {
        Color color = ProjectileColor;
        for (int component = 0; component < shape.Spec.ComponentCount; component++)
        {
            Vector3 position = shape.ComponentPosition(center, component);
            Handles.color = new Color(color.r, color.g, color.b, color.a * .12f);
            Handles.DrawSolidDisc(position, Vector3.forward, shape.ComponentRadius);
            Handles.color = color;
            Handles.DrawWireDisc(position, Vector3.forward, shape.ComponentRadius);
        }
    }
}
