@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v035\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v035\demo-data"
exit /b
