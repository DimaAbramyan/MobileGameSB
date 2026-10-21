from pathlib import Path

def edit(path, replacements):
    p = Path(path)
    s = p.read_text(encoding='utf-8-sig')
    for old,new in replacements:
        assert old in s, (path,old)
        s = s.replace(old,new)
    p.write_text(s,encoding='utf-8')

base = 'Assets/Scripts/Gameplay/Enemies/'
edit(base+'EnemyBurstAttackSettings.cs', [
 ('public RandomFloatRange AttackCooldownRandom => attackCooldownRandom ??= new();', 'public RandomFloatRange AttackCooldownRandom => ResolvePresets().attackCooldownRandom ??= new();'),
 ('public bool RepeatBurst => burstAttack ==', 'public bool RepeatBurst => ResolvePresets().burstAttack =='),
 ('public EnemyBurstAttackMode BurstAttack => burstAttack;', 'public EnemyBurstAttackMode BurstAttack => ResolvePresets().burstAttack;'),
 ('public bool UsesAttackStartDelay => useAttackStartDelay;', 'public bool UsesAttackStartDelay => ResolvePresets().useAttackStartDelay;'),
 ('public float AttackStartDelay => useAttackStartDelay\n        ? Mathf.Max(0f, attackStartDelay)', 'public float AttackStartDelay => ResolvePresets().useAttackStartDelay\n        ? Mathf.Max(0f, ResolvePresets().attackStartDelay)'),
 ('public EnemyShootingSettings Shooting => shooting;', 'public EnemyShootingSettings Shooting => ResolvePresets().shooting;'),
 *[(f'Mathf.Max({minimum}, {field});', f'Mathf.Max({minimum}, ResolvePresets().{field});') for field,minimum in [('attackShotCount','1'),('attackShotInterval','0f'),('attackCooldown','0f'),('burstShotCount','1'),('burstShotInterval','0f')]],
 ('shooting.CopyFrom(source.Shooting);', 'shooting.CopyFrom(source.AuthoredShooting);'),
 ('public void Validate()\n    {', 'public void Validate()\n    {\n        presetResolved = null;'),
 ('shooting.ScaleAttackTiming(multiplier);', 'Shooting.ScaleAttackTiming(multiplier);'),
 ('shooting.SetRuntimeProjectileBaseSpeedOverride(speed);', 'Shooting.SetRuntimeProjectileBaseSpeedOverride(speed);')
])
# Fix replacement inside validation: validation always clamps authored values, not linked copies.
p=Path(base+'EnemyBurstAttackSettings.cs');s=p.read_text();start=s.index('    public void Validate()');s=s[:start]+s[start:].replace('ResolvePresets().','');p.write_text(s,encoding='utf-8')
edit(base+'EnemyAttackPatternSettings.cs', [
 ('public EnemyShootingSettings Shooting => shooting;', 'internal EnemyShootingSettings AuthoredShooting => shooting;\n    public EnemyShootingSettings Shooting => shooting.HasLinkedPresets ? RuntimeSettings.Shooting : shooting;\n    [System.NonSerialized] private int presetRevision = -1;'),
 ('if (runtimeSettings == null)', 'if (runtimeSettings == null || presetRevision != EnemyPresetRevision.Value)'),
 ('runtimeSettings = copy;', 'runtimeSettings = copy;\n                presetRevision = EnemyPresetRevision.Value;'),
 ('shooting.CopyFrom(source.Shooting);', 'shooting.CopyFrom(source.AuthoredShooting);'),
])
edit(base+'EnemyAttackProjectileBuilder.cs', [
 ('int fanCount = shooting.UsesFan', 'prefab = shooting.ResolveProjectilePrefab(prefab);\n        int fanCount = shooting.UsesFan')
])
edit(base+'SimpleEnemyAttackExecutor.cs', [
 ('GetSelectedProjectilePrefab(ActiveShooting.ProjectileType) != null', 'ActiveShooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(ActiveShooting.ProjectileType)) != null'),
 ('GetSelectedProjectilePrefab(ActivePatterns.GetPattern(i).Shooting.ProjectileType) == null', 'ActivePatterns.GetPattern(i).Shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(ActivePatterns.GetPattern(i).Shooting.ProjectileType)) == null'),
 ('EnemyBullet prefab = GetSelectedProjectilePrefab(shooting.ProjectileType);', 'EnemyBullet prefab = shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(shooting.ProjectileType));')
])
edit('Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs', [
 ('var prefab = state.Prefabs.Get(shooting.ProjectileType);', 'var prefab = shooting.ResolveProjectilePrefab(state.Prefabs.Get(shooting.ProjectileType));')
])
edit('Assets/Editor/EnemyChildAttackScenePreview.cs', [
 ('var prefab = prefabs.Get(shooting.ProjectileType);', 'var prefab = shooting.ResolveProjectilePrefab(prefabs.Get(shooting.ProjectileType));'),
 ('EditorJsonUtility.ToJson(executor) +', 'EnemyPresetRevision.Value + EditorJsonUtility.ToJson(executor) +')
])
edit('Assets/Editor/WaveShootingPreviewSession.cs', [
 ('private readonly int numericPreviewRevision', 'private readonly int presetConfigurationRevision = EnemyPresetRevision.Value;\n    private readonly int numericPreviewRevision'),
 ('numericPreviewRevision == NumericRandomPreviewControls.Revision;', 'numericPreviewRevision == NumericRandomPreviewControls.Revision && presetConfigurationRevision == EnemyPresetRevision.Value;'),
 ('var prefab = slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0];', 'var prefab = shooting.ResolveProjectilePrefab(slot.Prefabs[shooting.ProjectileType == EnemyProjectileType.Laser ? 0 : (int)shooting.ProjectileType] ?? slot.Prefabs[0]);')
])
print('Preset resolution and prefab selection integrated')
