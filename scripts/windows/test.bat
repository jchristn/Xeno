@echo off
REM Build and run the Touchstone test suites through the console runner (src\Test.Automated).
REM
REM Usage: test.bat [test runner arguments...]
REM   test.bat                 run every suite
REM   test.bat --suite Tui     run one suite (SharedClient, Tray, Desktop, Tui, ClientCli, ...)
setlocal
call "%~dp0env.bat"
echo [test] Building the test runner (%XENO_CONFIGURATION%)...
dotnet build "%XENO_REPO_ROOT%\src\Test.Automated" -c %XENO_CONFIGURATION% --nologo -v quiet
if errorlevel 1 exit /b 1
echo [test] Running tests...
dotnet run --project "%XENO_REPO_ROOT%\src\Test.Automated" -c %XENO_CONFIGURATION% --no-build -- %*
exit /b %ERRORLEVEL%
