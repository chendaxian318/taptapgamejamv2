$ErrorActionPreference='Stop'
$src=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_+'.cs'}
Add-Type -Path ($src+@('AgentScripts/FinalRouteLab.cs'))
foreach($level in @(6,7,8,10,4,9)){
 $plan='Verification/feedback-221708/routes/'+$level+'.plan'; if(!(Test-Path $plan)){$plan='Verification/final-acceptance-20261018/routes/'+$level+'.plan'}
 $result=[FinalRouteLab]::Run($level,$plan)
 "$level $result"
 $result+[Environment]::NewLine+[FinalRouteLab]::Dump()|Set-Content ('Verification/feedback-221708/routes/'+$level+'-result.txt')
 if($result.StartsWith('PASS')){@([FinalRouteLab]::R.ToArray())|ConvertTo-Json -Depth 5|Set-Content ('Verification/feedback-221708/routes/'+$level+'-route.json')}
}
