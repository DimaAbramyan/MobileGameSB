from pathlib import Path
import re, json, hashlib

root = Path(__file__).resolve().parents[1]
if (root/'AgentArtifacts/fast-seek-profile-source-before.json').exists():
    raise SystemExit('Profiling is already installed. Original-source evidence will not be overwritten.')
before = {}
def edit(path, operation):
    p = root / path
    data = p.read_bytes()
    before[path] = dict(base64=__import__('base64').b64encode(data).decode(), sha256=hashlib.sha256(data).hexdigest())
    text = data.decode('utf-8-sig')
    newline = '\r\n' if '\r\n' in text else '\n'
    text = operation(text.replace('\r\n', '\n'))
    p.write_bytes(text.replace('\n', newline).encode('utf-8-sig' if data.startswith(b'\xef\xbb\xbf') else 'utf-8'))

def scopes(text, methods, runtime=False):
    for method, area in methods:
        # Declaration only; do not match call sites or expression-bodied members.
        pattern = r'(^    (?:public|private|internal)[^\n;{}=]*\b' + re.escape(method) + r'\([^;{}]*?\)\s*\n    \{)'
        matches = list(re.finditer(pattern, text, re.M))
        if len(matches) != 1: raise Exception((method, len(matches)))
        addition = f'\n        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.{area});'
        if runtime: addition = '\n#if UNITY_EDITOR' + addition + '\n#endif'
        match=matches[0]; text=text[:match.end()] + addition + text[match.end():]
    return text

editor = {
'Assets/Editor/WavePreviewPlayer.cs': [('Update','PlayerUpdate')],
'Assets/Editor/WaveConductorShootingPreview.cs': [('PrepareReadyCues','InitializeCue'),('PublishRenderState','Publish'),('SetVisible','Visibility')],
'Assets/Editor/WaveShootingPreviewSession.cs': [('TickWithBudget','SessionTick'),('UpdatePositions','Positions'),('TickGroup','AttackGroup'),('TickSlot','AttackSlot'),('QueueVolley','QueueVolley'),('EmitDirection','EmitDirection')],
'Assets/Editor/WaveShootingPreviewCoordinator.cs': [('TickCoordinatorSequence','AttackSequence')],
'Assets/Editor/EnemyShootingPreviewGeometry.cs': [('GetFacingGuide','Facing'),('GetEmitter','EmitterGeometry')],
'Assets/Editor/EnemyProjectilePreviewWorld.cs': [('Step','ProjectileStep'),('Spawn','Spawn'),('CaptureRenderState','CaptureRender'),('BoundBurstRequests','BurstBounds')],
'Assets/Editor/WavePreviewSnapshotCache.cs': [('Capture','SnapshotCapture'),('Trim','CacheTrim')],
'Assets/Editor/WaveConductorPreviewSnapshots.cs': [('CaptureInitialSession','SnapshotManaged')],
'Assets/Editor/WavePreviewPlayerWindow.cs': [('OnGUI','WindowGUI'),('DrawScene','SceneGUI'),('DrawTimeline','Timeline')],
'Assets/Editor/WaveShootingPreviewPresentation.cs': [('PublishVisuals','PublishVisuals')],
}
runtime = {
'Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs': [('EvaluateSimulationPreviewInternal','WaveEvaluate'),('EvaluateSimulationPipeline','PostPipeline'),('EvaluateSimulationCommandFrame','PostCommand'),('EvaluateSimulationEntrancePosition','Entrance'),('EvaluateSimulationCheckpointPath','CheckpointPath'),('CreateSimulationFormationPositions','Formation')],
'Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs': [('Tick','Child'),('Update','ChildTriggerUpdate'),('QueueVolley','ChildVolley'),('Emit','ChildEmit')],
'Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserRuntime.cs': [('Tick','Laser'),('UpdateGeometry','LaserGeometry')],
}
for path, methods in editor.items(): edit(path, lambda s, ms=methods: scopes(s, ms))
for path, methods in runtime.items(): edit(path, lambda s, ms=methods: scopes(s, ms, True))

