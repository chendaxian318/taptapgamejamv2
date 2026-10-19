param([string]$ProjectRoot=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $ProjectRoot
$routeSources=@('HabitatModel.cs','HabitatJourneyRules.cs','HabitatChapterRules.cs','HabitatTutorialRules.cs','HabitatLevels.cs','HabitatFigJamLevel.cs','HabitatMechanicLevels.cs','HabitatMountainLevel.cs') | ForEach-Object {Join-Path 'Assets/HabitatIsland/Scripts' $_}
Add-Type -Path ($routeSources+@('AgentScripts/LatestCampaignRouteLab.cs'))
$routeDirectory='Verification/latest-plan-20261018'
New-Item -ItemType Directory -Path $routeDirectory -Force|Out-Null
$routeReports=@()
foreach($routeIndex in 7,8,10){
    $routeName=switch($routeIndex){7{'third'}8{'fourth'}10{'fifth'}}
    $routeResult=switch($routeIndex){7{[LatestCampaignRouteLab]::Third()}8{[LatestCampaignRouteLab]::Fourth()}10{[LatestCampaignRouteLab]::Fifth()}}
    if(-not [LatestCampaignRouteLab]::State.won){throw $routeResult}
    $routeSteps=@([LatestCampaignRouteLab]::Route.ToArray())
    $routeFinal=[HabitatIsland.HabitatRules]::Clone([LatestCampaignRouteLab]::State)
    $routeReplay=[HabitatIsland.HabitatLevels]::Create($routeIndex)
    $routeReplay.rod=$true;$routeReplay.specialRod=$true;$routeReplay.backBasketUnlocked=$true
    if($routeIndex -eq 10){
        $routeReplay.coins=1150;$routeReplay.chapterOneAccepted=$true;$routeReplay.chapterOneDelivered=$true
        $routeReplay.chapterOneMorningSeen=$true;$routeReplay.campaignUnlockedStage=5
    }
    $routeInitialProps=@($routeReplay.tiles|Where-Object{$_.prop -in 'Crate','Float','ClimbBlock'}).Count
    $routeTuning=[HabitatIsland.Tuning]::new();$routeTuning.animalMovement=$false
    $routeStepNumber=0
    foreach($routeStep in $routeSteps){
        $routeStepNumber++
        $routeOkay=switch($routeStep[0]){
            0{[HabitatIsland.HabitatRules]::MovePlayer($routeReplay,$routeStep[1],$routeStep[2],$true,$routeTuning)}
            1{[HabitatIsland.HabitatRules]::Interact($routeReplay,$routeStep[1],$routeStep[2])}
            2{[HabitatIsland.HabitatRules]::Lure($routeReplay,$routeStep[1],$routeStep[2])}
            3{[HabitatIsland.HabitatRules]::Rotate($routeReplay,$routeStep[1],$routeStep[2])}
            7{[HabitatIsland.HabitatRules]::UseUtility($routeReplay,$routeStep[1],$routeStep[2],$routeStep[3],$routeStep[4],$routeStep[5])}
            8{$routeProduct=if($routeStep[1] -eq 6){'magnet'}elseif($routeStep[1] -eq 7){'launcher'}else{'crate'};[HabitatIsland.HabitatRules]::Shop($routeReplay,$routeProduct)}
            9{[HabitatIsland.HabitatRules]::TransferBoatCrate($routeReplay,$true)}
            default{throw "Unknown route action $routeStep"}
        }
        if(-not $routeOkay){throw "Replay $routeName step $routeStepNumber failed: $($routeReplay.message)"}
    }
    if(-not [HabitatIsland.HabitatMechanicLevels]::CompleteHabitat($routeReplay,$routeTuning)){throw "Replay $routeName failed final habitat"}
    $routeFinalProps=@($routeReplay.tiles|Where-Object{$_.prop -in 'Crate','Float','ClimbBlock'}).Count
    $routeBought=if($routeIndex -eq 10){1}else{0}
    if($routeFinalProps -ne $routeInitialProps+$routeBought){throw "Movable conservation failed for $routeName"}
    if($routeReplay.coins -ne $(if($routeIndex -eq 10){700}else{0})){throw "Unexpected economy for $routeName"}
    for($routeTile=0;$routeTile -lt $routeReplay.tiles.Count;$routeTile++){
        if($routeReplay.tiles[$routeTile].height -ne [HabitatIsland.HabitatLevels]::Create($routeIndex).tiles[$routeTile].height){throw "Terrain modified in $routeName"}
    }
    $routeSteps|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$routeDirectory/$routeName-route.json" -Encoding utf8
    $routeResult|Set-Content -LiteralPath "$routeDirectory/$routeName-route-result.txt" -Encoding utf8
    $routeReplay|ConvertTo-Json -Depth 12|Set-Content -LiteralPath "$routeDirectory/$routeName-route-state.json" -Encoding utf8
    $routeReports += [ordered]@{level=$routeIndex;name=$routeName;status='PASS';actions=$routeSteps.Count;independentReplay='PASS';initialMovables=$routeInitialProps;finalMovables=$routeFinalProps;purchasedCrates=$routeBought;remainingCoins=$routeReplay.coins;terrainUnchanged=$true;ecologyTicks=0;prerequisites='Inherited special rod and basket; fifth only: 1150 prior balance and real purchase of magnet300+crate150';note=$(if($routeIndex -eq 8){'Legal alternate path bypasses tide using movable climb block; not exact PDF standard path'}else{'Legal alternate route, not claimed identical to PDF schematic'})}
    Write-Output "$routeName : PASS $($routeSteps.Count) generated and independently replayed actions"
}
[ordered]@{status='PASS';runner='Pure C# via PowerShell Add-Type, no Unity controller execution';utc=[DateTime]::UtcNow.ToString('o');routes=$routeReports;sourceHashes=@($routeSources|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath "$routeDirectory/third-fourth-fifth-route-checks.json" -Encoding utf8
