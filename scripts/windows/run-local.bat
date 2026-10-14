@echo off
REM Run a complete local Xeno setup for development and testing:
REM   1. builds the server and the client apps,
REM   2. starts a local Xeno server in the background (artifacts\local-server, port 9000) and waits until it is healthy,
REM   3. starts the server web dashboard at http://localhost:9100 in the background (skip with XENO_SKIP_DASHBOARD=1),
REM   4. runs a client app in the foreground, pointed at the local server.
REM The client uses its own home directory (XENO_LOCAL_HOME, default %USERPROFILE%\.xeno-local), so your real
REM %USERPROFILE%\.xeno and its Xeno cloud sign-in are never touched. When the app exits, the server and dashboard stop.
REM Sign in to the local server with admin@xeno / password.
REM
REM Usage: run-local.bat [desktop^|tui^|tray^|client] [app arguments...]
setlocal
call "%~dp0env.bat"

set "APP=desktop"
if /i "%~1"=="desktop" (set "APP=desktop" & shift)
if /i "%~1"=="tui" (set "APP=tui" & shift)
if /i "%~1"=="tray" (set "APP=tray" & shift)
if /i "%~1"=="client" (set "APP=client" & shift)
set "APP_ARGS="
:collect
if "%~1"=="" goto :run
set "APP_ARGS=%APP_ARGS% %1"
shift
goto :collect

:run
set "XENO_CLIENT_HOME=%XENO_LOCAL_HOME%"
if not exist "%XENO_CLIENT_HOME%" mkdir "%XENO_CLIENT_HOME%"
if not exist "%XENO_LOCAL_SERVER_DIR%\data" mkdir "%XENO_LOCAL_SERVER_DIR%\data"
if not exist "%XENO_LOCAL_SERVER_DIR%\logs" mkdir "%XENO_LOCAL_SERVER_DIR%\logs"
if not exist "%XENO_LOCAL_SERVER_DIR%\xeno.json" copy /y "%XENO_REPO_ROOT%\xeno.json" "%XENO_LOCAL_SERVER_DIR%\xeno.json" >nul
set "PS=powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-stack.ps1""

echo [run-local] Building Xeno.Server (%XENO_CONFIGURATION%)...
dotnet build "%XENO_SERVER_PROJECT%" -c %XENO_CONFIGURATION% --nologo -v quiet
if errorlevel 1 exit /b 1
call "%~dp0build-apps.bat" run-local || exit /b 1

set "SERVER_PID="
set "DASHBOARD_PID="
%PS% -Action wait -ProcessId 0 >nul 2>nul
if not errorlevel 1 (
    echo [run-local] A Xeno server is already answering at %XENO_LOCAL_SERVER%; using it.
    goto :dashboard
)

echo [run-local] Starting the Xeno server (logs in %XENO_LOCAL_SERVER_DIR%\logs)...
for /f %%P in ('%PS% -Action start-server') do set "SERVER_PID=%%P"
echo [run-local] Waiting for %XENO_LOCAL_SERVER%...
%PS% -Action wait -ProcessId %SERVER_PID%
if errorlevel 1 (
    echo [run-local] ERROR: the server did not become healthy. See %XENO_LOCAL_SERVER_DIR%\logs\run-local-server.log.
    goto :cleanup
)
echo [run-local] Server healthy at %XENO_LOCAL_SERVER%.

:dashboard
if "%XENO_SKIP_DASHBOARD%"=="1" goto :app
where npm >nul 2>nul || goto :app
if not exist "%XENO_REPO_ROOT%\dashboard\node_modules" (
    echo [run-local] Installing dashboard packages...
    pushd "%XENO_REPO_ROOT%\dashboard"
    call npm.cmd install --silent
    popd
)
echo [run-local] Starting the web dashboard at http://localhost:9100...
for /f %%P in ('%PS% -Action start-dashboard') do set "DASHBOARD_PID=%%P"

:app
set "XENO=%XENO_APP_DIR%\xeno.exe"
"%XENO%" server use %XENO_LOCAL_SERVER% >nul
echo [run-local] Client home: %XENO_CLIENT_HOME% (server %XENO_LOCAL_SERVER%)
echo [run-local] Sign in from the General page with admin@xeno / password.
if "%APP%"=="desktop" "%XENO_APP_DIR%\xeno-desktop.exe" %APP_ARGS%
if "%APP%"=="tui" "%XENO%" tui %APP_ARGS%
if "%APP%"=="tray" "%XENO_APP_DIR%\xeno-tray.exe" %APP_ARGS%
if "%APP%"=="client" "%XENO%" connect --foreground %APP_ARGS%

:cleanup
if defined DASHBOARD_PID taskkill /T /F /PID %DASHBOARD_PID% >nul 2>nul
if defined SERVER_PID (
    echo [run-local] Stopping the Xeno server...
    taskkill /T /F /PID %SERVER_PID% >nul 2>nul
)
exit /b 0
