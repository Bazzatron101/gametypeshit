<#
Runs the Unity test suite headlessly (batch mode, no graphics).

  pwsh scripts/test.ps1                  # EditMode tests
  pwsh scripts/test.ps1 -Platform PlayMode
  pwsh scripts/test.ps1 -Filter SmokeTest

The editor is resolved from ProjectSettings/ProjectVersion.txt so the locked
version is always the one used. Override with $env:UNITY_EDITOR.
Exit code is 0 when all tests pass, non-zero otherwise.
#>
param(
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$Filter
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$editor = $env:UNITY_EDITOR
if (-not $editor) {
    $versionLine = Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    $version = ($versionLine -split ':\s*', 2)[1].Trim()
    $editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $editor)) { throw "Unity editor not found at '$editor'. Install the locked version via Unity Hub or set UNITY_EDITOR." }

$out = Join-Path $project 'TestResults'
New-Item -ItemType Directory -Force $out | Out-Null
$results = Join-Path $out "$Platform.xml"
$log = Join-Path $out "$Platform.log"
if (Test-Path $results) { Remove-Item $results }

$unityArgs = @('-batchmode', '-nographics', '-projectPath', $project,
    '-runTests', '-testPlatform', $Platform, '-testResults', $results, '-logFile', $log)
if ($Filter) { $unityArgs += @('-testFilter', $Filter) }

# Call operator quotes each argument (the project path may contain spaces);
# piping to Out-Null makes PowerShell wait for the GUI-subsystem exe to exit.
& $editor @unityArgs | Out-Null
$exitCode = $LASTEXITCODE
if (-not (Test-Path $results)) {
    Write-Host "No test results produced (exit $exitCode). See $log"
    exit 1
}
[xml]$xml = Get-Content $results
$run = $xml.'test-run'
Write-Host "$Platform tests: $($run.result) - total $($run.total), passed $($run.passed), failed $($run.failed), skipped $($run.skipped)"
$xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
    Write-Host "FAILED: $($_.fullname)"
    Write-Host "  $($_.failure.message.InnerText)"
}
exit $exitCode
