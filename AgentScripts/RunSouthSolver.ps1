$ErrorActionPreference='Stop'
$src=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_+'.cs'}
Add-Type -Path ($src+@('AgentScripts/FinalRouteLab.cs','AgentScripts/FeedbackSouthSolver.cs'))
$plan=Get-Content Verification/feedback-221708/routes/6.plan
$i=[Array]::IndexOf($plan,'W 9 4');$plan[0..($i-1)]+@('W 4 5','I 3 5')|Set-Content Verification/feedback-221708/routes/6-south-start.plan
[FinalRouteLab]::Run(6,'Verification/feedback-221708/routes/6-south-start.plan')
[FinalRouteLab]::Dump()
$result=[FeedbackSouthSolver]::Run([FinalRouteLab]::S);$result
if($result.StartsWith('PASS')){@([FinalRouteLab]::R.ToArray())+@([FeedbackSouthSolver]::Route.ToArray())|ConvertTo-Json -Depth 5|Set-Content Verification/feedback-221708/routes/6-route.json}
