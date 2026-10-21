"""Task-local source generation for explicit companion fields/copy code; no runtime reflection."""
from pathlib import Path
import re

ROOT = Path('C:/My project')
def read(path): return (ROOT / path).read_text(encoding='utf-8-sig')
def write(path, text): (ROOT / path).write_text(text, encoding='utf-8')
def replace(path, old, new, count=1):
    text = read(path)
    assert text.count(old) >= count, (path, old)
    write(path, text.replace(old, new, count))

# name, kind, minimum, allowed sampling scopes, default frequency, channel
shooting_geometry = [
 ('fanProjectileCount','int','1f','Direction','PerShot',101),
 ('fanMinAngle','float','float.MinValue','All','PerShot',102),
 ('fanMaxAngle','float','float.MinValue','All','PerShot',103),
 ('shotGunProjectileCount','int','1f','Direction','PerShot',104),
 ('randomMinAngle','float','float.MinValue','All','PerShot',105),
 ('randomMaxAngle','float','float.MinValue','All','PerShot',106),
 ('consistentInitialSpeedChangePerVolley','float','float.MinValue','Volley','PerShot',107),
]
shooting_motion = [
 ('projectileLifetime','float','.01f','All','PerProjectile',301),
 ('projectileSpeedChangeY','float','float.MinValue','All','PerProjectile',302),
 ('projectileAngularSpeed','float','float.MinValue','All','PerProjectile',303),
 ('projectileLateralSpeed','float','float.MinValue','All','PerProjectile',304),
 ('projectileOrbitRadius','float','0f','All','PerProjectile',305),
 ('projectileOrbitRadialSpeed','float','float.MinValue','All','PerProjectile',306),
 ('projectileDnaWeaveSpeed','float','.01f','All','PerProjectile',307),
 ('projectileSizeChangeY','float','float.MinValue','All','PerProjectile',308),
 ('projectileSizeChangeSpeed','float','.01f','All','PerProjectile',309),
 ('projectileFinalSize','float','0f','All','PerProjectile',310),
]
layout = [
 ('directionsCount','int','1f','Volley','PerShot',201),
 ('emitterCount','int','1f','Volley','PerShot',202),
 ('radius','float','0f','Direction','PerShot',203),
 ('startAngle','float','float.MinValue','Volley','PerShot',204),
 ('endAngle','float','float.MinValue','Volley','PerShot',205),
 ('startDelay','float','0f','Volley','PerShot',206),
 ('launchInterval','float','0f','Volley','PerShot',207),
]
rotation = [
 ('rotationSpeedDegreesPerSecond','float','.01f','Volley','PerAttack',221),
 ('rotationAngle','float','float.MinValue','Volley','PerAttack',222),
 ('rotationFromAngle','float','float.MinValue','Volley','PerAttack',223),
]
child = [
 ('triggerValue','float','0f','None','PerProjectile',401),
 ('repeatCount','int','1f','None','PerProjectile',402),
 ('repeatInterval','float','0f','None','PerProjectile',403),
 ('boundaryOffset','float','float.MinValue','None','PerProjectile',404),
 ('timeOffset','float','0f','None','PerProjectile',405),
]
timing = [
 ('attackStartDelay','float','0f','None','PerAttack',501),
 ('attackShotCount','int','1f','None','PerAttack',502),
 ('attackShotInterval','float','0f','None','PerShot',503),
 ('burstShotCount','int','1f','None','PerAttack',504),
 ('burstShotInterval','float','0f','None','PerShot',505),
]
wave_timing = [
 ('attackStartDelay','float','0f','None','PerAttack',601),
 ('attacksPerEnemyPerCycle','int','1f','None','PerAttack',602),
 ('attacksPerSecond','float','.01f','None','PerShot',603),
 ('delayAfterAttack','float','0f','None','PerShot',604),
]
def attach(path, fields):
    text = read(path)
    for name, kind, minimum, scopes, default, channel in fields:
        pattern = rf'((?:\[[^\]]*\]\s*)+)(private {kind} {name}\b)'
        match = re.search(pattern, text)
        assert match, (path, name)
        attrs = re.sub(r',?\s*Min\([^)]*\)', '', match[1]).replace('[SerializeField]', '[SerializeField]')
        attrs = attrs.replace('[,', '[').replace('[SerializeField, ]','[SerializeField]')
        # Randomizable owns validation/UI; remove an old Range so Unity does not select its drawer.
        attrs = re.sub(r',?\s*Range\([^)]*\)', '', attrs)
        annotation = f'[Randomizable(nameof({name}Random), {minimum}, EnemyNumericScopes.{scopes}, EnemyNumericRandomizeOn.{default})]\n    '
        text = text[:match.start()] + attrs + annotation + match[2] + text[match.end():]
    write(path,text)
