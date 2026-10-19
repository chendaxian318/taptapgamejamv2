$ErrorActionPreference='Stop'
$src=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')|ForEach-Object{'ChangeBackups/feedback-221708/Scripts/'+$_+'.cs'}
Add-Type -Path ($src+@('AgentScripts/FinalRouteLab.cs'))
[FinalRouteLab]::Begin(5);[FinalRouteLab]::S=[HabitatIsland.HabitatLevels]::Create(5)
$plan=[Collections.Generic.List[string]]::new()
$rows=Get-Content Verification/final-acceptance-20261018/routes/first-route.json -Raw|ConvertFrom-Json
foreach($a in $rows){if($a[0] -ne 0){$plan.Add('W '+[FinalRouteLab]::S.playerX+' '+[FinalRouteLab]::S.playerZ);$plan.Add('A '+($a -join ' '))};[FinalRouteLab]::A([int[]]$a)}
$plan|Set-Content Verification/feedback-221708/routes/5.plan
