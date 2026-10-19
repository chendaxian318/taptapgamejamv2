param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$chapterSources = @(
    'Assets/HabitatIsland/Scripts/HabitatModel.cs',
    'Assets/HabitatIsland/Scripts/HabitatJourneyRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatChapterRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatTutorialRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatLevels.cs',
    'Assets/HabitatIsland/Scripts/HabitatFigJamLevel.cs',
    'Assets/HabitatIsland/Scripts/HabitatModelChecks.cs',
    'Assets/HabitatIsland/Scripts/HabitatAuthoredRuleChecks.cs',
    'Assets/HabitatIsland/Scripts/HabitatJourneyChecks.cs',
    'AgentScripts/TutorialRuleChecks.cs',
    'AgentScripts/ChapterRuleChecks.cs'
) | ForEach-Object { Join-Path $ProjectRoot $_ }
$chapterReportDirectory = Join-Path $ProjectRoot 'Logs/plan-mainline-20261016'
New-Item -ItemType Directory -Path $chapterReportDirectory -Force | Out-Null
$chapterReport = [ordered]@{
    utc = [DateTime]::UtcNow.ToString('o')
    runner = 'PowerShell Add-Type; pure C#; no Unity assembly or editor call'
    sourceHashes = @($chapterSources | ForEach-Object { [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
    status = 'FAIL'
    summary = ''
    regression = @()
    passed = @()
}
try {
    Add-Type -Path $chapterSources
    $chapterReport.summary = [ChapterRuleChecks]::Run()
    $chapterReport.passed = @([ChapterRuleChecks]::Passed.ToArray())
    $chapterReport.regression = @([HabitatIsland.HabitatModelChecks]::Run(), [HabitatIsland.HabitatAuthoredRuleChecks]::Run(), [HabitatIsland.HabitatJourneyChecks]::Run(), [TutorialRuleChecks]::Run())
    $chapterReport.status = 'PASS'
} catch {
    $chapterReport.summary = $_.Exception.Message
    if ('ChapterRuleChecks' -as [type]) { $chapterReport.passed = @([ChapterRuleChecks]::Passed.ToArray()) }
    throw
} finally {
    $chapterReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $chapterReportDirectory 'chapter-rules.json') -Encoding utf8
    @($chapterReport.summary) + @($chapterReport.regression) | Set-Content -LiteralPath (Join-Path $chapterReportDirectory 'chapter-rules.txt') -Encoding utf8
}
$chapterReport.summary
$chapterReport.regression
