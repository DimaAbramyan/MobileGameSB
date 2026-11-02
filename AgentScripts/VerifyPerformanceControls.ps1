$ErrorActionPreference='Stop'
$project=(Get-Location).Path
function Command([string[]]$Arguments){
    $raw=& unity command @Arguments --project-path $project --format json
    if($LASTEXITCODE -ne 0){throw 'Pipeline request failed.'}
    $r=($raw -join "`n")|ConvertFrom-Json
    if(-not $r.success){throw 'Pipeline command unsuccessful.'}
    return $r.data.result
}
function StartCase {
    $r=Command @('run_script','--file','AgentScripts/VerifyAndroidPerformanceSuite.cs','--entry','VerifyAndroidPerformanceSuite.Start','--args','["ecs_500",30,1,false]')
    if(-not $r.success){throw $r.errorDetails}
}
function WaitPhase([string]$Phase){
    $deadline=[DateTime]::UtcNow.AddSeconds(90)
    while([DateTime]::UtcNow -lt $deadline){
        Start-Sleep -Seconds 1
        try{
            $p=Command @('eval','var r = UnityEngine.Object.FindFirstObjectByType<Game.Performance.PerformanceSuiteRunner>(); return r == null ? null : new { phase = r.Phase, status = r.Result == null ? null : r.Result.status, cleanup = r.Result != null && r.Result.repeats.Count > 0 && r.Result.repeats[r.Result.repeats.Count - 1].cleanupVerified, output = r.LastOutput };')
            if($p.success -and $p.result -and $p.result.phase -eq $Phase){return $p.result}
        }catch{}
    }
    throw "Timed out waiting for phase $Phase"
}
$cancel=$null;$failure=$null;$original=$null
try{
    StartCase
    $null=WaitPhase 'measure'
    $null=Command @('eval','var r = UnityEngine.Object.FindFirstObjectByType<Game.Performance.PerformanceSuiteRunner>(); r.Cancel(); return true;')
    $cancel=WaitPhase 'idle'
    if($cancel.status -ne 'cancelled' -or -not $cancel.cleanup){throw 'Cancellation did not clean up.'}
    $null=Command @('editor_stop')
    $p=Command @('eval','var c = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Performance.PerformanceSuiteConfig>(PerformanceSuiteEditor.ConfigPath); int original = c.maximumSamples; c.maximumSamples = 16; UnityEditor.EditorUtility.SetDirty(c); UnityEditor.AssetDatabase.SaveAssetIfDirty(c); return original;')
    $original=[int]$p.result
    StartCase
    $failure=WaitPhase 'idle'
    if($failure.status -ne 'failed' -or -not $failure.cleanup){throw 'Injected bounded-buffer failure did not clean up.'}
}finally{
    $null=Command @('editor_stop')
    if($null -ne $original){
        $code='var c = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Performance.PerformanceSuiteConfig>(PerformanceSuiteEditor.ConfigPath); c.maximumSamples = '+$original+'; UnityEditor.EditorUtility.SetDirty(c); UnityEditor.AssetDatabase.SaveAssetIfDirty(c); return true;'
        $null=Command @('eval',$code)
    }
}
$result=[pscustomobject]@{passed=$true;cancel=$cancel;boundedFailure=$failure;configurationRestored=$true}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath 'AgentArtifacts/Performance/controls-checks.json' -Encoding utf8
$result|ConvertTo-Json -Depth 5
