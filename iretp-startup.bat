@echo off
REM Logon auto-start: brings the three IRETP hosts online when you sign in.
REM No browser pop-up, no pause. Registered in HKCU\...\Run as "IRETP".
REM Remove it any time with:  reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v IRETP /f
start "" /min powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Minimized -File "%~dp0start-iretp.ps1" -NoBrowser
