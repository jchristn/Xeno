@echo off
REM Reset the Xeno client on this machine to a fresh state. Stops the client worker, the desktop app, and the tray
REM agent, then deletes the local history database, logs, state, and app preferences in the client home
REM (%USERPROFILE%\.xeno, or XENO_CLIENT_HOME). By default client.json (server, sign-in, providers, models, settings)
REM is kept; pass --all to delete the whole client home, which signs this machine out and points it back at the Xeno cloud.
REM
REM Usage: factory-reset.bat [-y^|--yes^|--force] [--all]
setlocal
call "%~dp0env.bat"
set "FORCE="
set "WIPE_ALL="
:parse
if "%~1"=="" goto :parsed
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if /i "%~1"=="--all" (
    set "WIPE_ALL=1"
    shift /1
    goto :parse
)
set "KNOWN="
if /i "%~1"=="-y" set "KNOWN=1"
if /i "%~1"=="--yes" set "KNOWN=1"
if /i "%~1"=="--force" set "KNOWN=1"
if not defined KNOWN goto :bad_arg
set "FORCE=1"
shift /1
goto :parse

:parsed
if not exist "%XENO_HOME_DIR%" goto :nothing
if defined FORCE goto :reset
echo.
echo WARNING: this permanently deletes Xeno client data in:
echo     %XENO_HOME_DIR%
echo   - the local history database: requests, host and provider telemetry
echo   - logs, client state, and desktop and terminal app preferences
if defined WIPE_ALL echo   - --all: ALSO client.json: server, sign-in, providers, models, and settings
echo.
set "CONFIRM="
set /p "CONFIRM=Type YES to continue: "
if not "%CONFIRM%"=="YES" goto :aborted

:reset
echo [factory-reset] Stopping the client worker, desktop app, and tray agent...
if defined XENO_CLI "%XENO_CLI%" stop --force >nul 2>nul
taskkill /IM xeno-desktop.exe /F >nul 2>nul
taskkill /IM xeno-tray.exe /F >nul 2>nul
if defined WIPE_ALL goto :wipe_all
echo [factory-reset] Deleting history, logs, state, and preferences ^(keeping client.json^)...
for /d %%D in ("%XENO_HOME_DIR%\*") do rmdir /s /q "%%~fD"
for %%F in ("%XENO_HOME_DIR%\*") do if /i not "%%~nxF"=="client.json" del /q "%%~fF"
set "LEFT="
for /f "delims=" %%E in ('dir /b /a "%XENO_HOME_DIR%" 2^>nul ^| findstr /v /i /x "client.json"') do set "LEFT=1"
if defined LEFT goto :locked
goto :done

:wipe_all
echo [factory-reset] Deleting %XENO_HOME_DIR%...
rmdir /s /q "%XENO_HOME_DIR%"
if exist "%XENO_HOME_DIR%" goto :locked

:done
echo [factory-reset] Done.
endlocal & exit /b 0

:nothing
echo [factory-reset] Nothing to reset: %XENO_HOME_DIR% does not exist.
endlocal & exit /b 0

:aborted
echo [factory-reset] Aborted.
endlocal & exit /b 1

:locked
echo ERROR: some client files are still in use. Stop the client worker (xeno stop --force), close the Xeno apps, and run factory-reset again. 1>&2
endlocal & exit /b 1

:bad_arg
echo [factory-reset] Unknown argument: %1 1>&2
echo Usage: factory-reset.bat [-y^|--yes^|--force] [--all] 1>&2
endlocal & exit /b 2

:usage
echo(Reset the Xeno client on this machine: deletes the history database, logs, state, and app preferences in the
echo(client home (%USERPROFILE%\.xeno, or XENO_CLIENT_HOME). client.json is kept unless you pass --all.
echo.
echo(Usage: factory-reset.bat [-y^|--yes^|--force] [--all]
endlocal & exit /b 0
