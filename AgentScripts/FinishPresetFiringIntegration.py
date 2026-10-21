from pathlib import Path

p=Path('Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceAttackController.cs');s=p.read_text(encoding='utf-8-sig')
needle='        DirectedWaveAttackFireMode fireMode = GetFireMode(enemy);'
assert needle in s
s=s.replace(needle,'''        if (!TryGetExecutor(enemy, out IWaveAttackExecutor executor)) return false;
        EnemyBurstAttackSettings burstSettings = GetBurstSettings(enemy, executor);
        var presetFiring = burstSettings.GetPattern(0).Shooting.PresetFiring;
        DirectedWaveAttackFireMode fireMode = GetFireMode(enemy, burstSettings);''')
s=s.replace('''        if (!TryGetExecutor(enemy, out IWaveAttackExecutor executor))
            return false;

        EnemyBurstAttackSettings burstSettings = GetBurstSettings(enemy, executor);
        Vector3 forward''','''        Vector3 forward''')
s=s.replace('private DirectedWaveAttackFireMode GetFireMode(Enemy enemy)', 'private DirectedWaveAttackFireMode GetFireMode(Enemy enemy, EnemyBurstAttackSettings profile = null)')
s=s.replace('''        return enemyOverride != null
            ? enemyOverride.ResolveFireMode(sharedAttackSettings.FireMode)
            : sharedAttackSettings.FireMode;''','''        var firing = profile != null && profile.PatternCount > 0 ? profile.GetPattern(0).Shooting.PresetFiring : null;
        var fallback = firing != null && firing.enabled ? firing.fireMode : sharedAttackSettings.FireMode;
        return enemyOverride != null ? enemyOverride.ResolveFireMode(fallback) : fallback;''')
s=s.replace('''            var stationary = local != null ? local.ResolveStationaryFireSettings(sharedAttackSettings.StationaryFireSettings) : sharedAttackSettings.StationaryFireSettings;''','''            var fallback = presetFiring.enabled ? presetFiring.stationaryFireSettings : sharedAttackSettings.StationaryFireSettings;
            var stationary = local != null ? local.ResolveStationaryFireSettings(fallback) : fallback;''')
s=s.replace('''        DirectedWaveAttackAimMode aimMode = enemyOverride != null
            ? enemyOverride.ResolveAimMode(sharedAttackSettings.AimMode) : sharedAttackSettings.AimMode;''','''        var fallbackAim = presetFiring.enabled ? presetFiring.aimMode : sharedAttackSettings.AimMode;
        DirectedWaveAttackAimMode aimMode = enemyOverride != null
            ? enemyOverride.ResolveAimMode(fallbackAim) : fallbackAim;''')
p.write_text(s,encoding='utf-8')

p=Path('Assets/Scripts/Gameplay/Enemies/SimpleEnemyAttackExecutor.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('TryFireInDirection(AttackForward, burstAttackSettings);', 'TryFireAutonomousPattern(burstAttackSettings, !burstAttackSettings.RepeatBurst || remainingBurstShots == burstAttackSettings.BurstShotCount);')
s=s.replace('TryFireInDirection(AttackForward, pattern);', 'TryFireAutonomousPattern(pattern, schedule.IsBurstStart);')
needle='    private void ResetAttackSchedule()'
s=s.replace(needle,'''    private bool TryFireAutonomousPattern(EnemyBurstAttackSettings pattern, bool burstStart)
    {
        var firing = pattern.Shooting.PresetFiring;
        if (!firing.enabled) return TryFireInDirection(AttackForward, pattern);
        if (firing.fireMode == DirectedWaveAttackFireMode.None) return false;
        if (firing.fireMode == DirectedWaveAttackFireMode.Stationary)
            return firing.stationaryFireSettings.TryFire(this, pattern, AttackForward);
        if (firing.fireMode == DirectedWaveAttackFireMode.Default) return TryFireInDirection(AttackForward, pattern);
        if (originPlayer == null && waveAimTarget == null) return false;
        var target = GetOriginPlayerPosition();
        if (firing.fireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
        {
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > .0001f && Vector3.Dot(direction.normalized, AttackForward) < Mathf.Cos(firing.forwardFireHalfAngle * Mathf.Deg2Rad)) return false;
            return TryFireInDirection(AttackForward, pattern);
        }
        bool refresh = firing.aimMode == DirectedWaveAttackAimMode.AimContinuous
            || firing.aimMode == DirectedWaveAttackAimMode.AimOnBurst && pattern.RepeatBurst && burstStart;
        return TryFireWithLockedEmissionAim(target, pattern, refresh);
    }

'''+needle)
s=s.replace('            initialSpeedSequences[pattern] = default;', '            initialSpeedSequences[pattern] = default;\n            if (!isWaveAttackControlled) lockedEmissionDirections.Remove(pattern);')
# Inspector readouts must use the projectile prefab belonging to the selected reusable projectile.
old='''        return TryGetProjectileBaseSpeed(
            settings.GetPattern(0).Shooting.ProjectileType,
            out baseSpeed);'''
new='''        var shooting = settings.GetPattern(0).Shooting;
        var prefab = shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(shooting.ProjectileType));
        var authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        if (authoring == null) return false;
        baseSpeed = authoring.BaseSpeed;
        return true;'''
assert old in s;s=s.replace(old,new);p.write_text(s,encoding='utf-8')
print('Entrance and autonomous firing policies integrated')
