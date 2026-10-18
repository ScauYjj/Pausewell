@echo off
setlocal
for %%i in ("%~dp0..\..") do set "REVIEW_ROOT=%%~fi"
set "DOTNET_ROOT=%REVIEW_ROOT%\.tools\dotnet"
start "" "%REVIEW_ROOT%\artifacts\wireframe-review\Pausewell.Wireframes.exe"
exit /b
