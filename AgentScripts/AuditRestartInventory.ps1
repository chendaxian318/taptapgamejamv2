$ErrorActionPreference = 'Stop'
$taskProject = Split-Path $PSScriptRoot -Parent
$taskSources = @('HabitatModel','HabitatJourneyRules','HabitatChapterRules','HabitatTutorialRules','HabitatLevels','HabitatFigJamLevel','HabitatMechanicLevels','HabitatMountainLevel') | ForEach-Object { Join-Path $taskProject ('Assets/HabitatIsland/Scripts/' + $_ + '.cs') }
Add-Type -Path $taskSources
# Pure rule audit only: no Unity, player saves, scene changes or production writes.
$taskState = [HabitatIsland.HabitatLevels]::Create(6)
$taskState.coins = 200
$taskState.chapterOneDelivered = $true
$taskState.playerX = $taskState.dockX
$taskState.playerZ = $taskState.dockZ
$taskBought = [HabitatIsland.HabitatRules]::Shop($taskState, 'fan')
$taskReset = [HabitatIsland.HabitatLevels]::Create(6)
[HabitatIsland.HabitatJourneyRules]::CopySharedProgress($taskState, $taskReset)
[PSCustomObject]@{
    BoughtFan = $taskBought
    BeforeCoins = $taskState.coins
    BeforeHeld = $taskState.carryingProp.ToString()
    AfterRestartCoins = $taskReset.coins
    AfterRestartHeld = $taskReset.carryingProp.ToString()
    AfterRestartBagFan = @($taskReset.bagProps | Where-Object { $_ -eq 'Fan' }).Count
    AfterRestartMapFan = @($taskReset.tiles | Where-Object { $_.prop -eq 'Fan' }).Count
} | ConvertTo-Json
