@echo off
REM Stops whatever is listening on the IRETP ports (5000 / 5002 / 5010).
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ports=5000,5002,5010; $pids=Get-NetTCPConnection -State Listen -LocalPort $ports -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique; if($pids){ $pids | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }; Write-Host 'IRETP hosts stopped.' } else { Write-Host 'No IRETP hosts were running.' }"
pause
