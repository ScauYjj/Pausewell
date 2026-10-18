@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v034\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v034\demo-data"
exit /b
