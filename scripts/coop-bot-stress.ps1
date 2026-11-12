<#
Ten-player stress test on one PC (ticket 14): runs the built co-op player as a bot host plus nine
bot clients over local UDP (Fish-Net Tugboat). Every copy's player is a BotBrain that fights the
Goblins and Hobgoblins the host keeps topping up. The host runs in a window by default, so its
frame time includes rendering ten players; the clients run headless. Each copy measures for
-Seconds and writes JSON: the host from the moment all clients are connected (stopping early if one
leaves), each client once it has joined. Clients run a little longer than the host, so the session
stays full for the host's whole window.

  powershell -File scripts/coop-bot-stress.ps1               # host windowed, 60 s
  powershell -File scripts/coop-bot-stress.ps1 -Seconds 30 -HeadlessHost

Passes when all nine clients joined a ten-player session, every player dealt damage, and no
client saw unreadable events. Frame time and bandwidth are reported, not judged.

Build first with: Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.CoopPlayerBuild.Build
#>
param([int]$Seconds = 60, [switch]$HeadlessHost, [int]$Clients = 9)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $project 'Builds/Coop/HunterSystem.exe'
if (-not (Test-Path $exe)) { throw "No co-op build at $exe. Build it first." }

$out = Join-Path $project 'TestResults/bots'
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -Filter '*.json' | Remove-Item

function Q([string]$path) { '"' + $path + '"' }   # Start-Process does not quote arguments; the path has a space
$hostJson = Join-Path $out 'host.json'
$hostDisplay = if ($HeadlessHost) { @('-batchmode', '-nographics') } else { @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720') }
$hostArgs = $hostDisplay + @('-no-save', '-transport', 'local', '-coop', 'host', '-bot', '1', '-bot-expect', $Clients, '-autotest', $Seconds, (Q $hostJson), '-logFile', (Q (Join-Path $out 'host.log')))
$processes = @(Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru)
Start-Sleep -Seconds 4

for ($i = 1; $i -le $Clients; $i++) {
    $json = Join-Path $out "bot-$i.json"
    $clientArgs = @('-batchmode', '-nographics', '-no-save', '-transport', 'local', '-coop', 'join', '-address', '127.0.0.1', '-player-key', "bot-$i",
        '-bot', ($i + 1), '-autotest', ($Seconds + 15), (Q $json), '-logFile', (Q (Join-Path $out "bot-$i.log")))
    $processes += Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru
    Start-Sleep -Milliseconds 300
}

$deadline = (Get-Date).AddSeconds($Seconds + 90)
while (($processes | Where-Object { -not $_.HasExited }) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
foreach ($p in $processes) { if (-not $p.HasExited) { $p.Kill(); Write-Host "Killed a copy that did not finish (pid $($p.Id))." } }

if (-not (Test-Path $hostJson)) { Write-Host "No host result; see $out"; exit 1 }
$h = Get-Content $hostJson -Raw | ConvertFrom-Json
$bots = @(Get-ChildItem $out -Filter 'bot-*.json' | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
$seconds = [math]::Max($h.measuredSeconds, 1)

function Fmt($v, $digits = 1) { [math]::Round([double]$v, $digits) }
Write-Host ""
Write-Host "HOST ($(if ($HeadlessHost) { 'headless' } else { 'windowed 1280x720' })): players=$($h.players) connected=$($h.connectedClients) enemies defeated=$($h.enemiesDefeated) player defeats=$($h.playerDefeats)"
Write-Host "  frame time ms: mean $(Fmt $h.frameMsMean 2)  p99 $(Fmt $h.frameMsP99 2)  max $(Fmt $h.frameMsMax 2)  over $($h.frames) frames / $(Fmt $seconds) s"
Write-Host "  upload $(Fmt ($h.hostBytesSent / $seconds / 1024)) KB/s total, $(Fmt ($h.hostBytesSent / $seconds / 1024 / $Clients)) KB/s per client; download $(Fmt ($h.hostBytesReceived / $seconds / 1024)) KB/s; last snapshot $($h.lastSnapshotBytes) bytes"
Write-Host "  damage by player: $($h.damageByPlayer -join ', ')"
Write-Host "CLIENTS: $($bots.Count) results, joined $(@($bots | Where-Object joined).Count)"
if ($bots.Count -gt 0) {
    $frameMean = ($bots | Measure-Object frameMsMean -Average).Average
    $frameP99 = ($bots | Measure-Object frameMsP99 -Maximum).Maximum
    $correction = ($bots | Measure-Object largestCorrection -Maximum).Maximum
    $unconfirmed = ($bots | Measure-Object unconfirmedCommands -Maximum).Maximum
    Write-Host "  frame time ms (headless): mean $(Fmt $frameMean 2), worst p99 $(Fmt $frameP99 2); largest correction $(Fmt $correction 3) m; median round trip up to $unconfirmed ticks"
}

$damaged = @($h.damageByPlayer | ForEach-Object { [int](($_ -split ':')[0]) })
$checks = [ordered]@{
    "all $Clients clients joined"             = @($bots | Where-Object joined).Count -eq $Clients
    "a full session of $($Clients + 1) on the host" = $h.players -eq ($Clients + 1)
    'host measured a full session throughout' = $h.sessionWasFull -and $h.measuredSeconds -ge ($Seconds - 1)
    'every player dealt damage'             = $damaged.Count -eq ($Clients + 1)
    'no unreadable events on clients'       = -not ($bots | Where-Object { $_.unreadableEventLists -gt 0 })
}
foreach ($k in $checks.Keys) { Write-Host ("  [{0}] {1}" -f ($(if ($checks[$k]) { 'ok' } else { 'FAIL' })), $k) }
if (@($checks.Keys | Where-Object { -not $checks[$_] }).Count -eq 0) { Write-Host "PASS: a ten-player bot session ran."; exit 0 }
Write-Host "FAIL"; exit 1
