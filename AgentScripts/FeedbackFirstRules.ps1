$ErrorActionPreference='Stop'
$src=@('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel')|ForEach-Object{'Assets/HabitatIsland/Scripts/'+$_+'.cs'}
Add-Type -Path ($src+@('AgentScripts/FinalRouteLab.cs'))
[FinalRouteLab]::Begin(5);[FinalRouteLab]::S=[HabitatIsland.HabitatLevels]::Create(5)
foreach($line in Get-Content Verification/feedback-221708/routes/5.plan){$a=$line.Split(' ');if($a[0] -eq 'W'){[FinalRouteLab]::W([int]$a[1],[int]$a[2])}else{[FinalRouteLab]::A([int[]]$a[1..($a.Length-1)])}}
$won=[HabitatIsland.HabitatMechanicLevels]::CompleteHabitat([FinalRouteLab]::S,[FinalRouteLab]::T)
"PASS=$won actions=$([FinalRouteLab]::R.Count) rod=$([FinalRouteLab]::S.specialRod)"|Tee-Object Verification/feedback-221708/routes/5-result.txt
@([FinalRouteLab]::R.ToArray())|ConvertTo-Json -Depth 5|Set-Content Verification/feedback-221708/routes/5-route.json