# Additional tiny ranges, preserving statement ordering.
def extra(path, fn):
    p = root/path; s=p.read_text(encoding='utf-8-sig'); data=p.read_bytes()
    p.write_bytes(fn(s).replace('\n', '\r\n' if b'\r\n' in data else '\n').encode('utf-8-sig' if data.startswith(b'\xef\xbb\xbf') else 'utf-8'))
def replace(s,a,b):
    if s.count(a)!=1: raise Exception(('replace count',a,s.count(a)))
    return s.replace(a,b)
extra('Assets/Editor/WaveConductorShootingPreview.cs', lambda s: replace(replace(s,
'            beforeStep?.Invoke(StepCount);','            using var stepProfile = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.FixedStep);\n            beforeStep?.Invoke(StepCount);'),
'            coordinatorPlayer?.Tick(Time);','            using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Coordinator)) coordinatorPlayer?.Tick(Time);'))
def systems(s):
    for name, area in [('homing','Homing'),('burst','Burst'),('speed','SpeedCurve'),('scale','SizeCurve'),('movement','Movement'),('lifetime','Lifetime')]:
        s=replace(s,f'        {name}.Update(world.Unmanaged);',f'        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.{area})) {name}.Update(world.Unmanaged);')
    s=replace(s,'        using (var entities = projectiles.ToEntityArray(Allocator.Temp))','        using (WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Cleanup))\n        using (var entities = projectiles.ToEntityArray(Allocator.Temp))')
    s=replace(s,'        var pending = world.EntityManager.GetBuffer<EnemyProjectileBurstSpawnRequest>(registry);','        using var burstSpawnProfile = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.BurstSpawn);\n        var pending = world.EntityManager.GetBuffer<EnemyProjectileBurstSpawnRequest>(registry);')
    s=s.replace('MaximumProjectiles - Count','WavePreviewProfiler.EffectiveProjectileLimit(MaximumProjectiles) - Count')
    s=s.replace('target, MaximumProjectiles, gameplayCamera','target, WavePreviewProfiler.EffectiveProjectileLimit(MaximumProjectiles), gameplayCamera')
    return s
extra('Assets/Editor/EnemyProjectilePreviewWorld.cs',systems)
extra('Assets/Editor/WavePreviewPlayer.cs',lambda s:replace(replace(s,'MaxStepsPerUpdate, deferPresentation: true);','MaxStepsPerUpdate, deferPresentation: !WavePreviewProfiler.IntermediateVisuals);'),
'        if (frame % interval == 0) snapshots.Capture(simulation, events, OmittedEvents);','        if (frame % interval == 0 && !WavePreviewProfiler.SkipNewSnapshots) snapshots.Capture(simulation, events, OmittedEvents);'))

# Snapshot constructors are nested (8 spaces), and state graph uses an expression member.
for path, needles in {
'Assets/Editor/WaveConductorPreviewSnapshots.cs':[
('        internal Snapshot(WaveConductorShootingPreview owner)\n        {','SnapshotManaged'),
('        internal void Restore()\n        {','SnapshotRestore'),
('        public VisualState(WaveShootingPreviewSession session)\n        {','SnapshotVisuals')],
'Assets/Editor/EnemyProjectilePreviewSnapshots.cs':[
('        public Snapshot(EnemyProjectilePreviewWorld source)\n        {','SnapshotNative')],
}.items():
    if path not in before:
        data=(root/path).read_bytes();before[path]=dict(base64=__import__('base64').b64encode(data).decode(),sha256=hashlib.sha256(data).hexdigest())
    def apply(s, ns=needles):
        for needle,area in ns: s=replace(s,needle,needle+f'\n            using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.{area});')
        return s
    extra(path,apply)
(root/'AgentArtifacts/fast-seek-profile-source-before.json').write_text(json.dumps(before),encoding='utf-8')
print('Instrumented',len(before),'files; exact original bytes saved.')
