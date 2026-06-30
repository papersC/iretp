@echo off
REM Double-click this to launch all three IRETP hosts for the demo.
REM The hosts open in their own windows and stay up after this window closes.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-iretp.ps1"
echo.
echo Launcher finished. The three IRETP host windows will keep running.
pause
