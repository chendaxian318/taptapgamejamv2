$ErrorActionPreference='Stop'
$src=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_.ToString()+'.cs'}
Add-Type -Path ($src+@('AgentScripts/FinalRouteLab.cs'))
$checks=[Collections.Generic.List[string]]::new()
function Check($ok,$description){if(!$ok){throw $description};$checks.Add('PASS '+$description)}
foreach($level in @(6,7,8,10,4,9)){
 $result=[FinalRouteLab]::Run($level,"Verification/final-acceptance-20261018/routes/$level.plan")
 Check ($result.StartsWith('PASS')) "Current level $level legal route: $result"
 Check (([FinalRouteLab]::S.animals|Where-Object{!([HabitatIsland.HabitatRules]::Evaluate([FinalRouteLab]::S,$_ ,[FinalRouteLab]::T).suitable)}).Count -eq 0) "Level $level all residents simultaneously suitable"
}
$s=[HabitatIsland.HabitatRules]::Clone([FinalRouteLab]::S)
$crab=$s.animals|Where-Object species -eq Crab
Check ([HabitatIsland.HabitatRules]::Evaluate($s,$crab,[FinalRouteLab]::T).suitable) 'New optional crab uses shore + nearby aquatic neighbor'
$s.sourceNodeId='legacy-optional'
Check (![HabitatIsland.HabitatRules]::Evaluate($s,$crab,[FinalRouteLab]::T).suitable) 'Legacy optional save retains its old species diversity rule'
[FinalRouteLab]::Run(4,'Verification/final-acceptance-20261018/routes/4.plan')|Out-Null
$s=[HabitatIsland.HabitatRules]::Clone([FinalRouteLab]::S)
$jelly=$s.animals|Where-Object species -eq Jellyfish
$jelly.x=6;$jelly.z=3
$t=[HabitatIsland.Tuning]::new();$t.minWater=1
Check (![HabitatIsland.HabitatRules]::Evaluate($s,$jelly,$t).suitable) 'New optional jellyfish rejects enclosed fish pond'
$s.sourceNodeId='legacy-optional'
Check ([HabitatIsland.HabitatRules]::Evaluate($s,$jelly,$t).suitable) 'Legacy optional jellyfish habitat remains compatible'
$checks|Set-Content Verification/final-acceptance-20261018/current-route-checks.txt
$checks
