@echo off
REM Stop starting Xeno Agent when you sign in: deletes the value "Xeno Agent" that run-at-startup.bat set in
REM HKCU\Software\Microsoft\Windows\CurrentVersion\Run. A running agent keeps running; quit it from its menu. Succeeds
REM when nothing was registered. The sign-in item the Xeno installer adds is not changed.
REM
REM Usage: remove-from-startup.bat
setlocal
call "%~dp0env.bat"
set "RUN_KEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if not "%~1"=="" goto :bad_arg

reg query "%RUN_KEY%" /v "Xeno Agent" >nul 2>nul
if errorlevel 1 goto :nothing
reg delete "%RUN_KEY%" /v "Xeno Agent" /f >nul
if errorlevel 1 goto :reg_failed
echo [remove-from-startup] Xeno Agent no longer starts when you sign in. Removed "Xeno Agent" from %RUN_KEY%
goto :installer_note
:nothing
echo [remove-from-startup] Nothing to remove: Xeno Agent was not set to start at sign-in by run-at-startup.bat.
:installer_note
reg query "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "Xeno Agent" >nul 2>nul
if not errorlevel 1 echo [remove-from-startup] Note: the Xeno installer's sign-in item still starts Xeno Agent. Turn it off in Settings ^> Apps ^> Startup.
endlocal & exit /b 0

:reg_failed
echo [remove-from-startup] ERROR: could not delete "Xeno Agent" from %RUN_KEY%. 1>&2
endlocal & exit /b 1

:bad_arg
echo [remove-from-startup] Unknown argument: %1 (this script takes no arguments) 1>&2
endlocal & exit /b 2

:usage
echo(Stop starting Xeno Agent when you sign in: deletes "Xeno Agent" from
echo(HKCU\Software\Microsoft\Windows\CurrentVersion\Run, which run-at-startup.bat set. A running agent keeps running.
echo.
echo(Usage: remove-from-startup.bat
endlocal & exit /b 0
