$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$auditProject = Get-Content -Raw -LiteralPath (Join-Path $auditWorkspace 'Assembly-CSharp.csproj')
$auditCore = [string]($auditProject.Project.ItemGroup.Reference | Where-Object Include -eq 'UnityEngine.CoreModule').HintPath
if (![IO.Path]::IsPathRooted($auditCore)) { $auditCore = Join-Path $auditWorkspace $auditCore }
Add-Type -Path $auditCore
function Read-AuditSource([string] $relative) { Get-Content -Raw -LiteralPath (Join-Path $auditWorkspace $relative) }

# Compile production managed logic, not a reimplementation or a Unity math shim.
# The model prefix excludes only the MonoBehaviour host and its gameplay dependencies.
$auditModels = (Read-AuditSource 'Assets/Scripts/Gameplay/Waves/WaveAttackCoordinator.cs').Split('// A scheduling client')[0]
$auditBoundaryModels = (Read-AuditSource 'Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs').Split('public enum EnemyChildAttackOffsetSpace')[0]
$auditExisting = Read-AuditSource 'AgentScripts/VerifyWaveAttackCoordinator.cs'
$auditExistingHead = $auditExisting.Substring(0, $auditExisting.IndexOf('    public static object Run()'))
$auditExistingMethods = $auditExisting.Substring($auditExisting.IndexOf('    private static WaveAttackCoordinatorGroup Group('))
$auditExistingMethods = $auditExistingMethods.Substring(0, $auditExistingMethods.IndexOf('    private static void Preview()'))
# JsonUtility requires the native Editor. Its round-trip stays in the original Editor suite.
$auditExistingMethods = [regex]::Replace($auditExistingMethods, '        var restored = JsonUtility[^\r\n]*\r?\n        Check\(restored[^\r\n]*\r?\n', '')
$auditExistingPure = $auditExistingHead + $auditExistingMethods + 'public static int RunPure() { checks = 0; Pure(); return checks; } }'
$auditSources = @(
    $auditModels,
    $auditBoundaryModels,
    (Read-AuditSource 'Assets/Scripts/Gameplay/Waves/WaveAttackPermissions.cs'),
    (Read-AuditSource 'Assets/Scripts/Gameplay/Waves/WaveAttackCoordinatorPlayer.cs'),
    (Read-AuditSource 'Assets/Scripts/Gameplay/Enemies/EnemyAttackClock.cs'),
    (Read-AuditSource 'Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundary.cs'),
    $auditExistingPure,
    (Read-AuditSource 'AgentScripts/VerifyPortableWaveAudit.cs')
)
$auditImports = "using System;`nusing System.Collections;`nusing System.Collections.Generic;`nusing System.Reflection;`nusing UnityEngine;`n"
$auditCombined = $auditImports + (($auditSources | ForEach-Object { [regex]::Replace($_, '(?m)^using [^\r\n]+;\r?\n', '') }) -join "`n")
$auditReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName) + $auditCore
Add-Type -TypeDefinition $auditCombined -ReferencedAssemblies $auditReferences -CompilerOptions '/nowarn:1701'
$auditResult = [VerifyPortableWaveAudit]::Run()
$auditResult | ConvertTo-Json -Depth 6 | Tee-Object -FilePath (Join-Path $auditWorkspace 'AgentArtifacts/technical-audit-portable.json')
if ($auditResult.failures.Count -gt 0) { exit 1 }
