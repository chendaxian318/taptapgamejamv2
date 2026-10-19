$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$unityCli='C:\Users\admin\AppData\Local\Unity\bin\unity.exe'
Push-Location $taskRoot
try {
 $code='typeof(HabitatIsland.HabitatGame).GetField("SaveSlotDirectoryForTests",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,System.IO.Path.GetFullPath("Verification/usability-audit-20261018/regression-saves")); return "isolated saves";'
 $isolation=(& $unityCli command eval --project-path $taskRoot --format json $code | ConvertFrom-Json)
 if(!$isolation.data.result.success){throw 'Could not isolate saves'}
 $tests=@(@('UIProbes','UsabilityUIProbes.Run'),@('InputChecks','OptimizationInputChecks.Run'),@('MouseChecks','MouseFacingChecks.Run'),@('UtilityChecks','LatestUtilityRuntimeChecks.Run'),@('RecoveryChecks','VisibleRecoveryChecks.Run'))
 foreach($test in $tests){
  $raw=& $unityCli command run_script --project-path $taskRoot --timeout 160 --format json -- --file ('AgentScripts/Usability'+$test[0]+'.cs') --entry $test[1] --timeout_ms 150000
  $raw | Set-Content -LiteralPath ('Verification/usability-audit-20261018/'+$test[0]+'.result.json') -Encoding utf8
  $result=$raw|ConvertFrom-Json
  if(!$result.success -or !$result.data.result.success -or $result.data.result.result -notlike 'PASS*'){throw ($raw -join [Environment]::NewLine)}
  Write-Output $result.data.result.result
 }
} finally {Pop-Location}
