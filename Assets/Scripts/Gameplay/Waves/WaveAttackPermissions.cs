using System.Collections.Generic;

public enum WaveAttackPermission { AllowAttack, BlockNewAttacks, InterruptAttacks }
public enum WaveAttackSwitchMode { FinishCurrentAttack, Interrupt }
public enum WaveAttackReactivation { Restart, Resume }

// Independent owners can only restrict each other. Releasing one owner's request
// never removes another system's block. No requests preserves legacy behaviour.
public sealed class WaveAttackPermissions
{
    private readonly Dictionary<object, WaveAttackPermission> requests = new();
    public WaveAttackPermission State { get; private set; }
    public bool AllowNewAttacks => State == WaveAttackPermission.AllowAttack;
    public bool Interrupted => State == WaveAttackPermission.InterruptAttacks;
    public void Set(object owner, WaveAttackPermission permission)
    {
        if (owner == null) throw new System.ArgumentNullException(nameof(owner));
        requests[owner] = permission;
        Refresh();
    }
    public void Release(object owner)
    { if (owner != null && requests.Remove(owner)) Refresh(); }
    private void Refresh()
    {
        State = WaveAttackPermission.AllowAttack;
        foreach (var value in requests.Values) if (value > State) State = value;
    }
}
