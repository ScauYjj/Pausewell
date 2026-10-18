@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v039\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v039\demo-data"
exit /b
