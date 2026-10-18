@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v0310\Pausewell.exe" --demo --diagnostics --data-dir "%~dp0artifacts\ui-v0310\demo-data"
exit /b