def fields_code(fields, public=False, resolved=False):
    text = ''
    for name, kind, minimum, scopes, default, channel in fields:
        typename = 'RandomIntRange' if kind == 'int' else 'RandomFloatRange'
        text += f'    [SerializeField, HideInInspector] private {typename} {name}Random = new() {{ randomizeOn = EnemyNumericRandomizeOn.{default} }};\n'
        if public:
            prop = name[0].upper()+name[1:]+'Random'
            owner = 'ResolvePresets().' if resolved else ''
            text += f'    public {typename} {prop} => {owner}{name}Random;\n'
    return text
def enabled(fields): return ' || '.join(f'EnemyNumericSample.Enabled({f[0]}Random)' for f in fields) or 'false'
def copy_code(fields, prefix='source.'):
    return ''.join(f'        {n}Random = {prefix}{n}Random?.Copy() ?? new();\n' for n,*_ in fields)
def sample_code(fields, target='copy.', scoped=True):
    lines=[]
    for name,kind,minimum,scopes,default,channel in fields:
        call = f'sample.{"Int" if kind=="int" else "Float"}({name}Random, {name}, {channel}u, {"1, 512" if kind=="int" else minimum})'
        check = f'if (AtScope({name}Random?.randomizeOn ?? default, perDirection)) ' if scoped else ''
        lines.append(f'        {check}{target}{name} = {call};\n')
    return ''.join(lines)
def partial(path, cls, fields, body):
    write(path,'using UnityEngine;\n\npublic sealed partial class '+cls+'\n{\n'+fields_code(fields)+body+'}\n')

attach('Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs',shooting_geometry+shooting_motion)
attach('Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs',layout)
attach('Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs',rotation)
attach('Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs',child)
attach('Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.cs',timing)
attach('Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSettings.cs',timing)
attach('Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs',wave_timing)
replace('Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs','public sealed class EnemyEmissionLayoutSettings','public sealed partial class EnemyEmissionLayoutSettings')
replace('Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs','public class EnemyRotationSettings','public partial class EnemyRotationSettings')
replace('Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs','public sealed class EnemyChildAttackSettings','public sealed partial class EnemyChildAttackSettings')
replace('Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSettings.cs','public sealed class EnemyAttackPatternSettings','public sealed partial class EnemyAttackPatternSettings')
replace('Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs','public sealed class DirectedWaveAttackSettings','public sealed partial class DirectedWaveAttackSettings')

partial('Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.Numeric.cs','EnemyShootingSettings',shooting_geometry+shooting_motion,
    f'    public bool UsesGeometryRandomization => {enabled(shooting_geometry)} || EmissionLayout.UsesNumericRandomization || Rotation.UsesNumericRandomization;\n'
    f'    public bool UsesProjectileRandomization => {enabled(shooting_motion)};\n'
    '    public bool UsesExtendedNumericRandomization => UsesGeometryRandomization || UsesProjectileRandomization;\n'
    '    internal static bool AtScope(EnemyNumericRandomizeOn frequency, bool perDirection) => perDirection\n'
    '        ? frequency == EnemyNumericRandomizeOn.PerDirection : frequency == EnemyNumericRandomizeOn.PerAttack || frequency == EnemyNumericRandomizeOn.PerShot;\n'
    '    private void CopyNumericFrom(EnemyShootingSettings source)\n    {\n'+copy_code(shooting_geometry+shooting_motion)+'    }\n'
    '    public EnemyShootingSettings SampleGeometry(EnemyNumericSample sample, bool perDirection = false)\n    {\n'
    '        if (!sample.Active || !UsesGeometryRandomization) return this;\n'
    '        var copy = (EnemyShootingSettings)MemberwiseClone();\n'+sample_code(shooting_geometry)+
    '        copy.emissionLayout = EmissionLayout.SampleNumeric(sample, perDirection);\n'
    '        copy.rotation = Rotation.SampleNumeric(sample, perDirection);\n        return copy;\n    }\n'
    '    public EnemyProjectileNumericValues SampleProjectile(EnemyNumericSample sample) => new(\n'
    +',\n'.join(f'        sample.Float({n}Random, {n}, {ch}u, {minimum})' for n,kind,minimum,scopes,default,ch in shooting_motion)+'\n    );\n'
    '    public float NumericFanStart(EnemyNumericSample sample) => sample.Float(fanMinAngleRandom, fanMinAngle, 102u);\n'
    '    public float NumericFanEnd(EnemyNumericSample sample) => sample.Float(fanMaxAngleRandom, fanMaxAngle, 103u);\n'
    '    public float NumericSpread(EnemyNumericSample sample) => Mathf.Lerp(sample.Float(randomMinAngleRandom, RandomMinAngle, 105u), sample.Float(randomMaxAngleRandom, RandomMaxAngle, 106u), sample.Unit(108u));\n')
