# start-iretp.ps1
# Launches the three IRETP hosts in their own minimized windows.
# These run independently of Claude/any tool — they stay up until you
# close the windows or reboot. Re-run any time.
# -NoBrowser : skip opening the browser (used by the logon auto-start).
param([switch]$NoBrowser)
$ErrorActionPreference = 'SilentlyContinue'
$root   = $PSScriptRoot
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

function Up($port) { [bool](Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) }

if ((Up 5000) -and (Up 5002) -and (Up 5010)) {
  Write-Host 'All three IRETP hosts are already running - skipping build.'
} else {
  Write-Host 'Building solution once (so the 3 hosts do not race on shared DLLs)...'
  & $dotnet build (Join-Path $root 'IRETP.sln') --nologo -v minimal | Out-Null
}

function Launch($proj) {
  Start-Process -FilePath $dotnet -WorkingDirectory $root -WindowStyle Minimized `
    -ArgumentList @('run','--project',(Join-Path $root "src\$proj"),'--launch-profile','http','--no-build')
}

if (Up 5000) {
  Write-Host 'IRETP.WebAPI already running on http://localhost:5000.'
} else {
  Write-Host 'Starting IRETP.WebAPI  -> http://localhost:5000 ...'
  Launch 'IRETP.WebAPI'
  $ready = $false
  for ($i = 0; $i -lt 45; $i++) {
    try { Invoke-WebRequest -UseBasicParsing 'http://localhost:5000/healthz/live' -TimeoutSec 4 | Out-Null; $ready = $true; break }
    catch { Start-Sleep -Seconds 2 }
  }
  Write-Host ("  WebAPI ready: {0}" -f $ready)
}

if (Up 5002) {
  Write-Host 'IRETP.AdminAPI already running on http://localhost:5002.'
} else {
  Write-Host 'Starting IRETP.AdminAPI -> http://localhost:5002 ...'
  Launch 'IRETP.AdminAPI'
}
if (Up 5010) {
  Write-Host 'IRETP.Web already running on http://localhost:5010.'
} else {
  Write-Host 'Starting IRETP.Web      -> http://localhost:5010 ...'
  Launch 'IRETP.Web'
  for ($i = 0; $i -lt 45; $i++) {
    try { Invoke-WebRequest -UseBasicParsing 'http://localhost:5010/' -TimeoutSec 4 | Out-Null; break }
    catch { Start-Sleep -Seconds 2 }
  }
}

Write-Host 'Warming the KPI cache (flips /healthz/sla green)...'
try { Invoke-WebRequest -UseBasicParsing 'http://localhost:5000/api/dashboard/kpis' -TimeoutSec 25 | Out-Null } catch {}

if (-not $NoBrowser) { Start-Process 'http://localhost:5010' }
Write-Host ''
Write-Host '======================================================'
Write-Host ' IRETP is UP    ->  http://localhost:5010'
Write-Host ' Admin login    ->  admin@dld.gov.ae  /  Admin@DLD2026!'
Write-Host ' Stop it        ->  run stop-iretp.bat (or close the 3 minimized windows)'
Write-Host '======================================================'
