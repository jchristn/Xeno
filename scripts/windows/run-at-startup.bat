@echo off
REM Start Xeno Agent (the tray agent) when you sign in. The agent connects the Xeno client when it starts, the same as
REM the sign-in item the installer adds, so this machine joins the Xeno network without opening an app. It uses the
REM apps install.bat put in %LOCALAPPDATA%\Programs\Xeno, so run install.bat first. It sets the value "Xeno Agent" in
REM HKCU\Software\Microsoft\Windows\CurrentVersion\Run; no admin rights are needed. Running it again rewrites the
REM value. remove-from-startup.bat undoes it, and remove.bat does too.
REM
REM Usage: run-at-startup.bat [--now]
REM   --now   also start Xeno Agent now, not only at the next sign-in
setlocal
call "%~dp0env.bat"
set "NOW="
set "RUN_KEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Run"
:parse
if "%~1"=="" goto :parsed
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if /i not "%~1"=="--now" goto :bad_arg
set "NOW=1"
shift /1
goto :parse

:parsed
set "TRAY=%XENO_INSTALL_DIR%\xeno-tray.exe"
if not exist "%TRAY%" goto :not_installed
reg add "%RUN_KEY%" /v "Xeno Agent" /t REG_SZ /d "\"%TRAY%\"" /f >nul
if errorlevel 1 goto :reg_failed
echo [run-at-startup] Xeno Agent will start when you sign in: "Xeno Agent" in %RUN_KEY%
reg query "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "Xeno Agent" >nul 2>nul
if not errorlevel 1 echo [run-at-startup] Note: the Xeno installer's sign-in item is also present. Only one agent runs at a time.
if not defined NOW goto :done
start "" "%TRAY%"
echo [run-at-startup] Started Xeno Agent.
:done
endlocal & exit /b 0

:not_installed
echo [run-at-startup] ERROR: Xeno Agent is not installed ^(%TRAY% is missing^). Run scripts\windows\install.bat first. 1>&2
endlocal & exit /b 1

:reg_failed
echo [run-at-startup] ERROR: could not set "Xeno Agent" in %RUN_KEY%. 1>&2
endlocal & exit /b 1

:bad_arg
echo [run-at-startup] Unknown argument: %1 1>&2
echo Usage: run-at-startup.bat [--now] 1>&2
endlocal & exit /b 2

:usage
echo(Start Xeno Agent, which connects the Xeno client, when you sign in. Uses the apps install.bat put in
echo(%LOCALAPPDATA%\Programs\Xeno and sets "Xeno Agent" in HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
echo.
echo(Usage: run-at-startup.bat [--now]
echo(  --now   also start Xeno Agent now, not only at the next sign-in
endlocal & exit /b 0
