@echo off
setlocal

set "REMOVED=0"

echo Removing xeno global tool...
call :uninstallIfInstalled xeno || exit /b 1

call :uninstallIfInstalled Xeno.Client || exit /b 1

if "%REMOVED%"=="0" (
  echo xeno global tool was not installed.
) else (
  echo xeno global tool removed.
)

endlocal
exit /b 0

:uninstallIfInstalled
set "PACKAGE_ID=%~1"
set "TOOL_INSTALLED="
for /f "skip=2 tokens=1" %%I in ('dotnet tool list --global 2^>nul') do (
  if /I "%%I"=="%PACKAGE_ID%" set "TOOL_INSTALLED=1"
)
if not defined TOOL_INSTALLED exit /b 0

dotnet tool uninstall --global "%PACKAGE_ID%"
if errorlevel 1 (
  echo.
  echo Failed to uninstall %PACKAGE_ID%.
  echo If xeno is running, stop it before removing the tool:
  echo   xeno stop
  echo If xeno is not available on PATH, stop the ProcessId recorded in %USERPROFILE%\.xeno\state.json.
  exit /b 1
)
set "REMOVED=1"
exit /b 0
