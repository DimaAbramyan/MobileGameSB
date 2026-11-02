$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$auditProject = Get-Content -Raw -LiteralPath (Join-Path $auditWorkspace 'Assembly-CSharp.csproj')
$auditCore = [string]($auditProject.Project.ItemGroup.Reference | Where-Object Include -eq 'UnityEngine.CoreModule').HintPath
if (![IO.Path]::IsPathRooted($auditCore)) { $auditCore = Join-Path $auditWorkspace $auditCore }
$auditGame = Join-Path $auditWorkspace 'Temp/bin/Debug/Assembly-CSharp.dll'
if (!(Test-Path -LiteralPath $auditGame)) { throw 'Build Assembly-CSharp.csproj before running the managed audit.' }
Add-Type -Path $auditCore
[void][Reflection.Assembly]::LoadFrom($auditGame)
[void][Reflection.Assembly]::LoadFrom((Join-Path $auditWorkspace 'Temp/bin/Debug/Assembly-CSharp-Editor.dll'))
$auditDependencies = @($auditProject.Project.ItemGroup.Reference | Where-Object { $_.Include -in @('Unity.Mathematics', 'Unity.Entities', 'Unity.Collections', 'Unity.Burst', 'Unity.Transforms') } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $auditWorkspace $_.HintPath)) })
foreach ($auditDependency in $auditDependencies) { [void][Reflection.Assembly]::LoadFrom($auditDependency) }
$auditReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName) + $auditCore + $auditGame + $auditDependencies
$auditLaserSource = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'VerifyEnemyLaser.cs')
$auditGeometry = $auditLaserSource.Substring($auditLaserSource.IndexOf('    private static void Geometry()'))
$auditGeometry = $auditGeometry.Substring(0, $auditGeometry.IndexOf('    private static void Timing()'))
# Quaternion.Euler is a native call. This changes only fixture direction construction,
# preserving the same 32 directions; production geometry and expectations are unchanged.
$auditGeometry = $auditGeometry.Replace('Quaternion.Euler(0, 0, i * 360f / 32) * Vector3.up', 'new Vector2(-Mathf.Sin(i * 2f * Mathf.PI / 32), Mathf.Cos(i * 2f * Mathf.PI / 32))')
$auditGeometry = 'public static class VerifyExistingLaserGeometry { private static int checks, scenarios; private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); } private static void Near(float value, float expected, string message) => Check(Mathf.Abs(value - expected) < .002f, message); public static int Run() { Geometry(); return checks; }' + $auditGeometry + '}'
$auditHarness = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'VerifyManagedWaveAudit.cs')
Add-Type -TypeDefinition ($auditHarness + $auditGeometry) -ReferencedAssemblies $auditReferences -CompilerOptions '/nowarn:1701,SYSLIB0050'
$auditResult = [VerifyManagedWaveAudit]::Run()
$auditResult | Add-Member -NotePropertyName runtimeAssemblySha256 -NotePropertyValue ((Get-FileHash -Algorithm SHA256 -LiteralPath $auditGame).Hash)
$auditResult | Add-Member -NotePropertyName execution -NotePropertyValue 'Managed .NET/Windows, production game/editor assemblies; native Unity lifecycle and Burst are not executed'
$auditResult | ConvertTo-Json -Depth 6 | Tee-Object -FilePath (Join-Path $auditWorkspace 'AgentArtifacts/technical-audit-managed.json')
if ($auditResult.failures.Count -gt 0) { exit 1 }
