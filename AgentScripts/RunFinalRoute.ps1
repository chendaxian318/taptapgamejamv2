param([int]$Level=6,[string]$Plan='Verification/final-acceptance-20261018/routes/6.plan')
$ErrorActionPreference='Stop'
$src=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_.ToString()+'.cs'}
Add-Type -Path ($src+@('AgentScripts/FinalRouteLab.cs'))
$result=[FinalRouteLab]::Run($Level,$Plan)
$result
[FinalRouteLab]::Dump()
[FinalRouteLab]::S | ConvertTo-Json -Depth 12 | Set-Content ('Verification/final-acceptance-20261018/routes/'+$Level+'-state.json')
@([FinalRouteLab]::R.ToArray()) | ConvertTo-Json -Depth 5 | Set-Content ('Verification/final-acceptance-20261018/routes/'+$Level+'-route.json')
$result | Set-Content ('Verification/final-acceptance-20261018/routes/'+$Level+'-result.txt')
