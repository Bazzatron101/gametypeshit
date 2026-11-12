<#
End-to-end co-op check on one PC: runs the built co-op player twice, headless, as a host and
a client over local UDP (Fish-Net Tugboat). The host puts out one tough target; both players
walk up to it and attack it, then stand still; each copy writes what it saw. Passes when the
client joined, both see two players and the same target, both players' damage reached both
copies, the client was shown its own damage exactly as the host counted it (once each), and the
client's own position matches the host's record of it.

  powershell -File scripts/coop-local-test.ps1            # uses Builds/Coop/HunterSystem.exe
  powershell -File scripts/coop-local-test.ps1 -Seconds 20

Build first with: Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.CoopPlayerBuild.Build
#>
param([int]$Seconds = 16)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $project 'Builds/Coop/HunterSystem.exe'
if (-not (Test-Path $exe)) { throw "No co-op build at $exe. Build it first." }

$out = Join-Path $project 'TestResults/coop'
New-Item -ItemType Directory -Force $out | Out-Null
$hostJson = Join-Path $out 'host.json'
$clientJson = Join-Path $out 'client.json'
Remove-Item $hostJson, $clientJson -ErrorAction SilentlyContinue

# Windows PowerShell's Start-Process does not quote array arguments, and the project path has a space.
function Q([string]$path) { '"' + $path + '"' }
$common = @('-batchmode', '-nographics', '-transport', 'local')
$hostArgs = $common + @('-coop', 'host', '-autotest', ($Seconds + 4), (Q $hostJson), '-logFile', (Q (Join-Path $out 'host.log')))
$clientArgs = $common + @('-coop', 'join', '-address', '127.0.0.1', '-player-key', 'local-test', '-autotest', $Seconds, (Q $clientJson), '-logFile', (Q (Join-Path $out 'client.log')))

$hostProcess = Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru
Start-Sleep -Seconds 2
$clientProcess = Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru
$deadline = (Get-Date).AddSeconds($Seconds + 40)
while ((-not $hostProcess.HasExited -or -not $clientProcess.HasExited) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
foreach ($p in $hostProcess, $clientProcess) { if (-not $p.HasExited) { $p.Kill(); Write-Host "Killed a copy that did not finish (pid $($p.Id))." } }

if (-not (Test-Path $hostJson) -or -not (Test-Path $clientJson)) { Write-Host "Missing results; see the logs in $out"; exit 1 }
$h = Get-Content $hostJson -Raw | ConvertFrom-Json
$c = Get-Content $clientJson -Raw | ConvertFrom-Json
Write-Host "HOST:   players=$($h.players) clients=$($h.connectedClients) enemies=$($h.enemies) snapshot=$($h.lastSnapshotBytes) bytes"
Write-Host "CLIENT: joined=$($c.joined) id=$($c.localPlayerId) players=$($c.players) enemies=$($c.enemies) defeated=$($c.enemiesDefeated) correction=$([math]::Round($c.largestCorrection, 3)) m unconfirmed=$($c.unconfirmedCommands)"

$hostView = $h.playerPositions | Where-Object { $_ -like "$($c.localPlayerId):*" } | Select-Object -First 1
if (-not $hostView) { Write-Host "FAIL: the host has no player $($c.localPlayerId)"; exit 1 }
$xy = ($hostView -split ':')[1] -split ','
$dx = [math]::Abs([double]$xy[0] - $c.localX); $dz = [math]::Abs([double]$xy[1] - $c.localZ)
Write-Host "Client at ($([math]::Round($c.localX,3)), $([math]::Round($c.localZ,3))); host has it at ($($xy[0]), $($xy[1]))"

function Damage($result, [int]$player) {
    $entry = $result.damageByPlayer | Where-Object { $_ -like "${player}:*" } | Select-Object -First 1
    if ($entry) { [int](($entry -split ':')[1]) } else { 0 }
}
$hostId = 1; $clientId = $c.localPlayerId
Write-Host "Damage to target $($h.targetId) as the host saw it: host player $(Damage $h $hostId), client player $(Damage $h $clientId)"
Write-Host "Damage to target $($c.targetId) as the client saw it: host player $(Damage $c $hostId), client player $(Damage $c $clientId)"

$checks = [ordered]@{
    'client joined, two players each side'      = $c.joined -and $h.players -eq 2 -and $c.players -eq 2
    'one shared target'                         = $h.targetId -gt 0 -and $h.targetId -eq $c.targetId
    'both players damaged it (host)'            = (Damage $h $hostId) -gt 0 -and (Damage $h $clientId) -gt 0
    'client shown the host player hitting it'   = (Damage $c $hostId) -gt 0
    'client shown its own hits once each'       = (Damage $c $clientId) -eq (Damage $h $clientId)
    'prediction matches the host'               = $dx -lt 0.05 -and $dz -lt 0.05
}
$failed = @($checks.Keys | Where-Object { -not $checks[$_] })
foreach ($k in $checks.Keys) { Write-Host ("  [{0}] {1}" -f ($(if ($checks[$k]) { 'ok' } else { 'FAIL' })), $k) }
if ($failed.Count -eq 0) { Write-Host "PASS: two copies fought one enemy together; the host's damage and the client's prediction agree."; exit 0 }
Write-Host "FAIL"; exit 1
