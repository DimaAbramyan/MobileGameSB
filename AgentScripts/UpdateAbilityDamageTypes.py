from pathlib import Path

def edit(path, change):
    p=Path(path); raw=p.read_bytes(); bom=raw.startswith(b'\xef\xbb\xbf')
    text=raw.decode('utf-8-sig').replace('\r\n','\n')
    backup=Path('AgentArtifacts/AbilitiesSlotsBefore')/(p.name+'.before.txt')
    backup.parent.mkdir(parents=True,exist_ok=True)
    if not backup.exists(): backup.write_bytes(raw)
    result=change(text)
    eol='\r\n' if b'\r\n' in raw else '\n'
    p.write_bytes((b'\xef\xbb\xbf' if bom else b'')+result.replace('\n',eol).encode('utf-8'))

def rep(text, old, new):
    assert text.count(old)==1, (old[:100],text.count(old))
    return text.replace(old,new)

def contracts(text):
    for name,next_name in [('BlackHoleShipMetaContract','BladeShipMetaContract'),('BladeShipMetaContract','HeavyShieldShipMetaContract')]:
        start=text.index('public sealed class '+name); end=text.index('public sealed class '+next_name,start)
        part=text[start:end]
        part=rep(part,'private float damage = 50f;','private float damage = 50f;\n    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;')
        part=rep(part,'public float Damage => Mathf.Max(0f, damage);','public float Damage => Mathf.Max(0f, damage);\n    public EnemyDamageType DamageType => damageType;')
        part=rep(part,'clone.projectileDestructionPolicy = projectileDestructionPolicy;','clone.projectileDestructionPolicy = projectileDestructionPolicy;\n        clone.damageType = damageType;')
        text=text[:start]+part+text[end:]
    text=rep(text,'private float ballDamage = 10f;','private float ballDamage = 10f;\n    [SerializeField] private EnemyDamageType ballDamageType = EnemyDamageType.Default;')
    text=rep(text,'private float stasisDamagePerSecond = 30f;','private float stasisDamagePerSecond = 30f;\n    [SerializeField] private EnemyDamageType stasisDamageType = EnemyDamageType.Default;')
    text=rep(text,'public float BallDamage => Mathf.Max(0f, ballDamage);','public float BallDamage => Mathf.Max(0f, ballDamage);\n    public EnemyDamageType BallDamageType => ballDamageType;\n    public EnemyDamageType StasisDamageType => stasisDamageType;')
    text=rep(text,'clone.paddleBounceSpeedMultiplier = paddleBounceSpeedMultiplier;','clone.ballDamageType = ballDamageType;\n        clone.stasisDamageType = stasisDamageType;\n        clone.paddleBounceSpeedMultiplier = paddleBounceSpeedMultiplier;')
    return text

def orbit(text):
    text=rep(text,'private float contactDamage = 2f;','private float contactDamage = 2f;\n    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Default;')
    return rep(text,'public float ContactDamage => Mathf.Max(0f, contactDamage);','public float ContactDamage => Mathf.Max(0f, contactDamage);\n    public EnemyDamageType DamageType => damageType;')

def inspector(text):
    for name,label,field,type_label in [('damage','Damage','damageType','Damage Type'),('ballDamage','Ball Damage','ballDamageType','Ball Damage Type'),('stasisDamagePerSecond','Stasis Damage per Second','stasisDamageType','Stasis Damage Type'),('contactDamage','Enemy Contact Damage','damageType','Damage Type')]:
        old=f'DrawField(contract, "{name}", "{label}");'
        text=rep(text,old,old+f'\n                DrawField(contract, "{field}", "{type_label}");')
    return text

edit('Assets/Scripts/Gameplay/Player/ShipMetaConfig.cs',contracts)
edit('Assets/Scripts/Gameplay/Player/OrbitShipMetaContract.cs',orbit)
edit('Assets/Editor/ShipMetaConfigEditor.cs',inspector)
print('Typed damage fields and Inspector controls added; clones retain all new fields.')
