param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$formalSources = @(
    'Assets/HabitatIsland/Scripts/HabitatModel.cs',
    'Assets/HabitatIsland/Scripts/HabitatJourneyRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatChapterRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatTutorialRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatLevels.cs',
    'Assets/HabitatIsland/Scripts/HabitatFigJamLevel.cs',
    'Assets/HabitatIsland/Scripts/HabitatMechanicLevels.cs',
    'Assets/HabitatIsland/Scripts/HabitatMountainLevel.cs',
    'Assets/HabitatIsland/Scripts/HabitatModelChecks.cs',
    'Assets/HabitatIsland/Scripts/HabitatAuthoredRuleChecks.cs',
    'Assets/HabitatIsland/Scripts/HabitatJourneyChecks.cs',
    'Assets/HabitatIsland/Scripts/HabitatLevelAccessChecks.cs',
    'AgentScripts/TutorialRuleChecks.cs',
    'AgentScripts/ChapterRuleChecks.cs',
    'AgentScripts/FormalLevelChecks.cs'
) | ForEach-Object { Join-Path $ProjectRoot $_ }
$formalReportDirectory = Join-Path $ProjectRoot 'Logs/formal-levels-20261016'
New-Item -ItemType Directory -Path $formalReportDirectory -Force | Out-Null
$formalReport = [ordered]@{
    utc = [DateTime]::UtcNow.ToString('o')
    runner = 'PowerShell Add-Type; pure C#; no Unity assembly, editor, or executable build'
    scope = 'Six source-map layouts and selected legal model routes. Shell, basket, carried-shrimp edge cases and shared commission checkpoints use explicitly synthetic states. All six formal shops enforce fan lock and price. Not full campaign or all-island completion.'
    sourceHashes = @($formalSources | ForEach-Object { [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
    status = 'FAIL'
    summary = ''
    regression = @()
    passed = @()
    routes = @()
}
try {
    Add-Type -Path $formalSources
    $formalReport.summary = [FormalLevelChecks]::Run()
    $formalReport.regression = @(
        [HabitatIsland.HabitatModelChecks]::Run(),
        [HabitatIsland.HabitatAuthoredRuleChecks]::Run(),
        [HabitatIsland.HabitatJourneyChecks]::Run(),
        [HabitatIsland.HabitatLevelAccessChecks]::Run(),
        [TutorialRuleChecks]::Run(),
        [ChapterRuleChecks]::Run()
    )
    $formalReport.status = 'PASS'
} catch {
    $formalReport.summary = $_.Exception.Message
    throw
} finally {
    if ('FormalLevelChecks' -as [type]) {
        $formalReport.passed = @([FormalLevelChecks]::Passed.ToArray())
        $formalReport.routes = @([FormalLevelChecks]::Routes.ToArray())
    }
    $formalReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $formalReportDirectory 'rules.json') -Encoding utf8
    @($formalReport.summary) + @($formalReport.regression) + @($formalReport.routes) | Set-Content -LiteralPath (Join-Path $formalReportDirectory 'rules.txt') -Encoding utf8
}
$formalReport.summary
$formalReport.regression
