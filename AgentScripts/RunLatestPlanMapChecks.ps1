$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $sources=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel') | ForEach-Object { 'Assets/HabitatIsland/Scripts/'+$_+'.cs' }
    Add-Type -Path ($sources + 'AgentScripts/LatestPlanMapChecks.cs')
    $outDir='Verification/latest-plan-20261018'
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $results=@([LatestPlanMapChecks]::FirstTerminal(),[LatestPlanMapChecks]::SecondTerminal(),[LatestPlanMapChecks]::ThirdTerminal(),[LatestPlanMapChecks]::FourthTerminal(),[LatestPlanMapChecks]::FifthTerminal())
    $results+[LatestPlanMapChecks]::Evidence | Set-Content -LiteralPath ($outDir+'/illustrated-endpoints.txt') -Encoding utf8
    $results+=[LatestPlanMapChecks]::FirstRoute()
    [LatestPlanMapChecks]::Route | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($outDir+'/first-route.json') -Encoding utf8
    $results+=[LatestPlanMapChecks]::SecondRoute()
    [LatestPlanMapChecks]::Route | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($outDir+'/second-route.json') -Encoding utf8
    $results+[LatestPlanMapChecks]::Evidence | Set-Content -LiteralPath ($outDir+'/map-routes.txt') -Encoding utf8
    $sources | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} } | ConvertTo-Json | Set-Content -LiteralPath ($outDir+'/route-source-hashes.json') -Encoding utf8
    $results
} finally { Pop-Location }
