@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v036\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v036\demo-data"
exit /b
