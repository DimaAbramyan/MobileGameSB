param([ValidateSet('before','after')][string]$Phase)
$ErrorActionPreference = 'Stop'
foreach ($index in 0..3) {
    $variants = if ($Phase -eq 'before') { @('profile','profile') } else { @('warmup','profile','profile','profile','alloc') }
    $repeat = 0
    foreach ($variant in $variants) {
        $repeat++
        $arguments = '[' + $index + ',"' + $Phase + '","' + $variant + '",' + $repeat + ']'
        $output = "AgentArtifacts/behaviour-cache-$Phase-$index-$variant-$repeat-cli.json"
        unity command run_script --file AgentScripts/ProfileWaveFastSeek.cs --entry ProfileWaveFastSeek.CacheBenchmark --args $arguments --timeout_ms 240000 --timeout 250 --format json > $output
        if ($LASTEXITCODE -ne 0) { throw "CLI failed: $output" }
        $response = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
        if (-not $response.data.result.success) { throw ($response.data.result | ConvertTo-Json -Depth 5) }
        $response.data.result.result | ConvertTo-Json -Compress
    }
}
