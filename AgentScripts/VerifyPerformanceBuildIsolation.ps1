$ErrorActionPreference='Stop'
# Add-Type loads PowerShell's supported Roslyn parser; no Unity source is compiled or changed.
Add-Type -TypeDefinition 'internal static class PerformanceSuiteParserBootstrap {}'
$symbols=[string[]]@('UNITY_ANDROID','ENABLE_IL2CPP','UNITY_6000_3_OR_NEWER')
$options=[Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithPreprocessorSymbols($symbols)
$verified=[Collections.Generic.List[string]]::new()
foreach($file in Get-ChildItem -LiteralPath 'Assets/PerformanceSuite/Runtime' -Filter '*.cs'){
    $tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText((Get-Content -Raw -LiteralPath $file.FullName),$options)
    foreach($node in $tree.GetRoot().DescendantNodes()){
        if($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax]){throw "Suite type survives ordinary Android preprocessing: $($file.Name)"}
    }
    $verified.Add($file.Name)
}
$hooks=@('Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.cs','Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileCollisionRegistry.cs','Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs','Assets/Scripts/Gameplay/Waves/DirectedWaveAttackBehaviour.cs','Assets/Scripts/Gameplay/Waves/WaveAttackCoordinator.cs')
foreach($file in $hooks){
    $tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText((Get-Content -Raw -LiteralPath $file),$options)
    foreach($node in $tree.GetRoot().DescendantNodes()){
        if($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -or $node -is [Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax] -or $node -is [Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax]){
            if($node.Identifier.ValueText -match 'Performance'){throw "Suite hook survives ordinary Android preprocessing: $file / $($node.Identifier.ValueText)"}
        }
    }
    $verified.Add($file)
}
$result=[pscustomobject]@{passed=$true;files=@($verified);profile='Android IL2CPP without UNITY_EDITOR and ANDROID_PERFORMANCE_SUITE';note='Roslyn preprocessor verification, not a substitute for Player build.'}
$result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath 'AgentArtifacts/Performance/normal-build-exclusion.json' -Encoding utf8
$result|ConvertTo-Json -Depth 4
