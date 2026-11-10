@echo off
REM Build and run any xeno command from source.
REM
REM Usage: run-cli.bat <xeno arguments...>
REM   run-cli.bat status
REM   run-cli.bat server show
REM   run-cli.bat server use http://localhost:9000
REM   run-cli.bat login -u admin@example.com
setlocal
call "%~dp0env.bat"
call "%~dp0build-apps.bat" run-cli
if errorlevel 1 (endlocal & exit /b 1)
"%XENO_APP_DIR%\xeno.exe" %*
endlocal & exit /b %ERRORLEVEL%
