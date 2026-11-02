$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $auditWorkspace
# Read-only integrity check; this does not replace AssetDatabase/Prefab API validation.
$auditGuidPaths = @{}
$auditDuplicates = [Collections.Generic.List[object]]::new()
$auditMetaFiles = @(rg --files --no-ignore Assets Packages Library/PackageCache -g '*.meta')
foreach ($auditMeta in $auditMetaFiles) {
    $auditMatch = [regex]::Match([IO.File]::ReadAllText((Join-Path $auditWorkspace $auditMeta)), '(?m)^guid: ([0-9a-f]{32})\s*$')
    if (!$auditMatch.Success) { continue }
    $auditGuid = $auditMatch.Groups[1].Value
    $auditAsset = $auditMeta.Substring(0, $auditMeta.Length - 5).Replace('\','/')
    if (!$auditGuidPaths.ContainsKey($auditGuid)) { $auditGuidPaths[$auditGuid] = $auditAsset }
    elseif ($auditAsset.StartsWith('Assets/') -and $auditGuidPaths[$auditGuid].StartsWith('Assets/')) {
        $auditDuplicates.Add(@{guid=$auditGuid; first=$auditGuidPaths[$auditGuid]; second=$auditAsset})
    }
}
$auditQueue = [Collections.Generic.Queue[string]]::new()
$auditSeeds = @('Assets/Resources/ProjectContext.prefab', 'Assets/Scenes/Fighting/Fighting.unity', 'Assets/Scenes/NewMainMenu.unity')
$auditSeeds += @(rg --files Assets/Waves/1 Assets/Waves/2 -g '1_[1-4]_extrime.prefab' -g '2_[1-3]_extrime.prefab')
$auditSeeds += @(rg --files Assets/GameContent/Catalogs -g '*.asset')
foreach ($auditSeed in $auditSeeds) { $auditQueue.Enqueue($auditSeed.Replace('\','/')) }
$auditVisited = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$auditMissing = [Collections.Generic.List[object]]::new()
$auditMissingScripts = [Collections.Generic.List[string]]::new()
$auditReferenceCount = 0
while ($auditQueue.Count -gt 0 -and $auditVisited.Count -lt 2000) {
    $auditPath = $auditQueue.Dequeue()
    if (!$auditVisited.Add($auditPath)) { continue }
    $auditText = [IO.File]::ReadAllText((Join-Path $auditWorkspace $auditPath))
    if ($auditText -match 'm_Script: \{fileID: 0\}') { $auditMissingScripts.Add($auditPath) }
    foreach ($auditMatch in [regex]::Matches($auditText, 'guid: ([0-9a-f]{32})')) {
        $auditGuid = $auditMatch.Groups[1].Value
        if ($auditGuid -match '^0{16}') { continue }
        $auditReferenceCount++
        if (!$auditGuidPaths.ContainsKey($auditGuid)) {
            $auditStart = $auditText.LastIndexOf("`n", $auditMatch.Index) + 1
            $auditEnd = $auditText.IndexOf("`n", $auditMatch.Index)
            if ($auditEnd -lt 0) { $auditEnd = $auditText.Length }
            $auditProperty = $auditText.Substring($auditStart, $auditEnd - $auditStart).Trim()
            $auditMissing.Add(@{source=$auditPath; guid=$auditGuid; property=$auditProperty})
            if ($auditProperty.StartsWith('m_Script:')) { $auditMissingScripts.Add($auditPath + ': ' + $auditGuid) }
            continue
        }
        $auditTarget = $auditGuidPaths[$auditGuid]
        if ($auditTarget.StartsWith('Assets/') -and $auditTarget -match '\.(prefab|asset|mat|controller)$' -and !$auditVisited.Contains($auditTarget)) {
            $auditQueue.Enqueue($auditTarget)
        }
    }
}
$auditResult = [ordered]@{
    checkedFiles=$auditVisited.Count; references=$auditReferenceCount; metadataFiles=$auditMetaFiles.Count
    traversalComplete=($auditQueue.Count -eq 0); duplicateProjectGuids=@($auditDuplicates.ToArray())
    missingScripts=@($auditMissingScripts.ToArray()); unresolvedReferences=@($auditMissing.ToArray())
    scope='Seven extrime waves, catalogs, ProjectContext, Fighting/NewMainMenu and their referenced serialized assets; read-only filesystem scan'
    limitation='Checks GUID existence, not fileID validity, importer results, DI resolution or native scene lifecycle'
}
$auditResult | ConvertTo-Json -Depth 6 | Tee-Object -FilePath (Join-Path $auditWorkspace 'AgentArtifacts/technical-audit-serialized-references.json')
if ($auditDuplicates.Count -gt 0 -or $auditMissingScripts.Count -gt 0 -or $auditMissing.Count -gt 0 -or $auditQueue.Count -gt 0) { exit 1 }
