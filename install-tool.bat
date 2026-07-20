@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\Xeno.Client\Xeno.Client.csproj"
set "PACKAGE_DIR=%ROOT%artifacts\tools"

echo Removing existing xeno global tool installation, if present...
call :uninstallIfInstalled xeno || exit /b 1
call :uninstallIfInstalled Xeno.Client || exit /b 1

if not exist "%PACKAGE_DIR%" mkdir "%PACKAGE_DIR%"
del /q "%PACKAGE_DIR%\xeno*.nupkg" >nul 2>nul
del /q "%PACKAGE_DIR%\Xeno.Client*.nupkg" >nul 2>nul

echo Building xeno from source...
pushd "%ROOT%dashboard" || exit /b 1
call npm.cmd install || exit /b 1
call npm.cmd run build:local || exit /b 1
popd
dotnet restore "%PROJECT%" || exit /b 1
dotnet build "%PROJECT%" -c Release || exit /b 1

echo Packing xeno global tool...
dotnet pack "%PROJECT%" -c Release --no-build -o "%PACKAGE_DIR%" || exit /b 1

echo Installing xeno as a global tool...
dotnet tool install --global xeno --add-source "%PACKAGE_DIR%" || exit /b 1
call :verifyInstalledCommand || exit /b 1

echo.
echo xeno installed successfully.
echo Run "xeno help" to verify the tool is available on your PATH.

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
  echo If xeno is running, stop it before reinstalling:
  echo   xeno stop
  echo If xeno is not available on PATH, stop the ProcessId recorded in %USERPROFILE%\.xeno\state.json.
  exit /b 1
)
exit /b 0

:verifyInstalledCommand
set "TOOL_COMMAND=%USERPROFILE%\.dotnet\tools\xeno.exe"
if exist "%TOOL_COMMAND%" exit /b 0

echo.
echo xeno was installed by dotnet, but %TOOL_COMMAND% was not created.
echo Run "dotnet tool list --global" and reinstall after resolving any dotnet tool errors.
exit /b 1
