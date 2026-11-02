param([Parameter(Mandatory)][string]$Results)
$ErrorActionPreference='Stop'
$path=(Resolve-Path -LiteralPath $Results).Path
if(Test-Path -LiteralPath $path -PathType Container){$path=Join-Path $path 'results.json'}
$run=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
$culture=[Globalization.CultureInfo]::InvariantCulture
$systemSources=@{}
if(Test-Path -LiteralPath 'Assets/Scripts/Gameplay'){
    if(Get-Command rg -ErrorAction SilentlyContinue){
        $sourceFiles=@(& rg --files --glob '*.cs' 'Assets/Scripts/Gameplay')
    }else{
        $sourceFiles=@(Get-ChildItem -LiteralPath 'Assets/Scripts/Gameplay' -Filter '*.cs' -File -Recurse|ForEach-Object FullName)
    }
    foreach($sourceFile in $sourceFiles){
        foreach($declaration in [regex]::Matches([IO.File]::ReadAllText($sourceFile),'\b(?:class|struct)\s+([A-Za-z_]\w*System)\b')){
            $systemSources[$declaration.Groups[1].Value]=$sourceFile.Replace('\','/')
        }
    }
}
function N([double]$value){$value.ToString('0.###',$culture)}
function Source([string]$marker){
    if($marker -like '*WaveAttackCoordinator*'){return 'Assets/Scripts/Gameplay/Waves/WaveAttackCoordinator.cs: Update / WaveAttackCoordinatorPlayer.Tick'}
    if($marker -like '*AttackBehaviour*'){return 'Assets/Scripts/Gameplay/Waves/DirectedWaveAttackBehaviour.cs: TickPostTimeline'}
    if($marker -like '*EnemyChildAttackSystem*'){return 'Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs: OnUpdate / EnemyChildAttackRunner.Tick'}
    if($marker -like '*EnemyLaserSystem*'){return 'Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserRuntime.cs: EnemyLaserSystem.OnUpdate / EnemyLaserRuntime.Tick'}
    if($marker -match '\b(\w*Projectile\w*System)\b'){
        $systemName=$Matches[1]
        if($systemSources.ContainsKey($systemName)){return "$($systemSources[$systemName]): $systemName.OnUpdate"}
        return "$systemName.OnUpdate: source location unavailable in this checkout"
    }
    if($marker -like '*Spawn*'){return 'PerformanceWorkloadRuntime.Tick / EnemyProjectileEcsSpawner.TrySpawnBatch (includes request setup)'}
    if($marker -like '*CombatCommands*'){return 'PerformanceWorkloadRuntime.Tick: explicit real gameplay commands'}
    if($marker -like '*WaveSetup*'){return 'PerformanceWorkloadRuntime.CreateWave / Wave.Init'}
    return 'Unity/Editor process-wide marker; not attributed to a gameplay class'
}
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('# Performance diagnostics')
$lines.Add('')
$lines.Add("Run $($run.runId), $($run.platform), $($run.buildKind). These are observations and investigation candidates, not automatic optimization conclusions.")
foreach($r in $run.repeats){
    $lines.Add('');$lines.Add("## $($r.scenarioId) / $($r.repetition)")
    $lines.Add("Status: $($r.status); samples: $($r.frameMilliseconds.count); P95 $(N $r.frameMilliseconds.p95) ms; P99 $(N $r.frameMilliseconds.p99) ms. Cleanup verified: $($r.cleanupVerified).")
    $lines.Add('');$lines.Add('| Measured marker | Mean ms / frame | P95 ms | Observed invocations | Source |')
    $lines.Add('|---|---:|---:|---:|---|')
    foreach($m in @($r.metrics|Where-Object {$_.available -and $_.unit -eq 'ms' -and $_.name -notlike '*Frame Time'}|Sort-Object {$_.distribution.mean} -Descending|Select-Object -First 8)){
        $lines.Add("| $($m.name) | $(N $m.distribution.mean) | $(N $m.distribution.p95) | $($m.observedInvocations) | $(Source $m.name) |")
    }
    $gc=@($r.metrics|Where-Object {$_.name -eq 'GC Allocated In Frame' -and $_.available})
    if($gc.Count -gt 0){$lines.Add("`nObserved process GC allocation: mean $(N $gc[0].distribution.mean) bytes/frame. This includes Editor and the observer; use profiler.raw for call attribution.")}
    $unavailable=@($r.metrics|Where-Object {-not $_.available}|ForEach-Object {$_.name})
    if($unavailable.Count -gt 0){$lines.Add("`nUnavailable/no observed invocation timing: $($unavailable -join ', '). Do not interpret these as free systems.")}
    foreach($a in $r.anomalies){$lines.Add("`n- $($a -replace '\r?\n',' ')")}
}
$lines.Add('');$lines.Add('## Post-cleanup retention')
foreach($group in @($run.repeats|Where-Object status -eq completed|Group-Object scenarioId)){
    $values=@($group.Group|Sort-Object repetition)
    if($values.Count -ge 3){
        $delta=[long]$values[-1].memoryAfter-[long]$values[0].memoryAfter
        $lines.Add("- $($group.Name): $($values.Count) repetitions; first-to-last retained allocated delta $(N ($delta/1048576)) MiB. Correlate with stabilized caches and report-buffer retention; this alone neither proves nor excludes a leak.")
    }else{$lines.Add("- $($group.Name): insufficient repetitions for a retained-memory trend.")}
}
$output=Join-Path (Split-Path $path) 'diagnostics.md'
$lines|Set-Content -LiteralPath $output -Encoding utf8
Write-Output $output
