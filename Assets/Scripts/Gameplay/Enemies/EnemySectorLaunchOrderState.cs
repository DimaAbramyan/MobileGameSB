using System.Collections.Generic;

// Owned by a physical enemy (or preview slot/child parent), never by shared authored settings.
public sealed class EnemySectorLaunchOrderRegistry
{
    private readonly Dictionary<(object Source, int Index), EnemySectorLaunchOrderState> states = new();
    private EnemySectorLaunchOrderState Get(EnemyEmissionLayoutSettings layout)
    {
        if (!states.TryGetValue(layout.OrderKey, out var state))
        { state = new EnemySectorLaunchOrderState(); states.Add(layout.OrderKey, state); }
        return state;
    }
    public void BeginAttack(EnemyEmissionLayoutSettings layout)
    {
        if (layout.UsesPingPong) Get(layout).BeginAttack(layout);
    }
    public EnemySectorLaunchTicket Reserve(EnemyEmissionLayoutSettings layout)
        => layout.UsesPingPong ? Get(layout).Reserve(layout) : new EnemySectorLaunchTicket(layout.ReverseOrder);
    public void Clear() => states.Clear();

    // Runtime copies keep the identity of their authored pattern; distinct users of one preset
    // bind their own source here. No serialized ids or state are added to assets.
    public static void BindSource(EnemyBurstAttackSettings settings, object source)
    {
        for (int i = 0; i < settings.PatternCount; i++) settings.GetPattern(i).Shooting.EmissionLayout.BindOrderSource(source, i);
    }
}

internal sealed class EnemySectorLaunchOrderState
{
    private bool initialized, nextReverse;
    private uint epoch;
    private int pending;
    public void BeginAttack(EnemyEmissionLayoutSettings layout)
    {
        if (initialized && !layout.ResetOrderOnAttackStart) return;
        initialized = true; nextReverse = layout.InitialDirection == EnemySectorInitialDirection.Reverse;
        epoch++; pending = 0;
    }
    public EnemySectorLaunchTicket Reserve(EnemyEmissionLayoutSettings layout)
    {
        if (!initialized) BeginAttack(layout);
        // Overlapping sectors reserve alternating directions, but only completed sectors
        // commit the turn. Cancelling/resetting pending work cannot cause an extra flip.
        bool reverse = nextReverse ^ ((pending & 1) != 0);
        pending++;
        return new EnemySectorLaunchTicket(this, epoch, reverse);
    }
    public void Finish(uint ticketEpoch, bool completed)
    {
        if (ticketEpoch != epoch) return;
        if (pending > 0) pending--;
        if (completed) nextReverse = !nextReverse;
    }
}

public struct EnemySectorLaunchTicket
{
    private EnemySectorLaunchOrderState state;
    private uint epoch;
    public bool Reversed { get; private set; }
    internal EnemySectorLaunchTicket(bool reverse) { state = null; epoch = 0; Reversed = reverse; }
    internal EnemySectorLaunchTicket(EnemySectorLaunchOrderState state, uint epoch, bool reverse)
    { this.state = state; this.epoch = epoch; Reversed = reverse; }
    public int GetLaunchIndex(EnemyEmissionLayoutSettings layout, int order) => layout.GetLaunchIndex(order, Reversed);
    public void Complete() { state?.Finish(epoch, true); state = null; }
    public void Cancel() { state?.Finish(epoch, false); state = null; }
}
