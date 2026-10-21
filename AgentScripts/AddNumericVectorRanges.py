from pathlib import Path
import re
root=Path('C:/My project')
def read(p): return (root/p).read_text(encoding='utf-8-sig')
def write(p,t): (root/p).write_text(t,encoding='utf-8')
groups=[
 ('Enemies/EnemyShootingSettings','shotGunDirection','Vector2','Direction','PerShot',111,False),
 ('Enemies/EnemyShootingSettings','projectileOrbitFixedWorldCenter','Vector2','All','PerProjectile',311,True),
 ('Enemies/EnemyEmissionLayoutSettings','centerOffset','Vector3','Direction','PerShot',211,False),
 ('Enemies/EnemyEmissionLayoutSettings','positionOffset','Vector3','Direction','PerShot',213,False),
 ('Enemies/EnemyChildAttackSettings','spawnOffset','Vector2','None','PerProjectile',411,False),
]
for file,name,kind,scopes,frequency,channel,motion in groups:
 path='Assets/Scripts/Gameplay/'+file+'.cs'; text=read(path)
 match=re.search(r'((?:\[[^\]]*\]\s*)+)(private '+kind+' '+name+r'\b)',text); assert match,(path,name)
 text=text[:match.start()]+match[1]+f'[Randomizable(nameof({name}XRandom), nameof({name}YRandom), EnemyNumericScopes.{scopes}, EnemyNumericRandomizeOn.{frequency})]\n    '+match[2]+text[match.end():]; write(path,text)
 path='Assets/Scripts/Gameplay/'+file+'.Numeric.cs'; text=read(path)
 block=''.join(f'    [SerializeField, HideInInspector] private RandomFloatRange {name}{axis}Random = new() {{ randomizeOn = EnemyNumericRandomizeOn.{frequency} }};\n' for axis in ['X','Y'])
 pos=text.index('{')+1; text=text[:pos]+'\n'+block+text[pos:]
 flag='UsesProjectileRandomization' if motion else 'UsesGeometryRandomization' if 'EnemyShootingSettings' in file else 'UsesNumericRandomization'
 marker=f'public bool {flag} => '; text=text.replace(marker,marker+f'EnemyNumericSample.Enabled({name}XRandom) || EnemyNumericSample.Enabled({name}YRandom) || ',1)
 hook=re.search(r'private void CopyNumericFrom\([^)]*\)\s*\{',text); assert hook
 copy=''.join(f'\n        {name}{axis}Random = source.{name}{axis}Random?.Copy() ?? new();' for axis in ['X','Y'])
 text=text[:hook.end()]+copy+text[hook.end():]
 if not motion:
  marker='        return copy;'; pos=text.index(marker,text.index('public '+file.split('/')[-1]+' Sample'))
  lines=''
  for i,axis in enumerate(['X','Y']):
   check='' if 'Child' in file else f'if (EnemyShootingSettings.AtScope({name}{axis}Random?.randomizeOn ?? default, perDirection)) '
   lines+=f'        {check}copy.{name}.{axis.lower()} = sample.Float({name}{axis}Random, {name}.{axis.lower()}, {channel+i}u);\n'
  text=text[:pos]+lines+text[pos:]
 else:
  # Fixed world orbit centres are part of the same immutable spawn-time movement snapshot.
  marker='        sample.Float(projectileFinalSizeRandom, projectileFinalSize, 310u, 0f)'
  text=text.replace(marker,marker+',\n        new Vector2(sample.Float(projectileOrbitFixedWorldCenterXRandom, projectileOrbitFixedWorldCenter.x, 311u), sample.Float(projectileOrbitFixedWorldCenterYRandom, projectileOrbitFixedWorldCenter.y, 312u))')
  text=text.replace('    public readonly float Lifetime,','    public readonly Vector2 OrbitFixedWorldCenter;\n    public readonly float Lifetime,')
  text=text.replace('float finalSize)\n','float finalSize, Vector2 orbitFixedWorldCenter)\n')
  text=text.replace('        FinalSize = finalSize;','        FinalSize = finalSize;\n        OrbitFixedWorldCenter = orbitFixedWorldCenter;')
 write(path,text)
 if 'EnemyShootingSettings' in file:
  path='Assets/Scripts/Gameplay/Enemies/EnemyPresetFields.cs'; text=read(path)
  marker='public static readonly string[] '+('Projectile' if motion else 'Layout')+' = {'
  text=text.replace(marker,marker+'\n        '+', '.join('"'+name+a+'Random"' for a in ['X','Y'])+','); write(path,text)
print('Added independent vector-axis ranges.')
