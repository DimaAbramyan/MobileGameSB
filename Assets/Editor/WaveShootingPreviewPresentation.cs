using UnityEngine;

public sealed partial class WaveShootingPreviewSession
{
    // Shared by entrance/sequence profiles of one enemy. These are presentation inputs,
    // captured by the existing snapshot graph along with their owning profiles.
    private sealed class VisualPose
    {
        public readonly Transform Target;
        public readonly Quaternion InitialRotation;
        public Vector3 Position;
        public bool Visible, RequiresLiveSource;
        public Slot FacingSlot;
        public float FacingElapsed;
        public Vector3? FacingAim;
        public int LayoutAttacks, LayoutPrograms;
        public bool LayoutRunning, LayoutProgramHasAttack;
        public float LayoutElapsed, LayoutStamp;
        public EnemyLayoutRotationOnAttackFinished LayoutFinish;
        public float LayoutTime(float time) => LayoutElapsed + (LayoutRunning ? Mathf.Max(0f, time - LayoutStamp) : 0f);
        public void BeginLayout(EnemyLayoutRotationOnAttackFinished mode, float time)
        {
            LayoutElapsed = LayoutTime(time); LayoutStamp = time;
            if (LayoutAttacks == 0 && (LayoutPrograms == 0 || !LayoutProgramHasAttack)) LayoutFinish = mode;
            else if (mode > LayoutFinish) LayoutFinish = mode;
            LayoutAttacks++; LayoutProgramHasAttack = true; LayoutRunning = true;
        }
        public void EndLayout(float time, bool program = false)
        {
            LayoutElapsed = LayoutTime(time); LayoutStamp = time;
            if (program) LayoutPrograms = Mathf.Max(0, LayoutPrograms - 1);
            else LayoutAttacks = Mathf.Max(0, LayoutAttacks - 1);
            if (LayoutAttacks > 0 || LayoutPrograms > 0 || LayoutFinish == EnemyLayoutRotationOnAttackFinished.ContinueRotation) return;
            LayoutRunning = false;
            if (LayoutFinish == EnemyLayoutRotationOnAttackFinished.ResetRotation) LayoutElapsed = 0f;
        }

        public VisualPose(Transform target)
        { Target = target; Position = target.position; InitialRotation = target.rotation; Visible = target.gameObject.activeSelf; }

        public Quaternion Rotation
        {
            get
            {
                if (FacingSlot == null) return InitialRotation;
                var slot = FacingSlot;
                int facing = slot.Profile.FacingPatternIndex;
                var pattern = slot.Patterns[facing >= 0 ? facing : 0];
                var guide = EnemyShootingPreviewGeometry.GetFacingGuide(slot.Profile, slot.Executor.transform.rotation,
                    pattern.Basis, FacingElapsed, slot.RotationOverride, FacingAim);
                return Quaternion.FromToRotation(Vector3.up, guide.Direction);
            }
        }
        public void UpdateAnchor()
        {
            if (Target.gameObject.activeSelf != Visible) Target.gameObject.SetActive(Visible);
            Target.position = Position;
        }
        public void Publish()
        {
            UpdateAnchor();
            Target.rotation = Rotation;
        }
    }

    // Consume published transforms, including the last stable presentation during Fast Seek.
    // Existing visible renderers already provide the silhouette: do not draw them twice.
    internal void DrawEnemySilhouettes()
    {
        foreach (var renderer in visualRenderers)
            if (renderer is SpriteRenderer sprite && WaveEnemySilhouette.NeedsOverlay(sprite))
                WaveEnemySilhouette.Draw(sprite, sprite.localToWorldMatrix);
    }

    internal void PublishVisuals()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.PublishVisuals);
        foreach (var pose in visualPoses.Values) pose.Publish();
    }
}
