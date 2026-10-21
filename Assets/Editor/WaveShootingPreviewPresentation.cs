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

    internal void PublishVisuals()
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.PublishVisuals);
        foreach (var pose in visualPoses.Values) pose.Publish();
    }
}
