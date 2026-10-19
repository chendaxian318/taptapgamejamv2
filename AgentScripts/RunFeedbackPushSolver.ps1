$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$names=@('HabitatModel.cs','HabitatJourneyRules.cs','HabitatChapterRules.cs','HabitatTutorialRules.cs','HabitatLevels.cs','HabitatFigJamLevel.cs','HabitatMechanicLevels.cs','HabitatMountainLevel.cs')
Add-Type -Path (@($names|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_})+@('AgentScripts/FeedbackPushSolver.cs'))
$result=[FeedbackPushSolver]::Run()
$result|Set-Content -LiteralPath 'Verification/feedback-20261018-140142/first-route-result.txt' -Encoding utf8
if($result -like 'PASS*'){[FeedbackPushSolver]::Route.ToArray()|ConvertTo-Json -Depth 5|Set-Content -LiteralPath 'Verification/feedback-20261018-140142/first-route.json' -Encoding utf8}
$result
