param([Parameter(Mandatory)][string]$Results, [switch]$RequireProfilerData)
$ErrorActionPreference = 'Stop'
$path = (Resolve-Path -LiteralPath $Results).Path
if (Test-Path -LiteralPath $path -PathType Container) { $path = Join-Path $path 'results.json' }
$run = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
$checks = 0
$failures = [System.Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) { $script:checks++; if(-not $Condition){$script:failures.Add($Message)} }
Check ($run.schemaVersion -eq 1) 'Unknown result schema.'
$totalFrames = 0
foreach ($r in $run.repeats) {
    $prefix = "$($r.scenarioId)/$($r.repetition)"
    $totalFrames += $r.frames.Count
    if ($r.status -ne 'completed') { continue }
    Check ($r.cleanupVerified -and $r.defaultWorldUnchanged -and $r.worldsBefore -eq $r.worldsAfter) "${prefix}: cleanup/default World invariant."
    Check ($r.projectilesAfter -eq 0 -and $r.enemiesAfter -eq 0) "${prefix}: residual owned objects."
    $sorted = @($r.frames | Sort-Object)
    $n = $sorted.Count
    Check ($n -gt 0 -and $r.frameMilliseconds.count -eq $n) "${prefix}: invalid frame count."
    if($n -eq 0){ continue }
    $sum = ($sorted | Measure-Object -Sum).Sum
    $median = if($n % 2 -eq 0){($sorted[$n/2-1]+$sorted[$n/2])/2}else{$sorted[[int][Math]::Floor($n/2)]}
    $p95 = $sorted[[int][Math]::Ceiling($n*.95)-1]
    $p99 = $sorted[[int][Math]::Ceiling($n*.99)-1]
    Check ([Math]::Abs($r.frameMilliseconds.mean - $sum/$n) -lt .00001) "${prefix}: mean mismatch."
    Check ([Math]::Abs($r.frameMilliseconds.median - $median) -lt .00001) "${prefix}: median mismatch."
    Check ([Math]::Abs($r.frameMilliseconds.p95-$p95) -lt .00001 -and [Math]::Abs($r.frameMilliseconds.p99-$p99) -lt .00001) "${prefix}: percentile mismatch."
    Check ([Math]::Abs($r.averageFps - $n*1000/$sum) -lt .00001) "${prefix}: FPS mismatch."
    if($r.scenarioId -eq 'child_three_generations'){ Check ($r.highestChildGeneration -ge 3) "${prefix}: three child generations not exercised." }
    if($r.scenarioId -eq 'coordinator_groups'){Check ($r.coordinatorTransitions -ge 2) "${prefix}: group transitions not exercised."}
    if($r.scenarioId -eq 'team_combat'){Check ($r.shipSwitches -gt 0 -and $r.extraHealthGrants -gt 0 -and $r.enemyKills -gt 0) "${prefix}: combat commands not exercised."}
    foreach($m in $r.metrics){
        Check ((-not $m.available) -or $m.distribution.count -gt 0) "$prefix / $($m.name): available metric without samples."
        if($m.name -eq 'GPU Frame Time' -and $m.available){Check ($m.distribution.maximum -le 10000 -and $m.distribution.minimum -gt 0) "${prefix}: invalid GPU timings."}
    }
    if($RequireProfilerData){Check (@($r.metrics | Where-Object { $_.available -and $_.name -eq 'Main Thread' }).Count -eq 1) "${prefix}: Main Thread Recorder not collecting."}
}
$directory = Split-Path $path
foreach($file in @('summary.csv','frames.csv','metrics.csv','memory.csv','report.md')){Check (Test-Path -LiteralPath (Join-Path $directory $file)) "Missing export: $file"}
Check (@(Import-Csv -LiteralPath (Join-Path $directory 'summary.csv')).Count -eq $run.repeats.Count) 'CSV repeat rows mismatch.'
Check (@(Import-Csv -LiteralPath (Join-Path $directory 'frames.csv')).Count -eq $totalFrames) 'CSV frame rows mismatch.'
$result = [pscustomobject]@{run=$run.runId;checks=$checks;passed=($failures.Count -eq 0);failures=@($failures)}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directory 'validation.json') -Encoding utf8
$result | ConvertTo-Json -Depth 4
if($failures.Count -gt 0){exit 1}

