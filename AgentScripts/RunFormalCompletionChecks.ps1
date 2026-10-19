$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $sources=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel') | ForEach-Object { 'Assets/HabitatIsland/Scripts/'+$_+'.cs' }
    Add-Type -Path ($sources + 'AgentScripts/FormalCompletionChecks.cs')
    $result=@([FormalCompletionChecks]::Run())
    $result += [FormalCompletionChecks]::ThirdRoute()
    [FormalCompletionChecks]::Route | ConvertTo-Json -Depth 4 | Set-Content Verification/formal-completion-20261016/third-route.json
    $result += [FormalCompletionChecks]::FourthRoute()
    [FormalCompletionChecks]::Route | ConvertTo-Json -Depth 4 | Set-Content Verification/formal-completion-20261016/fourth-route.json
    $result + [FormalCompletionChecks]::Evidence | Set-Content Verification/formal-completion-20261016/rules.txt
    $result
} finally { Pop-Location }
