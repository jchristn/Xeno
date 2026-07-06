@echo off
setlocal
pushd "%~dp0\.."

echo This will reset the Xeno Docker deployment to factory defaults.
echo Docker containers, volumes, local runtime data, and logs for this compose project will be removed.
echo The docker\xeno.json settings file will be overwritten from factory defaults.
set "CONFIRM="
set /P "CONFIRM=Type RESET to continue: "
if not "%CONFIRM%"=="RESET" (
  echo Reset cancelled.
  popd
  endlocal
  exit /b 1
)

docker compose -f compose.yaml down --volumes --remove-orphans
if exist data rmdir /S /Q data
if exist logs rmdir /S /Q logs
if not exist logs mkdir logs
copy /Y factory\xeno.json xeno.json >NUL
docker compose -f compose.yaml up -d --build
popd
endlocal
