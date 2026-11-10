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
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage

REM The app arguments are forwarded untouched (=, ; and & included): all of %* when no app name is given, otherwise
REM everything after the app name.
set "APP=desktop"
set "APP_ARGS=%*"
set "APP_ARGS_ALL=%*"
for %%A in (desktop tui tray client) do if /i "%~1"=="%%A" set "APP=%%A"
if /i not "%~1"=="%APP%" goto :prepare
set "APP_ARGS="
if not "%~2"=="" call set "APP_ARGS=%%APP_ARGS_ALL:*%~1=%%"
goto :prepare

:prepare
where dotnet >nul 2>nul
if errorlevel 1 goto :no_dotnet
set "XENO_CLIENT_HOME=%XENO_LOCAL_HOME%"
if not exist "%XENO_CLIENT_HOME%" mkdir "%XENO_CLIENT_HOME%"
if not exist "%XENO_LOCAL_SERVER_DIR%\data" mkdir "%XENO_LOCAL_SERVER_DIR%\data"
if not exist "%XENO_LOCAL_SERVER_DIR%\logs" mkdir "%XENO_LOCAL_SERVER_DIR%\logs"
if not exist "%XENO_LOCAL_SERVER_DIR%\xeno.json" copy /y "%XENO_REPO_ROOT%\xeno.json" "%XENO_LOCAL_SERVER_DIR%\xeno.json" >nul
set "PS=powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-stack.ps1""

echo [run-local] Building Xeno.Server (%XENO_CONFIGURATION%)...
dotnet build "%XENO_SERVER_PROJECT%" -c %XENO_CONFIGURATION% --nologo -v quiet
if errorlevel 1 (endlocal & exit /b 1)
call "%~dp0build-apps.bat" run-local
if errorlevel 1 (endlocal & exit /b 1)

set "SERVER_PID="
set "DASHBOARD_PID="
set "EXIT_CODE=0"
%PS% -Action probe >nul 2>nul
if errorlevel 1 goto :start_server
echo [run-local] A Xeno server is already answering at %XENO_LOCAL_SERVER%; using it.
goto :dashboard

:start_server
echo [run-local] Starting the Xeno server (logs in %XENO_LOCAL_SERVER_DIR%\logs)...
for /f %%P in ('%PS% -Action start-server') do set "SERVER_PID=%%P"
if not defined SERVER_PID goto :server_failed
echo [run-local] Waiting for %XENO_LOCAL_SERVER%...
%PS% -Action wait -ProcessId %SERVER_PID%
if errorlevel 1 goto :server_failed
echo [run-local] Server healthy at %XENO_LOCAL_SERVER%.

:dashboard
if "%XENO_SKIP_DASHBOARD%"=="1" goto :app
where npm >nul 2>nul
if errorlevel 1 goto :app
if exist "%XENO_REPO_ROOT%\dashboard\node_modules" goto :start_dashboard
echo [run-local] Installing dashboard packages...
pushd "%XENO_REPO_ROOT%\dashboard"
call npm install --silent
popd
:start_dashboard
echo [run-local] Starting the web dashboard at http://localhost:9100...
for /f %%P in ('%PS% -Action start-dashboard') do set "DASHBOARD_PID=%%P"

:app
set "XENO=%XENO_APP_DIR%\xeno.exe"
"%XENO%" server use "%XENO_LOCAL_SERVER%" >nul
if errorlevel 1 (
    set "EXIT_CODE=1"
    goto :cleanup
)
echo [run-local] Client home: %XENO_CLIENT_HOME% (server %XENO_LOCAL_SERVER%)
echo [run-local] Sign in from the General page with admin@xeno / password.
if "%APP%"=="desktop" "%XENO_APP_DIR%\xeno-desktop.exe" %APP_ARGS%
if "%APP%"=="tui" "%XENO%" tui %APP_ARGS%
if "%APP%"=="tray" "%XENO_APP_DIR%\xeno-tray.exe" %APP_ARGS%
if "%APP%"=="client" "%XENO%" connect --foreground %APP_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"
goto :cleanup

:server_failed
echo [run-local] ERROR: the server did not become healthy. See %XENO_LOCAL_SERVER_DIR%\logs\run-local-server.log and run-local-server.err.log. 1>&2
set "EXIT_CODE=1"

:cleanup
if defined DASHBOARD_PID taskkill /T /F /PID %DASHBOARD_PID% >nul 2>nul
if not defined SERVER_PID goto :exit
echo [run-local] Stopping the Xeno server...
taskkill /T /F /PID %SERVER_PID% >nul 2>nul
:exit
endlocal & exit /b %EXIT_CODE%

:no_dotnet
echo ERROR: 'dotnet' was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download 1>&2
endlocal & exit /b 1

:usage
echo(Run a local Xeno server and web dashboard in the background, then a client app connected to them. The client
echo(uses XENO_LOCAL_HOME (default %USERPROFILE%\.xeno-local). Sign in with admin@xeno / password.
echo.
echo(Usage: run-local.bat [desktop^|tui^|tray^|client] [app arguments...]
echo(  run-local.bat               desktop app (default)
echo(  run-local.bat tui           terminal app in this console
echo(  run-local.bat tray          tray agent
echo(  run-local.bat client        client worker in the foreground
endlocal & exit /b 0
