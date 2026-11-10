@echo off
REM Remove and then freshly install Xeno from this checkout (the xeno tool, the desktop app, and the tray agent).
REM Your client configuration in %USERPROFILE%\.xeno is kept. Arguments are passed to install.bat.
REM
REM Usage: reinstall.bat [--no-apps] [--no-dashboard]
setlocal
REM Check the arguments before removing anything, so a typo does not leave Xeno uninstalled. shift does not change %*.
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
echo [reinstall] Removing the current installation...
call "%~dp0remove.bat"
if errorlevel 1 (endlocal & exit /b 1)
echo.
echo [reinstall] Installing...
call "%~dp0install.bat" %*
endlocal & exit /b %ERRORLEVEL%

:bad_arg
echo [reinstall] Unknown argument: %1 1>&2
endlocal & exit /b 2

:usage
echo(Remove and then freshly install Xeno from this checkout. Your client configuration is kept.
echo.
echo(Usage: reinstall.bat [--no-apps] [--no-dashboard]
endlocal & exit /b 0
