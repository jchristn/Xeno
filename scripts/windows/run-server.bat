@echo off
REM Build and run a local Xeno server (src\Xeno.Server) from source in the foreground. Ctrl+C stops it.
REM It runs in artifacts\local-server (ignored by git): xeno.json there starts as a copy of the repository's xeno.json,
REM with the SQLite database in data\ and logs in logs\. REST and WebSocket listen on port 9000. Delete
REM artifacts\local-server to start over with an empty server. Sign in with admin@xeno / password.
REM
REM Usage: run-server.bat [settings-file]
setlocal
call "%~dp0env.bat"
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if not "%~2"=="" goto :bad_arg
where dotnet >nul 2>nul
if errorlevel 1 goto :no_dotnet
call :prepare
set "SETTINGS=%~1"
if "%SETTINGS%"=="" set "SETTINGS=xeno.json"
echo [run-server] Building Xeno.Server (%XENO_CONFIGURATION%)...
dotnet build "%XENO_SERVER_PROJECT%" -c %XENO_CONFIGURATION% --nologo -v quiet
if errorlevel 1 (endlocal & exit /b 1)
pushd "%XENO_LOCAL_SERVER_DIR%"
echo [run-server] Starting the Xeno server in %XENO_LOCAL_SERVER_DIR% with %SETTINGS% (Ctrl+C to stop)...
dotnet run --project "%XENO_SERVER_PROJECT%" -c %XENO_CONFIGURATION% --no-build -- "%SETTINGS%"
set "EXIT_CODE=%ERRORLEVEL%"
popd
endlocal & exit /b %EXIT_CODE%

:prepare
if not exist "%XENO_LOCAL_SERVER_DIR%\data" mkdir "%XENO_LOCAL_SERVER_DIR%\data"
if not exist "%XENO_LOCAL_SERVER_DIR%\logs" mkdir "%XENO_LOCAL_SERVER_DIR%\logs"
if not exist "%XENO_LOCAL_SERVER_DIR%\xeno.json" copy /y "%XENO_REPO_ROOT%\xeno.json" "%XENO_LOCAL_SERVER_DIR%\xeno.json" >nul
exit /b 0

:bad_arg
echo [run-server] Unexpected argument: %2 ^(this script takes at most a settings file^) 1>&2
endlocal & exit /b 2

:no_dotnet
echo ERROR: 'dotnet' was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download 1>&2
endlocal & exit /b 1

:usage
echo(Build and run a local Xeno server from source in the foreground, in artifacts\local-server (port 9000).
echo(Sign in with admin@xeno / password.
echo.
echo(Usage: run-server.bat [settings-file]
echo(  run-server.bat                            use artifacts\local-server\xeno.json
echo(  run-server.bat C:\path\to\other\xeno.json use another settings file (absolute path)
endlocal & exit /b 0
