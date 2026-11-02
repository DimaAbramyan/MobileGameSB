param([ValidateSet('Combat', 'Preview', 'Gameplay', 'Integration')][string]$Group = 'Combat')
$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $auditWorkspace
$auditCases = @(switch ($Group) {
    Combat {
        ,@('VerifyChildAttackPattern', 'Run')
        ,@('VerifyChildAttackIntegration', 'Run')
        ,@('VerifyChildBoundary', 'Run')
        ,@('VerifyChildBoundarySelection', 'Run')
        ,@('VerifyEnemyLaser', 'Run')
        ,@('VerifyAttackSequence', 'RunAll')
        ,@('VerifyAttackOrder', 'Run')
        ,@('VerifyWaveAttackCoordinator', 'Run')
        ,@('VerifyMotionCurves', 'Run')
        ,@('VerifyDiveMotionLimits', 'Run')
    }
    Preview {
        ,@('VerifyWavePreviewNativeSnapshots', 'Run')
        ,@('VerifyWaveAuditEditorLifecycle', 'Run')
        ,@('VerifyWavePreviewFastSeek', 'Run')
        ,@('VerifyCoordinatorEventNavigation', 'Run')
        ,@('VerifyCoordinatorEventNavigation', 'RealWave')
    }
    Gameplay {
        ,@('VerifyCatalogAbilitiesAndDamage', 'Run')
        ,@('VerifyHeavyExtraHealth', 'Run')
        ,@('VerifyUnitRegeneration', 'Run')
        ,@('VerifyDebuffDamageRouting', 'Run')
        ,@('VerifyTeamBattleLevel', 'Run')
        ,@('VerifyBlackHoleRegions', 'Run')
    }
    Integration {
        ,@('VerifyExpandedNumericRandomization', 'Run')
        ,@('VerifyWavePhaseDeathRegression', 'Run')
        ,@('VerifyWaveAuditRegressions', 'Run')
        ,@('VerifyWavePreviewSnapshotStress', 'WavePreviewSnapshotStressChecks.Run')
    }
})
$auditResults = @()
foreach ($auditCase in $auditCases) {
    $auditName, $auditMethod = $auditCase
    $auditFile = "AgentScripts/$auditName.cs"
    $auditEntry = if ($auditMethod.Contains('.')) { $auditMethod } else { "$auditName.$auditMethod" }
    $auditLog = "AgentArtifacts/technical-audit-$auditName-$auditMethod.json"
    $auditRaw = & unity command run_script --file $auditFile --entry $auditEntry --timeout_ms 60000 --format json
    $auditExit = $LASTEXITCODE
    $auditRaw | Set-Content -LiteralPath $auditLog -Encoding utf8
    $auditResponse = $auditRaw | ConvertFrom-Json
    $auditRun = $auditResponse.data.result
    $auditPayload = $auditRun.result
    $auditPassed = $auditExit -eq 0 -and $auditResponse.success -eq $true -and $auditRun.success -eq $true
    if ($null -ne $auditPayload.failures -and $auditPayload.failures.Count -gt 0) { $auditPassed = $false }
    if ($null -ne $auditPayload.passed -and $auditPayload.passed -is [bool] -and !$auditPayload.passed) { $auditPassed = $false }
    if ($null -ne $auditPayload.success -and !$auditPayload.success) { $auditPassed = $false }
    $auditResult = [pscustomobject]@{ entry = $auditEntry; passed = $auditPassed; executeMs = $auditRun.executeMs; result = $auditPayload; diagnostics = $auditRun.diagnostics; error = $auditRun.errorDetails; errors = $auditResponse.errors; artifact = $auditLog }
    $auditResults += $auditResult
    $auditResults | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath "AgentArtifacts/technical-audit-$Group-summary.json" -Encoding utf8
    $auditResult | ConvertTo-Json -Depth 6 -Compress
    # Never start another Editor operation behind a still-running timed-out script.
    if ($auditRun.error -match 'timed out' -or ($auditResponse.errors.message -join ' ') -match 'timed out') { break }
}
if ($auditResults.Where({ !$_.passed }).Count -gt 0) { exit 1 }
