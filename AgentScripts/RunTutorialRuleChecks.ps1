param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$tutorialSources = @(
    'Assets/HabitatIsland/Scripts/HabitatModel.cs',
    'Assets/HabitatIsland/Scripts/HabitatJourneyRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatChapterRules.cs',
    'Assets/HabitatIsland/Scripts/HabitatTutorialRules.cs',
    'AgentScripts/TutorialRuleChecks.cs'
) | ForEach-Object { Join-Path $ProjectRoot $_ }
$tutorialReportDirectory = Join-Path $ProjectRoot 'Logs/tutorial-20261016'
New-Item -ItemType Directory -Path $tutorialReportDirectory -Force | Out-Null
$tutorialReport = [ordered]@{
    utc = [DateTime]::UtcNow.ToString('o')
    runner = 'PowerShell Add-Type; pure C#; no Unity assembly or editor call'
    sourceHashes = @($tutorialSources | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
    status = 'FAIL'
    summary = ''
    passed = @()
}
try {
    Add-Type -Path $tutorialSources
    $tutorialReport.summary = [TutorialRuleChecks]::Run()
    $tutorialReport.passed = @([TutorialRuleChecks]::Passed.ToArray())
    $tutorialReport.status = 'PASS'
} catch {
    $tutorialReport.summary = $_.Exception.Message
    if ('TutorialRuleChecks' -as [type]) { $tutorialReport.passed = @([TutorialRuleChecks]::Passed.ToArray()) }
    throw
} finally {
    $tutorialReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $tutorialReportDirectory 'pure-rules.json') -Encoding utf8
    $tutorialReport.summary | Set-Content -LiteralPath (Join-Path $tutorialReportDirectory 'pure-rules.txt') -Encoding utf8
}
$tutorialReport.summary
