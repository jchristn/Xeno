@echo off
REM Build and run the Xeno client worker from source in the foreground (xeno connect --foreground), so its log prints
REM to this console. Ctrl+C stops it.
REM
REM Usage: run-client.bat [--server <cloud|url|host[:port]>]
setlocal
call "%~dp0env.bat"
call "%~dp0build-apps.bat" run-client || exit /b 1
set "XENO=%XENO_APP_DIR%\xeno.exe"
if /i "%~1"=="--server" (
    if "%~2"=="" (
        echo [run-client] --server requires a value.
        exit /b 2
    )
    "%XENO%" server use "%~2" || exit /b 1
)
echo [run-client] Stopping any background client so this one can run in the foreground...
"%XENO%" stop >nul 2>nul
echo [run-client] Starting the client worker in the foreground (Ctrl+C to stop)...
"%XENO%" connect --foreground
exit /b %ERRORLEVEL%
