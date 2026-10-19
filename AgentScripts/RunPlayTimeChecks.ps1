$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $sources=@('HabitatModel','HabitatPlayClock','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel') | ForEach-Object { 'Assets/HabitatIsland/Scripts/'+$_+'.cs' }
    Add-Type -Path ($sources + 'AgentScripts/PlayTimeChecks.cs')
    $result=[PlayTimeChecks]::Run()
    @($result)+[PlayTimeChecks]::Evidence | Set-Content -LiteralPath 'Verification/latest-plan-20261018/play-time.txt' -Encoding utf8
    $result
} finally { Pop-Location }
