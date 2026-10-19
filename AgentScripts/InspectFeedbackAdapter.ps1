param([int]$Level=7,[string]$RouteName='third')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
$names=@('HabitatModel.cs','HabitatJourneyRules.cs','HabitatChapterRules.cs','HabitatTutorialRules.cs','HabitatLevels.cs','HabitatFigJamLevel.cs','HabitatMechanicLevels.cs','HabitatMountainLevel.cs')
Add-Type -Path (@($names|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_})+@($names|ForEach-Object{'Verification/feedback-20261018-140142/legacy-code/'+$_})+@('AgentScripts/FeedbackRouteInspector.cs'))
[int[][]]$route=Get-Content -LiteralPath ('Verification/release-playtest-20261018/'+$RouteName+'-route.json') -Raw|ConvertFrom-Json
try{$result=[FeedbackRouteInspector]::Run($Level,$route);$result;if($result -like 'PASS*'){[FeedbackRouteInspector]::Route.ToArray()|ConvertTo-Json -Depth 5|Set-Content -LiteralPath ('Verification/feedback-20261018-140142/'+$RouteName+'-route.json') -Encoding utf8}}catch{$result=$_.ToString();Write-Output $result}
# $result|Set-Content -LiteralPath ('Verification/feedback-20261018-140142/'+$RouteName+'-adapter-result.txt') -Encoding utf8
