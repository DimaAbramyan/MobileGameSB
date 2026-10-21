param([string]$Mode = 'isolation')
$ErrorActionPreference = 'Stop'
$cases = @()
if ($Mode -eq 'isolation') {
    foreach ($index in 0..3) { $cases += ,@($index, 'noSnapshots', 1); $cases += ,@($index, 'minimal', 1) }
    $cases += ,@(1, 'visuals', 1); $cases += ,@(1, 'visuals', 2)
} elseif ($Mode -eq 'alloc') {
    foreach ($index in 0..3) { $cases += ,@($index, 'profile', 10) }
    foreach ($index in 1..3) { $cases += ,@($index, 'alloc', 1) }
    $cases += ,@(1, 'memory', 1)
}
foreach ($case in $cases) {
    $index, $variant, $repeat = $case
    $stem = "fast-seek-profile-$index-$variant-$repeat"
    $arguments = '[' + $index + ',"' + $variant + '",' + $repeat + ']'
    unity command run_script --file AgentScripts/ProfileWaveFastSeek.cs --entry ProfileWaveFastSeek.Run --args $arguments --timeout_ms 240000 --timeout 250 --format json > "AgentArtifacts/$stem-cli.json"
    if ($LASTEXITCODE -ne 0) { throw "CLI failed: $stem" }
    $response = Get-Content -LiteralPath "AgentArtifacts/$stem-cli.json" -Raw | ConvertFrom-Json
    if (-not $response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 5) }
    $response.data.result.result | ConvertTo-Json -Compress
}
