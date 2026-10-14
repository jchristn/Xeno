@echo off
REM Build xeno-desktop, which also builds xeno (CLI, worker, and terminal app) and xeno-tray into the same folder.
REM Used by the run-* scripts. The build output is shown only when the build fails.
REM
REM Usage: build-apps.bat <label>
setlocal
call "%~dp0env.bat"
echo [%~1] Building xeno, xeno-desktop, and xeno-tray (%XENO_CONFIGURATION%, %XENO_FRAMEWORK%)...
set "XENO_BUILD_LOG=%TEMP%\xeno-build-%RANDOM%.log"
dotnet build "%XENO_DESKTOP_PROJECT%" -c %XENO_CONFIGURATION% -f %XENO_FRAMEWORK% --nologo > "%XENO_BUILD_LOG%" 2>&1
if errorlevel 1 (
    type "%XENO_BUILD_LOG%"
    del /q "%XENO_BUILD_LOG%" >nul 2>nul
    exit /b 1
)
del /q "%XENO_BUILD_LOG%" >nul 2>nul
exit /b 0
