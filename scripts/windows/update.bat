@echo off
REM Update the installed Xeno from this checkout: stops the client worker, reinstalls the xeno tool and the apps, and
REM starts the worker again if it was running. Your client configuration in %USERPROFILE%\.xeno is kept.
REM
REM Usage: update.bat [--no-apps] [--no-dashboard]
setlocal
set "WAS_RUNNING=0"
where xeno >nul 2>nul && (
    xeno status --json 2>nul | findstr /r /c:"\"processId\": *[1-9]" >nul && set "WAS_RUNNING=1"
)

call "%~dp0reinstall.bat" %*
if errorlevel 1 exit /b 1

if "%WAS_RUNNING%"=="1" (
    echo.
    echo [update] Starting the client worker again...
    call "%USERPROFILE%\.dotnet\tools\xeno.exe" start
)
exit /b 0
