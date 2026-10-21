// Tracks child attack scheduling, not the lifetime of ordinary bullets or visual effects.
public sealed class EnemyAttackCompletionTracker
{
    public int PendingChildAttacks { get; private set; }
    public bool IsComplete => PendingChildAttacks == 0;
    public void BeginChild() => PendingChildAttacks++;
    public void EndChild() { if (PendingChildAttacks > 0) PendingChildAttacks--; }
}

public interface IEnemyAttackCompletionReceiver
{
    void SetAttackCompletionTracker(EnemyAttackCompletionTracker tracker);
}
