@echo off
REM Shared settings for the Xeno Windows scripts. Call this file; do not run it directly.
REM
REM Environment variables:
REM   XENO_CONFIGURATION   Build configuration for the run-* scripts (default Debug; install uses Release).
REM   XENO_FRAMEWORK       Target framework (default net10.0).
REM   XENO_CLIENT_HOME     Client home used by xeno, xeno-desktop, xeno-tray, and xeno tui (default %USERPROFILE%\.xeno).
REM   XENO_LOCAL_SERVER    URL of the local server started by run-server.bat and run-local.bat (default http://localhost:9000).
REM   XENO_LOCAL_HOME      Client home run-local.bat uses, so local testing never touches %USERPROFILE%\.xeno
REM                        (default %USERPROFILE%\.xeno-local).

for %%I in ("%~dp0..\..") do set "XENO_REPO_ROOT=%%~fI"
if "%XENO_CONFIGURATION%"=="" set "XENO_CONFIGURATION=Debug"
if "%XENO_FRAMEWORK%"=="" set "XENO_FRAMEWORK=net10.0"
if "%XENO_LOCAL_SERVER%"=="" set "XENO_LOCAL_SERVER=http://localhost:9000"
if "%XENO_LOCAL_HOME%"=="" set "XENO_LOCAL_HOME=%USERPROFILE%\.xeno-local"
set "XENO_DESKTOP_PROJECT=%XENO_REPO_ROOT%\src\Xeno.Desktop\Xeno.Desktop.csproj"
set "XENO_CLIENT_PROJECT=%XENO_REPO_ROOT%\src\Xeno.Client\Xeno.Client.csproj"
set "XENO_SERVER_PROJECT=%XENO_REPO_ROOT%\src\Xeno.Server\Xeno.Server.csproj"
set "XENO_TOOL_PACKAGES=%XENO_REPO_ROOT%\artifacts\tools"
set "XENO_LOCAL_SERVER_DIR=%XENO_REPO_ROOT%\artifacts\local-server"
set "XENO_APP_DIR=%XENO_REPO_ROOT%\src\Xeno.Desktop\bin\%XENO_CONFIGURATION%\%XENO_FRAMEWORK%"
set "XENO_INSTALL_DIR=%LOCALAPPDATA%\Programs\Xeno"
exit /b 0
