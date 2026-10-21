using UnityEngine;

// Observations from the existing Preview, never a second event scheduler.
public enum WavePreviewEventKind
{
    EnemySpawn, AttackStart, AttackEnd, ProjectileSpawn, PhaseChange, CoordinatorGroupChange,
    ChildAttackTrigger // Reserved for a future observer in the child runner.
}

public readonly struct WavePreviewEvent
{
    public readonly float Time;
    public readonly WavePreviewEventKind Kind;
    public readonly int Cue, Slot, Count;
    public readonly string Detail;
    public WavePreviewEvent(float time, WavePreviewEventKind kind, int cue = -1, int slot = -1,
        int count = 1, string detail = null)
    { Time = Mathf.Max(0f, time); Kind = kind; Cue = cue; Slot = slot; Count = count; Detail = detail; }
}