props=['Lifetime','SpeedChangeY','AngularSpeed','LateralSpeed','OrbitRadius','OrbitRadialSpeed','DnaWeaveSpeed','SizeChangeY','SizeChangeSpeed','FinalSize']
text=read('Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.Numeric.cs')
text+='\npublic readonly struct EnemyProjectileNumericValues\n{\n    public readonly float '+', '.join(props)+';\n'
text+='    public EnemyProjectileNumericValues('+', '.join('float '+p[0].lower()+p[1:] for p in props)+')\n    {\n'
text+=''.join(f'        {p} = {p[0].lower()+p[1:]};\n' for p in props)+'    }\n}\n'
write('Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.Numeric.cs',text)

for path,cls,fields in [
 ('Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.Numeric.cs','EnemyEmissionLayoutSettings',layout),
 ('Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.Numeric.cs','EnemyRotationSettings',rotation)]:
    body=f'    public bool UsesNumericRandomization => {enabled(fields)};\n'
    body+=f'    private void CopyNumericFrom({cls} source)\n    {{\n'+copy_code(fields)+'    }\n'
    body+=f'    public {cls} SampleNumeric(EnemyNumericSample sample, bool perDirection = false)\n    {{\n'
    body+='        if (!sample.Active || !UsesNumericRandomization) return this;\n'
    body+=f'        var copy = ({cls})MemberwiseClone();\n'
    body+=sample_code(fields).replace('AtScope(','EnemyShootingSettings.AtScope(')
    if cls=='EnemyEmissionLayoutSettings': body+='        (copy.orderSource, copy.orderPatternIndex) = OrderKey;\n'
    body+='        return copy;\n    }\n'
    partial(path,cls,fields,body)
    if cls=='EnemyRotationSettings': replace(path,'public sealed partial class','public partial class')

partial('Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.Numeric.cs','EnemyChildAttackSettings',child,
    f'    public bool UsesNumericRandomization => {enabled(child)};\n'
    '    private void CopyNumericFrom(EnemyChildAttackSettings source)\n    {\n'+copy_code(child)+'    }\n'
    '    public EnemyChildAttackSettings SampleNumeric(uint seed)\n    {\n'
    '        if (!UsesNumericRandomization) return this;\n'
    '        var sample = new EnemyNumericSample(seed, seed);\n'
    '        var copy = (EnemyChildAttackSettings)MemberwiseClone();\n'+sample_code(child,scoped=False)+
    '        copy.repeatCount = Mathf.Clamp(copy.repeatCount, 1, 32);\n        return copy;\n    }\n')

