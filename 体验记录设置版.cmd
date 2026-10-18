@echo off
setlocal
start "" "%~dp0artifacts\Pausewell-ui-v038\Pausewell.exe" --demo --data-dir "%~dp0artifacts\ui-v038\demo-data"
exit /b
