param(
    [string]$Scenario = '',
    [float]$MeasurementSeconds = 0,
    [int]$Repetitions = 0,
    [switch]$IncludeSoak,
    [switch]$ProbeMotion,
    [int]$TimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
if ($Scenario -and $Scenario -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid scenario ID.' }
if ($MeasurementSeconds -lt 0 -or $Repetitions -lt 0 -or $TimeoutSeconds -lt 10) { throw 'Invalid timing arguments.' }
$projectPath = (Get-Location).Path
function Invoke-Pipeline([string[]]$CommandArguments) {
    $raw = & unity command @CommandArguments --project-path $projectPath --format json
    if ($LASTEXITCODE -ne 0) { throw 'Unity CLI command failed; Editor may be reloading.' }
    $response = ($raw -join "`n") | ConvertFrom-Json
    if (-not $response.success) { throw ($response.errors | Select-Object -First 1).message }
    return $response.data.result
}
$argumentsJson = ConvertTo-Json -Compress -InputObject @($Scenario, $MeasurementSeconds, $Repetitions, [bool]$IncludeSoak)
$start = Invoke-Pipeline @('run_script','--file','AgentScripts/VerifyAndroidPerformanceSuite.cs','--entry','VerifyAndroidPerformanceSuite.Start','--args',$argumentsJson)
if (-not $start.success) { throw ('Suite launch failed: ' + $start.errorDetails) }
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$finished = $false
$motionProbed = $false
try {
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 3
        try {
            $probe = Invoke-Pipeline @('eval','var r = UnityEngine.Object.FindFirstObjectByType<Game.Performance.PerformanceSuiteRunner>(); return r == null ? null : new { phase = r.Phase, status = r.Result == null ? null : r.Result.status, output = r.LastOutput };')
        } catch { continue } # Expected connection loss during Play Mode domain reload.
        if (-not $probe.success -or -not $probe.result) { continue }
        $state = $probe.result
        if ($state -is [string]) { $state = $state | ConvertFrom-Json }
        Write-Output "Performance Suite: $($state.phase)"
        if($ProbeMotion -and -not $motionProbed -and $state.phase -eq 'measure') {
            $motion = Invoke-Pipeline @('run_script','--file','AgentScripts/ProbePerformanceWorld.cs','--entry','ProbePerformanceWorld.Run')
            $motion | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath 'AgentArtifacts/Performance/runtime-advance-probe.json' -Encoding utf8
            if(-not $motion.success -or -not $motion.result.passed){throw ('Runtime movement probe failed: '+$motion.errorDetails)}
            $motionProbed = $true
            Write-Output 'Real movement/lifetime probe passed.'
        }
        if ($state.phase -eq 'idle') {
            $finished = $true
            Write-Output "Result: $($state.status); $($state.output)"
            if ($state.status -ne 'completed') { throw 'The suite completed with failures/cancellation; inspect results.json.' }
            break
        }
    }
    if (-not $finished) {
        $null = Invoke-Pipeline @('eval','var r = UnityEngine.Object.FindFirstObjectByType<Game.Performance.PerformanceSuiteRunner>(); if (r != null) r.Cancel(); return true;')
        throw 'Suite timeout; cancellation requested.'
    }
} finally {
    # This script owns the dedicated Play session, never closes the Editor process.
    $null = Invoke-Pipeline @('editor_stop')
}
