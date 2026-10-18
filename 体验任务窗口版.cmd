@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v037\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v037\demo-data"
exit /b
