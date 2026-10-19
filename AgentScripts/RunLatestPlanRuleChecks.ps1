$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $sources=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel') | ForEach-Object { 'Assets/HabitatIsland/Scripts/'+$_+'.cs' }
    Add-Type -Path ($sources + 'AgentScripts/LatestPlanRuleChecks.cs')
    $result=[LatestPlanRuleChecks]::Run()
    New-Item -ItemType Directory -Path 'Verification/latest-plan-20261018' -Force | Out-Null
    @($result)+[LatestPlanRuleChecks]::Evidence | Set-Content -LiteralPath 'Verification/latest-plan-20261018/rules.txt' -Encoding utf8
    $result
} finally { Pop-Location }
