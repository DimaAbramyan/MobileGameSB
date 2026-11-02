using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed partial class DirectedWaveAttackController
{
    private sealed class GroupProgram
    {
        public DirectedWaveAttackSequencePlayer Player;
        public DirectedWaveAttackSequenceRuntime Runtime;
        public bool Running;
        public string GroupId;
        public int Revision;
    }
    private readonly Dictionary<Enemy, GroupProgram> groupPrograms = new();
    private int groupSettingsRevision = int.MinValue;
    private DirectedWaveAttackGroup GroupFor(Enemy enemy)
        => useGroupConfigurations && wave.AttackPatternBehaviour != null && wave.AttackPatternBehaviour.UseAttackGroups
            && wave.TryGetFormationIndex(enemy, out int slot) ? wave.AttackPatternBehaviour.GetAttackGroupForSlot(slot) : null;
    private bool IsGroupSequence(Enemy enemy) => GroupFor(enemy)?.UsesSequence == true;
    private DirectedWaveAttackSettings ShootingSettingsFor(Enemy enemy)
    {
        if (!useGroupConfigurations || wave.AttackPatternBehaviour == null || !wave.AttackPatternBehaviour.UseAttackGroups) return settings;
        int revision = wave.AttackPatternBehaviour.ConfigurationRevision;
        if (revision != groupSettingsRevision)
        { projectileBaseSpeedOverrideSettings.Clear(); groupSettingsRevision = revision; }
        return wave.TryGetFormationIndex(enemy, out int slot)
            && wave.AttackPatternBehaviour.TryGetGroupAttackSettings(slot, out var grouped) && grouped != null ? grouped : settings;
    }
    private IEnumerator FireConfiguredAttack(Enemy enemy, IWaveAttackExecutor executor, EnemyBurstAttackSettings profile)
        => IsGroupSequence(enemy) ? PlayGroupSequence(enemy, executor) : FireFullAttack(enemy, executor, profile);
    private IEnumerator RunGroupSequence(Enemy enemy, IWaveAttackExecutor executor)
    {
        yield return PlayGroupSequence(enemy, executor);
        CompleteSequentialAttack(enemy); CompleteFormationBurst(enemy);
    }
    private IEnumerator PlayGroupSequence(Enemy enemy, IWaveAttackExecutor executor)
    {
        var group = GroupFor(enemy);
        if (group == null || !wave.TryGetFormationIndex(enemy, out int slot)) yield break;
        int revision = wave.AttackPatternBehaviour.ConfigurationRevision;
        if (!groupPrograms.TryGetValue(enemy, out var program) || program.GroupId != group.Id || program.Revision != revision)
        {
            program?.Player.Stop(); program?.Runtime.ReleaseControl();
            var runtime = new DirectedWaveAttackSequenceRuntime(wave, wave.AttackPatternBehaviour, slot);
            program = new GroupProgram { Runtime = runtime, Player = new DirectedWaveAttackSequencePlayer(group.Sequence, runtime), GroupId = group.Id, Revision = revision };
            groupPrograms[enemy] = program;
        }
        program.Player.Begin(enemy.AttackTime, group.Sequence.restartOnReentry);
        program.Running = true;
        while (enemy != null && !enemy.isDead && enemy.isActiveAndEnabled)
        {
            if (GroupFor(enemy)?.Id != program.GroupId || wave.AttackPatternBehaviour.ConfigurationRevision != program.Revision)
            { program.Player.Stop(); break; }
            if (wave.AttackPatternBehaviour.AttackPermissions.Interrupted) { yield return null; continue; }
            // An outer dispatch already granted this program. FinishCurrent may
            // block new dispatches, but must let its remaining steps finish.
            program.Player.AllowNewAttacks = true;
            program.Player.Tick(enemy.AttackTime);
            executor?.SetWaveAttackControl(true);
            if (program.Player.IsComplete) break;
            yield return null;
        }
        if (enemy != null)
        {
            nextAttackReadyTimes[enemy] = enemy.AttackTime;
            if (!enemy.isDead) executor?.SetWaveAttackControl(true);
        }
        program.Running = false;
        program.Runtime.CompleteLayoutRotationProgram();
    }
    private void PauseGroupPrograms()
    {
        foreach (var pair in groupPrograms)
        {
            pair.Value.Player.Pause(pair.Key != null ? pair.Key.AttackTime : Time.time);
            pair.Value.Running = false;
            pair.Value.Runtime.ReleaseControl();
        }
    }
    private void RemoveGroupProgram(Enemy enemy)
    {
        if (ReferenceEquals(enemy, null) || !groupPrograms.TryGetValue(enemy, out var program)) return;
        program.Player.Stop(); program.Runtime.NotifyEnemyDestroyed(enemy); program.Runtime.ReleaseControl(); groupPrograms.Remove(enemy);
    }
    private bool HasRunningGroupProgram(Enemy enemy)
        => groupPrograms.TryGetValue(enemy, out var program) && program.Running;
}
