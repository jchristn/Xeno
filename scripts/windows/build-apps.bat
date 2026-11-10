@echo off
REM Build xeno-desktop, which also builds xeno (CLI, worker, and terminal app) and xeno-tray into the same folder.
REM Used by the run-* scripts. The build output is shown only when the build fails.
REM
REM Usage: build-apps.bat [label]
setlocal
call "%~dp0env.bat"
set "LABEL=%~1"
if "%LABEL%"=="" set "LABEL=build-apps"
where dotnet >nul 2>nul
if errorlevel 1 goto :no_dotnet
echo [%LABEL%] Building xeno, xeno-desktop, and xeno-tray (%XENO_CONFIGURATION%, %XENO_FRAMEWORK%)...
set "XENO_BUILD_LOG=%TEMP%\xeno-build-%RANDOM%.log"
dotnet build "%XENO_DESKTOP_PROJECT%" -c %XENO_CONFIGURATION% -f %XENO_FRAMEWORK% --nologo > "%XENO_BUILD_LOG%" 2>&1
if errorlevel 1 goto :failed
del /q "%XENO_BUILD_LOG%" >nul 2>nul
endlocal & exit /b 0

:failed
type "%XENO_BUILD_LOG%"
del /q "%XENO_BUILD_LOG%" >nul 2>nul
endlocal & exit /b 1

:no_dotnet
echo ERROR: 'dotnet' was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download 1>&2
endlocal & exit /b 1
