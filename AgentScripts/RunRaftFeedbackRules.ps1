$ErrorActionPreference='Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
 $names=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')
 Add-Type -Path (@($names|ForEach-Object {'Assets/HabitatIsland/Scripts/'+$_+'.cs'})+@('AgentScripts/RaftFeedbackRulesChecks.cs','AgentScripts/FeedbackRulesChecks.cs'))
 try { [RaftFeedbackRulesChecks]::Run(); [FeedbackRulesChecks]::Run() } finally { @([RaftFeedbackRulesChecks]::Evidence)+@([FeedbackRulesChecks]::Evidence)|Set-Content Verification/feedback-20261018-144126/rules.txt }
} finally {Pop-Location}
