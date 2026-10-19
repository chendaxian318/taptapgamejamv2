$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $sources=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel') | ForEach-Object { 'Assets/HabitatIsland/Scripts/'+$_+'.cs' }
    Add-Type -Path ($sources + @('AgentScripts/LatestPlanMapChecks.cs','AgentScripts/SecondDefaultMovementChecks.cs'))
    $outDir='Verification/latest-plan-20261018'
    $results=@([LatestPlanMapChecks]::SecondDefaultRoute($false))
    $single=[LatestPlanMapChecks]::Route | ConvertTo-Json -Depth 3 -Compress
    $results+=[LatestPlanMapChecks]::SecondDefaultRoute($true)
    $segmented=[LatestPlanMapChecks]::Route | ConvertTo-Json -Depth 3 -Compress
    if($single -ne $segmented){throw 'Single and segmented tick searches generated different routes.'}
    [LatestPlanMapChecks]::Route | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($outDir+'/second-default-route.json') -Encoding utf8
    [int[][]]$commands=@(Get-Content -LiteralPath ($outDir+'/second-default-route.json') -Raw | ConvertFrom-Json | ForEach-Object { ,([int[]]$_) })
    $results+=[SecondDefaultMovementChecks]::Compare($commands)
    $results+[LatestPlanMapChecks]::Evidence | Set-Content -LiteralPath ($outDir+'/second-default-movement.txt') -Encoding utf8
    ($sources + @('AgentScripts/LatestPlanMapChecks.cs','AgentScripts/SecondDefaultMovementChecks.cs')) | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} } | ConvertTo-Json | Set-Content -LiteralPath ($outDir+'/second-default-source-hashes.json') -Encoding utf8
    $results
} finally { Pop-Location }
