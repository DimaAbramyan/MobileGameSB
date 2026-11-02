using System;

[Flags]
public enum PlayerActions { None = 0, Movement = 1, ShipSwitch = 2, Ability = 4 }

// Scoped restrictions compose with damage/control-loss timers and with each other.
// No knowledge of UI, tutorials or any other caller belongs here.
public sealed class PlayerActionLocks
{
    private int movement, switching, ability;
    public bool Blocks(PlayerActions actions)
        => (actions & PlayerActions.Movement) != 0 && movement > 0
        || (actions & PlayerActions.ShipSwitch) != 0 && switching > 0
        || (actions & PlayerActions.Ability) != 0 && ability > 0;
    public IDisposable Acquire(PlayerActions actions)
    {
        Change(actions, 1);
        return new Lease(this, actions);
    }
    private void Change(PlayerActions actions, int delta)
    {
        if ((actions & PlayerActions.Movement) != 0) movement += delta;
        if ((actions & PlayerActions.ShipSwitch) != 0) switching += delta;
        if ((actions & PlayerActions.Ability) != 0) ability += delta;
    }
    private sealed class Lease : IDisposable
    {
        private PlayerActionLocks owner;
        private readonly PlayerActions actions;
        public Lease(PlayerActionLocks owner, PlayerActions actions) { this.owner = owner; this.actions = actions; }
        public void Dispose() { owner?.Change(actions, -1); owner = null; }
    }
}
