@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v02\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v02\demo-data"
exit /b
