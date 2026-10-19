$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
$taskDir='Verification/feedback-20261018-140142/legacy-code'
New-Item -ItemType Directory -Path $taskDir -Force|Out-Null
$names=@('HabitatModel.cs','HabitatJourneyRules.cs','HabitatChapterRules.cs','HabitatTutorialRules.cs','HabitatLevels.cs','HabitatFigJamLevel.cs','HabitatMechanicLevels.cs','HabitatMountainLevel.cs')
foreach($n in $names){$src=if(Test-Path -LiteralPath ('ChangeBackups/20261018_Feedback140142/'+$n)){'ChangeBackups/20261018_Feedback140142/'+$n}else{'Assets/HabitatIsland/Scripts/'+$n};(Get-Content -LiteralPath $src -Raw).Replace('HabitatIsland','LegacyHabitatIsland')|Set-Content -LiteralPath ($taskDir+'/'+$n) -Encoding utf8}
Add-Type -Path @($names|ForEach-Object{$taskDir+'/'+$_})
$s=[LegacyHabitatIsland.HabitatLevels]::Create(5)
$t=[LegacyHabitatIsland.Tuning]::new();$t.animalMovement=$false
$route=Get-Content -LiteralPath 'Verification/release-playtest-20261018/first-route.json' -Raw|ConvertFrom-Json
$i=0
foreach($a in $route){$i++;$old=$s.carryingProp;$p="$($s.playerX),$($s.playerZ)";$target=if($a[0] -eq 1){[LegacyHabitatIsland.HabitatRules]::TileAt($s,$a[1],$a[2]).prop}else{''}
$ok=switch($a[0]){0{[LegacyHabitatIsland.HabitatRules]::MovePlayer($s,$a[1],$a[2],$true,$t)}1{[LegacyHabitatIsland.HabitatRules]::Interact($s,$a[1],$a[2])}default{$true}}
if($a[0] -eq 1){"$i P=$p target=$($a[1]),$($a[2]) $target held=$old -> $($s.carryingProp) $($s.message)"}
if(!$ok){throw "step$i $($s.message)"}}
