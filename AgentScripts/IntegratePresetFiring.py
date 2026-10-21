from pathlib import Path

p=Path('Assets/Scripts/Gameplay/Waves/DirectedWaveAttackController.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('DirectedWaveAttackFireMode fireMode = GetFireMode(enemy);','DirectedWaveAttackFireMode fireMode = GetFireMode(enemy, attackSettings);')
s=s.replace('if (GetFireMode(enemy) != DirectedWaveAttackFireMode.Aimed)','if (GetFireMode(enemy, pattern) != DirectedWaveAttackFireMode.Aimed)')
s=s.replace('DirectedWaveAttackAimMode mode = GetAimMode(enemy);','DirectedWaveAttackAimMode mode = GetAimMode(enemy, pattern);')
s=s.replace('private DirectedWaveAttackAimMode GetAimMode(Enemy enemy)','private DirectedWaveAttackAimMode GetAimMode(Enemy enemy, EnemyBurstAttackSettings profile = null)')
s=s.replace('return enemyOverride != null ? enemyOverride.ResolveAimMode(settings.AimMode) : settings.AimMode;', '''var firing = profile?.Shooting.PresetFiring;
        var fallback = firing != null && firing.enabled ? firing.aimMode : settings.AimMode;
        return enemyOverride != null ? enemyOverride.ResolveAimMode(fallback) : fallback;''')
s=s.replace('private DirectedWaveAttackFireMode GetFireMode(Enemy enemy)','private DirectedWaveAttackFireMode GetFireMode(Enemy enemy, EnemyBurstAttackSettings profile = null)')
s=s.replace('''        return enemyOverride != null
            ? enemyOverride.ResolveFireMode(settings.FireMode)
            : settings.FireMode;''','''        var firing = profile?.Shooting.PresetFiring;
        var fallback = firing != null && firing.enabled ? firing.fireMode : settings.FireMode;
        return enemyOverride != null ? enemyOverride.ResolveFireMode(fallback) : fallback;''')
s=s.replace('''            var stationary = local != null ? local.ResolveStationaryFireSettings(settings.StationaryFireSettings) : settings.StationaryFireSettings;''','''            var firing = attackSettings?.Shooting.PresetFiring;
            var fallback = firing != null && firing.enabled ? firing.stationaryFireSettings : settings.StationaryFireSettings;
            var stationary = local != null ? local.ResolveStationaryFireSettings(fallback) : fallback;''')
p.write_text(s,encoding='utf-8')

p=Path('Assets/Editor/WaveShootingPreviewSession.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('new EnemyProjectileSpawnRequest(slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0],', 'new EnemyProjectileSpawnRequest(shooting.ResolveProjectilePrefab(slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0]),')
s=s.replace('''        bool aimed = slot.FireMode == DirectedWaveAttackFireMode.Aimed;
        bool stationary = slot.FireMode == DirectedWaveAttackFireMode.Stationary && !slot.Stationary.PreservesLegacyForward;''','''        var firing = shooting.PresetFiring;
        var local = wave.GetConfiguredEnemyOverrideForSlot(slot.Index);
        var fireMode = firing.enabled ? local != null ? local.ResolveFireMode(firing.fireMode) : firing.fireMode : slot.FireMode;
        var aimMode = firing.enabled ? local != null ? local.ResolveAimMode(firing.aimMode) : firing.aimMode : slot.AimMode;
        var stationarySettings = firing.enabled ? local != null ? local.ResolveStationaryFireSettings(firing.stationaryFireSettings) : firing.stationaryFireSettings : slot.Stationary;
        bool aimed = fireMode == DirectedWaveAttackFireMode.Aimed;
        bool stationary = fireMode == DirectedWaveAttackFireMode.Stationary && !stationarySettings.PreservesLegacyForward;''')
# Restrict changes to QueueVolley, retaining the saved per-volley aim for delayed launches.
a=s.index('    private void QueueVolley(');b=s.index('    private float Random01()',a);part=s[a:b]
part=part.replace('slot.AimMode ==','aimMode ==').replace('if (stationary) direction = slot.Stationary.Direction;','if (stationary) direction = stationarySettings.Direction;').replace('if (slot.FireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead','if (fireMode == DirectedWaveAttackFireMode.None) return;\n        if (fireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead')
s=s[:a]+part+s[b:];p.write_text(s,encoding='utf-8')
print('Firing policy integrated without changing local override precedence')
