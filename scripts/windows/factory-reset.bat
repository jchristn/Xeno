@echo off
setlocal enabledelayedexpansion
REM Reset the Xeno client on this machine to a fresh state. Stops the client worker, the desktop app, and the tray
REM agent, then deletes the local history database, logs, state, and app preferences in the client home
REM (%USERPROFILE%\.xeno, or XENO_CLIENT_HOME). By default client.json (server, sign-in, providers, models, settings)
REM is kept; pass --all to delete the whole client home, which signs this machine out and points it back at the Xeno cloud.
REM
REM Usage: factory-reset.bat [-y^|--yes^|--force] [--all]

set "HOME_DIR=%XENO_CLIENT_HOME%"
if "%HOME_DIR%"=="" set "HOME_DIR=%USERPROFILE%\.xeno"
set "FORCE="
set "WIPE_ALL="
for %%A in (%*) do (
    set "KNOWN_ARG="
    if /I "%%~A"=="-y" (set "FORCE=1" & set "KNOWN_ARG=1")
    if /I "%%~A"=="--yes" (set "FORCE=1" & set "KNOWN_ARG=1")
    if /I "%%~A"=="--force" (set "FORCE=1" & set "KNOWN_ARG=1")
    if /I "%%~A"=="--all" (set "WIPE_ALL=1" & set "KNOWN_ARG=1")
    if not defined KNOWN_ARG (
        echo [factory-reset] Unknown argument: %%~A 1>&2
        echo Usage: scripts\windows\factory-reset.bat [-y^|--yes^|--force] [--all] 1>&2
        exit /b 2
    )
)

if not exist "%HOME_DIR%" (
    echo [factory-reset] Nothing to reset: %HOME_DIR% does not exist.
    exit /b 0
)

if not defined FORCE (
    echo.
    echo WARNING: this permanently deletes Xeno client data in:
    echo     %HOME_DIR%
    echo   - the local history database: requests, host and provider telemetry
    echo   - logs, client state, and desktop and terminal app preferences
    if defined WIPE_ALL echo   - --all: ALSO client.json: server, sign-in, providers, models, and settings
    echo.
    set /p "CONFIRM=Type YES to continue: "
    if /I not "!CONFIRM!"=="YES" (
        echo [factory-reset] Aborted.
        exit /b 1
    )
)

echo [factory-reset] Stopping the client worker, desktop app, and tray agent...
where xeno >nul 2>nul && call xeno stop --force >nul 2>nul
taskkill /IM xeno-desktop.exe /F >nul 2>nul
taskkill /IM xeno-tray.exe /F >nul 2>nul

if defined WIPE_ALL (
    echo [factory-reset] Deleting %HOME_DIR%...
    rmdir /s /q "%HOME_DIR%"
) else (
    echo [factory-reset] Deleting history, logs, state, and preferences ^(keeping client.json^)...
    for /d %%D in ("%HOME_DIR%\*") do rmdir /s /q "%%~fD"
    for %%F in ("%HOME_DIR%\*") do if /I not "%%~nxF"=="client.json" del /q "%%~fF"
)

echo [factory-reset] Done.
exit /b 0
