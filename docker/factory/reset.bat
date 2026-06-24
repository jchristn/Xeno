@echo off
setlocal
pushd "%~dp0\.."

echo This will reset the Xeno Docker deployment to factory defaults.
echo Docker containers and volumes for this compose project will be removed.
echo The docker\xeno.json settings file will be overwritten from factory defaults.
set "CONFIRM="
set /P "CONFIRM=Type RESET to continue: "
if not "%CONFIRM%"=="RESET" (
  echo Reset cancelled.
  popd
  endlocal
  exit /b 1
)

docker compose -f compose.yaml down -v
if not exist data mkdir data
if not exist logs mkdir logs
copy /Y factory\xeno.json xeno.json >NUL
docker compose -f compose.yaml up -d
popd
endlocal
