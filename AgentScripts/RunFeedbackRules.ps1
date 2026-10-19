$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $names=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')
    Add-Type -Path (@($names|ForEach-Object {'Assets/HabitatIsland/Scripts/'+$_+'.cs'})+@('AgentScripts/FeedbackRulesChecks.cs'))
    $result=[FeedbackRulesChecks]::Run()
    @($result)+[FeedbackRulesChecks]::Evidence | Set-Content -LiteralPath Verification/feedback-20261018-140142/rules.txt
    $result
} finally { Pop-Location }