for cls,filename,resolved in [('EnemyBurstAttackSettings','EnemyBurstAttackSettings.Numeric.cs',True),('EnemyAttackPatternSettings','EnemyAttackPatternSettings.Numeric.cs',False)]:
    text='using UnityEngine;\n\npublic sealed partial class '+cls+'\n{\n'+fields_code(timing,True,resolved)
    text+=f'    public bool UsesRandomTiming => {enabled(timing)};\n'
    for source in ['EnemyBurstAttackSettings','EnemyAttackPatternSettings']:
        text+=f'    private void CopyNumericTimingFrom({source} source)\n    {{\n'
        text+=''.join(f'        {n}Random = source.{n[0].upper()+n[1:]}Random?.Copy() ?? new();\n' for n,*_ in timing)+'    }\n'
    if resolved:
        text+='    public float SampleStartDelay(EnemyAttackNumericState numeric) => UsesAttackStartDelay ? numeric?.SampleTiming(AttackStartDelay, AttackStartDelayRandom) ?? AttackStartDelay : 0f;\n'
        text+='    public float SampleShotInterval(EnemyAttackNumericState numeric) => numeric?.SampleTiming(AttackShotInterval, AttackShotIntervalRandom) ?? AttackShotInterval;\n'
        text+='    public int SampleBurstCount(EnemyAttackNumericState numeric) => numeric?.SampleTiming(BurstShotCount, BurstShotCountRandom) ?? BurstShotCount;\n'
        text+='    public float SampleBurstInterval(EnemyAttackNumericState numeric) => numeric?.SampleTiming(BurstShotInterval, BurstShotIntervalRandom) ?? BurstShotInterval;\n'
    text+='}\n'
    write('Assets/Scripts/Gameplay/Enemies/'+filename,text)

partial('Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSettings.Numeric.cs','DirectedWaveAttackSettings',wave_timing,
    '    private void CopyNumericFrom(DirectedWaveAttackSettings source)\n    {\n'+copy_code(wave_timing)+'    }\n'
    '    public float SampleStartDelay(ref NumericRandomStream random) => useAttackStartDelay ? attackStartDelayRandom.Sample(AttackStartDelay, ref random, 0f) : 0f;\n'
    '    public float SampleAttacksPerSecond(ref NumericRandomStream random) => attacksPerSecondRandom.Sample(AttacksPerSecond, ref random, .01f);\n'
    '    public float SampleDelayAfterAttack(ref NumericRandomStream random) => delayAfterAttackRandom.Sample(DelayAfterAttack, ref random, 0f);\n'
    '    public int SampleAttacksPerCycle(ref NumericRandomStream random) => Mathf.Max(1, attacksPerEnemyPerCycleRandom.Sample(AttacksPerEnemyPerCycle, ref random));\n')

# Explicit copy hooks retain existing deep-copy and override semantics.
replace('Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs','        CopyPresetBindingsFrom(source);','        CopyPresetBindingsFrom(source);\n        CopyNumericFrom(source);')
replace('Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs','|| TryGetRandomSpeedRange(out _, out _) || useChildAttackPattern;','|| TryGetRandomSpeedRange(out _, out _) || useChildAttackPattern || UsesExtendedNumericRandomization;')
replace('Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs','        type = source.type;','        CopyNumericFrom(source);\n        type = source.type;')
replace('Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs','        Configure(source.direction,','        CopyNumericFrom(source);\n        Configure(source.direction,')
replace('Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs','            copy.stationaryFireSettings.CopyFrom','            copy.CopyNumericFrom(source);\n            copy.stationaryFireSettings.CopyFrom')
replace('Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs','        hasRuntimeProjectileBaseSpeedOverride = false;','        CopyNumericFrom(source);\n        hasRuntimeProjectileBaseSpeedOverride = false;')
for path,anchor,count in [
 ('Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSettings.cs','        shooting.CopyFrom(source.',2),
 ('Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.cs','        shooting.CopyFrom(source.',2)]:
    # Hook only full/timing copies. CopyShootingFrom intentionally keeps the owning timing configuration.
    text=read(path)
    matches=list(re.finditer(re.escape(anchor),text))[:count]
    for m in reversed(matches): text=text[:m.start()]+'        CopyNumericTimingFrom(source);\n'+text[m.start():]
    write(path,text)

path='Assets/Scripts/Gameplay/Enemies/EnemyPresetFields.cs'
text=read(path)
for group,extra in [('Projectile',shooting_motion+[shooting_geometry[-1]]),('Layout',shooting_geometry[:-1]),('Timing',timing)]:
    marker=f'public static readonly string[] {group} = {{'
    text=text.replace(marker,marker+'\n        '+', '.join('"'+n+'Random"' for n,*_ in extra)+',',1)
write(path,text)
print('Generated companion ranges and explicit copy hooks.')
