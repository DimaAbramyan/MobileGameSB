"""Read existing APIs and write ONE typed UI adapter file. Never rewrites gameplay sources."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
paths = ['Assets/Scripts/Gameplay/Weapons/WeaponMetaConfig.cs', 'Assets/Scripts/Gameplay/Player/ShipMetaConfig.cs',
         'Assets/Scripts/Gameplay/Player/OrbitShipMetaContract.cs', 'Assets/Scripts/Gameplay/Projectiles/ProjectileData.cs',
         'Assets/Scripts/Gameplay/Enemies/EnemyDebuffConfig.cs', 'Assets/Scripts/Gameplay/Enemies/EnemyHeatDebuffConfig.cs']
scalars = {'float','int','double','bool','string','EnemyDamageType','ProjectileDeliveryType','ProjectileSimulationBackend',
 'ProjectileDamageTrigger','SecondaryProjectileSpawnTrigger','ProjectileContactMode','EnemyProjectileDestructionPolicy',
 'UltimateAbilityMode','EnemyDebuffThresholdMode','EnemyDebuffThresholdAction'}
direct = {'WeaponMetaBasicStats','ShipMetaBasicStats','ProjectileData','ProjectileDamageSource','DictatorTurretSpawnGroup',
          'FanFireAngleGroup','EnemyDebuffApplication','AbilityMetaRecoverySettings','EnemyHeatDebuffConfig'}
bases = {'WeaponMetaContract','ShipMetaContract','ProjectileDataContract','EnemyDebuffConfig'}
extra = {
 'AbilityRecoveryShipMetaContract': 's.Nested(v.Settings, "recovery", "Recovery Settings");',
 'FanFireWeaponMetaContract': 'if (v.PartialFanGroups != null) foreach (var g in v.PartialFanGroups) if (g != null) s.Nested(g, g.StatSourceId, "Fan Group " + g.StatSourceId);',
 'DictatorShipMetaContract': 'if (v.TurretSpawnGroups != null) foreach (var g in v.TurretSpawnGroups) if (g != null) s.Nested(g, g.StatSourceId, "Turret Group " + g.StatSourceId);',
 'ProjectileDamageSourcesContract': 'if (v.Sources != null) foreach (var g in v.Sources) if (g != null) s.Nested(g, g.Id, "Damage Source " + g.Id);',
 'ProjectileEnemyDebuffsContract': 'if (v.Debuffs != null) foreach (var g in v.Debuffs) if (g != null) s.Nested(g, g.StatSourceId, "Debuff Application " + g.StatSourceId);',
 'EnemyDebuffApplication': 'if (v.Debuff != null) s.Nested(v.Debuff, "config", v.Debuff.name, v.Debuff);'}
cases = []
for rel in paths:
    text = (root / rel).read_text(encoding='utf-8-sig')
    for m in re.finditer(r'public\s+(?:(?:sealed|abstract|partial)\s+)*(class|struct)\s+(\w+)([^\{]*)\{', text):
        kind, name, inherit = m.groups()
        if name in bases or (name not in direct and not any(re.search(r'\b'+b+r'\b',inherit) for b in bases)): continue
        end=m.end(); balance=1
        while balance: balance += (text[end]=='{')-(text[end]=='}'); end+=1
        body=text[m.end():end-1]
        lines=[f'            case {name} v:', '            {']
        for typ, prop in re.findall(r'public\s+(?:override\s+)?([\w<>]+)\s+(\w+)\s*(?:=>|\{\s*get\b)',body):
            if prop in {'DisplayName','StatSourceId','DamageGrowthFalloffPower','ExpansionSpeed'}: continue
            label=re.sub(r'(?<=[a-z0-9])(?=[A-Z])',' ',prop)
            if typ in scalars: lines.append(f'                s.Add(nameof(v.{prop}), "{label}", () => v.{prop});')
            if typ=='Vector2':
                for axis in ['x','y']: lines.append(f'                s.Add("{prop}.{axis}", "{label} {axis.upper()}", () => v.{prop}.{axis});')
        if name=='AbilityMetaRecoverySettings':
            for typ,prop in re.findall(r'public\s+(\w+)\s+(\w+)\s*;',body):
                if typ in scalars: lines.append(f'                s.Add(nameof(v.{prop}), "{prop}", () => v.{prop});')
        if name=='EnemyHeatDebuffConfig':
            for prop in ['ThresholdMode','ThresholdAction','TriggerOncePerEnemy']: lines.append(f'                s.Add(nameof(v.{prop}), "{prop}", () => v.{prop});')
        if name in extra: lines.append('                '+extra[name])
        lines += ['                return;', '            }']
        cases += lines
output = '''// Typed adapters for existing contracts. New contracts may override CollectDisplayStats directly.
// Generated offline from public APIs: no runtime reflection or gameplay-source rewriting.
public static class StatContractDeclarations
{
    public static void Collect(IStatValueProvider value, StatSourceBuilder s)
    {
        switch (value)
        {
'''+ '\n'.join(cases)+'''
            default: s.Warn("No stat declaration for " + value.GetType().FullName); return;
        }
    }
}
'''
(root/'Assets/Scripts/UI/StatContractDeclarations.cs').write_text(output,encoding='utf-8')
print('Typed adapter cases:', sum('case ' in x for x in cases))
