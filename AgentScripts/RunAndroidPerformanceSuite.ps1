param(
    [string]$Adb = 'adb',
    [string]$Apk = 'AgentArtifacts/Performance/Build/AndroidPerformanceSuite.apk',
    [string]$Serial,
    [string]$Scenario = '',
    [int]$TimeoutSeconds = 1800
)
$ErrorActionPreference = 'Stop'
if ($TimeoutSeconds -lt 10 -or $TimeoutSeconds -gt 14400) { throw 'Timeout must be 10..14400 seconds.' }
if ($Scenario -and $Scenario -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid scenario ID.' }
$apkPath = (Resolve-Path -LiteralPath $Apk).Path
$packageFile = [IO.Path]::ChangeExtension($apkPath, '.package.txt')
$packageId = (Get-Content -Raw -LiteralPath $packageFile).Trim()
if ($packageId -notmatch '^[A-Za-z][A-Za-z0-9_.]*\.performancesuite$') { throw 'Refusing to install/stop anything except the isolated .performancesuite application.' }
$devices = @(& $Adb devices)
if ($LASTEXITCODE -ne 0) { throw 'ADB discovery failed.' }
if (-not $Serial) {
    $ready = @($devices | Where-Object { $_ -match '^\S+\s+device$' } | ForEach-Object { ($_ -split '\s+')[0] })
    if ($ready.Count -ne 1) { throw 'Connect/authorize exactly one device, or specify -Serial.' }
    $Serial = $ready[0]
}
$targetArgs = @('-s', $Serial)
& $Adb @targetArgs install -r $apkPath
if ($LASTEXITCODE -ne 0) { throw 'Installation failed.' }
& $Adb @targetArgs shell am force-stop $packageId
$resolved = @(& $Adb @targetArgs shell cmd package resolve-activity --brief $packageId)
$activity = @($resolved | Where-Object { $_ -match '^[A-Za-z0-9_.]+/[A-Za-z0-9_.$]+$' } | Select-Object -Last 1)
if ($activity.Count -ne 1) { throw 'Could not resolve suite activity.' }
$remoteRoot = "/sdcard/Android/data/$packageId/files/Performance"
# The isolated app writes a fresh completion manifest for each launch; pass a launch token to reject old output.
$launchToken = [Guid]::NewGuid().ToString('N')
$launchArgs = @('shell','am','start','-n',$activity[0],'--es','perfLaunchToken',$launchToken)
if ($Scenario) { $launchArgs += @('--es','perfScenario',$Scenario) }
& $Adb @targetArgs @launchArgs
if ($LASTEXITCODE -ne 0) { throw 'Application launch failed.' }
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$manifest = $null
while ([DateTime]::UtcNow -lt $deadline) {
    $raw = & $Adb @targetArgs shell run-as $packageId cat "$remoteRoot/complete.json" 2>$null
    if ($LASTEXITCODE -eq 0) {
        try {
            $candidate = ($raw -join "`n") | ConvertFrom-Json
            if ($candidate.launchToken -eq $launchToken) { $manifest = $candidate; break }
        } catch { }
    }
    Start-Sleep -Seconds 3
}
if (-not $manifest) {
    & $Adb @targetArgs shell am force-stop $packageId
    throw 'Suite timed out. Only the isolated application was stopped; inspect adb logcat.'
}
$destination = Join-Path (Get-Location) 'AgentArtifacts/Performance/Android'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
& $Adb @targetArgs pull "$remoteRoot/$($manifest.runId)" $destination
if ($LASTEXITCODE -ne 0) { throw 'Result pull failed; files remain in the isolated app Performance directory.' }
Write-Output "Android suite: $($manifest.status), results: $destination"
if ($manifest.status -ne 'completed') { exit 1 }
