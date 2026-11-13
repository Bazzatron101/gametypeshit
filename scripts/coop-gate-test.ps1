<#
A gate played over the network on one PC (ticket 17): the built co-op player runs as a bot host
that goes straight into a gate, plus bot clients over local UDP (Fish-Net Tugboat). The bots walk
the rooms, fight what appears and reach the checkpoints. Each copy writes what its own world
ended with; this script compares them.

  powershell -File scripts/coop-gate-test.ps1                      # host + 1 client, Goblin Warren
  powershell -File scripts/coop-gate-test.ps1 -Clients 2 -Seconds 60

Passes when every client joined, the host cleared the gate, and every client's own world agrees:
the same gate, cleared, with the same rooms and latest checkpoint, and it was shown the
checkpoints being reached. No copy saves anything (-no-save).

Build first with: Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.CoopPlayerBuild.Build
#>
param([int]$Seconds = 80, [int]$Clients = 1, [string]$Gate = 'goblin-warren')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $project 'Builds/Coop/HunterSystem.exe'
if (-not (Test-Path $exe)) { throw "No co-op build at $exe. Build it first." }

$out = Join-Path $project 'TestResults/gate'
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -Filter '*.json' | Remove-Item

function Q([string]$path) { '"' + $path + '"' }   # Start-Process does not quote arguments; the path has a space
$common = @('-batchmode', '-nographics', '-no-save', '-transport', 'local')
$hostJson = Join-Path $out 'host.json'
$hostArgs = $common + @('-coop', 'host', '-gate', $Gate, '-bot', '1', '-bot-expect', $Clients, '-autotest', $Seconds, (Q $hostJson), '-logFile', (Q (Join-Path $out 'host.log')))
$processes = @(Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru)
Start-Sleep -Seconds 3

for ($i = 1; $i -le $Clients; $i++) {
    $json = Join-Path $out "bot-$i.json"
    # Clients stop a little before the host, so each ends on a state the host has confirmed.
    $clientArgs = $common + @('-coop', 'join', '-address', '127.0.0.1', '-player-key', "bot-$i", '-bot', ($i + 1),
        '-autotest', ($Seconds - 6), (Q $json), '-logFile', (Q (Join-Path $out "bot-$i.log")))
    $processes += Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru
    Start-Sleep -Milliseconds 300
}

$deadline = (Get-Date).AddSeconds($Seconds + 90)
while (($processes | Where-Object { -not $_.HasExited }) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
foreach ($p in $processes) { if (-not $p.HasExited) { $p.Kill(); Write-Host "Killed a copy that did not finish (pid $($p.Id))." } }

if (-not (Test-Path $hostJson)) { Write-Host "No host result; see $out"; exit 1 }
$h = Get-Content $hostJson -Raw | ConvertFrom-Json
$bots = @(Get-ChildItem $out -Filter 'bot-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })

function Line($who, $r) {
    "{0}: gate '{1}' cleared={2} rooms {3}/{4} latest checkpoint {5} attempt {6}; saw {7} checkpoints, {8} revives; enemies defeated {9}, player defeats {10}" -f `
        $who, $r.gateId, $r.gateCleared, $r.roomsCleared, $r.rooms, ($r.latestCheckpoint + 1), $r.gateAttempt, $r.checkpointsSeen, $r.revivesSeen, $r.enemiesDefeated, $r.playerDefeats
}
Write-Host (Line 'HOST  ' $h)
foreach ($b in $bots) { Write-Host (Line "CLIENT" $b) }
foreach ($r in @($h) + $bots) { Write-Host ("  {0} player {1}: boss attacks seen {2}, reward '{3}', holds [{4}]" -f $r.role, $r.localPlayerId, $r.bossAttacksSeen, $r.rewardSeen, ($r.localModifiers -join ', ')) }

$agree = @($bots | Where-Object { $_.gateId -eq $h.gateId -and $_.gateCleared -eq $h.gateCleared -and $_.roomsCleared -eq $h.roomsCleared -and $_.latestCheckpoint -eq $h.latestCheckpoint }).Count
$checks = [ordered]@{
    "all $Clients clients joined"                     = @($bots | Where-Object joined).Count -eq $Clients
    "the host is in gate '$Gate' and cleared it"      = $h.gateId -eq $Gate -and $h.gateCleared
    'every client agrees on the gate (cleared, rooms, checkpoint)' = $agree -eq $Clients
    'every client was shown the checkpoints'          = -not ($bots | Where-Object { $_.checkpointsSeen -lt ($h.latestCheckpoint + 1) })
    'every client saw the boss attack'                = -not ($bots | Where-Object { $_.bossAttacksSeen -lt 1 })
    'every player was rewarded and keeps it'          = -not (@($h) + $bots | Where-Object { -not $_.rewardSeen -or $_.localModifiers -notcontains $_.rewardSeen })
    'no unreadable events on clients'                 = -not ($bots | Where-Object { $_.unreadableEventLists -gt 0 })
}
foreach ($k in $checks.Keys) { Write-Host ("  [{0}] {1}" -f ($(if ($checks[$k]) { 'ok' } else { 'FAIL' })), $k) }
if (@($checks.Keys | Where-Object { -not $checks[$_] }).Count -eq 0) { Write-Host "PASS: a gate was played and cleared over the network."; exit 0 }
Write-Host "FAIL"; exit 1
