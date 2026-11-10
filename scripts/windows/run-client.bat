@echo off
REM Build and run the Xeno client worker from source in the foreground (xeno connect --foreground), so its log prints
REM to this console. Ctrl+C stops it.
REM
REM Usage: run-client.bat [--server <cloud^|url^|host[:port]>]
setlocal
call "%~dp0env.bat"
set "SERVER="
:parse
if "%~1"=="" goto :parsed
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if /i not "%~1"=="--server" goto :bad_arg
if "%~2"=="" goto :missing_server
set "SERVER=%~2"
shift /1
shift /1
goto :parse

:parsed
call "%~dp0build-apps.bat" run-client
if errorlevel 1 (endlocal & exit /b 1)
set "XENO=%XENO_APP_DIR%\xeno.exe"
if not defined SERVER goto :start
"%XENO%" server use "%SERVER%"
if errorlevel 1 (endlocal & exit /b 1)
:start
echo [run-client] Stopping any background client so this one can run in the foreground...
"%XENO%" stop >nul 2>nul
echo [run-client] Starting the client worker in the foreground (Ctrl+C to stop)...
"%XENO%" connect --foreground
endlocal & exit /b %ERRORLEVEL%

:missing_server
echo [run-client] --server requires a value. 1>&2
endlocal & exit /b 2

:bad_arg
echo [run-client] Unknown argument: %1 1>&2
endlocal & exit /b 2

:usage
echo(Build and run the Xeno client worker from source in the foreground (xeno connect --foreground).
echo.
echo(Usage: run-client.bat [--server ^<cloud^|url^|host[:port]^>]
echo(  --server   point this client at a server first (saved to client.json), for example http://localhost:9000
endlocal & exit /b 0
