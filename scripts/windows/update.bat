@echo off
REM Update the installed Xeno from this checkout: stops the client worker, reinstalls the xeno tool and the apps, and
REM starts the worker again if it was running. Your client configuration in %USERPROFILE%\.xeno is kept.
REM
REM Usage: update.bat [--no-apps] [--no-dashboard]
setlocal
:check
if "%~1"=="" goto :checked
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if /i "%~1"=="--no-apps" (
    shift /1
    goto :check
)
if /i "%~1"=="--no-dashboard" (
    shift /1
    goto :check
)
goto :bad_arg

:checked
call "%~dp0env.bat"
set "WAS_RUNNING=0"
if not defined XENO_CLI goto :reinstall
REM The client state keeps the last processId after the worker stops, so check that the process still exists.
set "STATUS_FILE=%TEMP%\xeno-status-%RANDOM%.json"
"%XENO_CLI%" status --json > "%STATUS_FILE%" 2>nul
set "WORKER_PID="
for /f "tokens=2 delims=:, " %%P in ('findstr /c:"processId" "%STATUS_FILE%"') do if not defined WORKER_PID set "WORKER_PID=%%P"
del /q "%STATUS_FILE%" >nul 2>nul
if not defined WORKER_PID goto :reinstall
if "%WORKER_PID%"=="0" goto :reinstall
tasklist /FI "PID eq %WORKER_PID%" /NH 2>nul | find " %WORKER_PID% " >nul
if not errorlevel 1 set "WAS_RUNNING=1"

:reinstall
call "%~dp0reinstall.bat" %*
if errorlevel 1 (endlocal & exit /b 1)
if "%WAS_RUNNING%"=="0" (endlocal & exit /b 0)

echo.
echo [update] Starting the client worker again...
call "%~dp0env.bat"
if not defined XENO_CLI goto :no_cli
"%XENO_CLI%" start
endlocal & exit /b %ERRORLEVEL%

:no_cli
echo [update] ERROR: xeno was not found after the reinstall; start the worker with: xeno start 1>&2
endlocal & exit /b 1

:bad_arg
echo [update] Unknown argument: %1 1>&2
endlocal & exit /b 2

:usage
echo(Update the installed Xeno from this checkout and start the client worker again if it was running.
echo.
echo(Usage: update.bat [--no-apps] [--no-dashboard]
endlocal & exit /b 0
