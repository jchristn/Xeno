@echo off
setlocal
pushd "%~dp0\.."
docker compose -f compose.yaml down -v
if not exist data mkdir data
if not exist logs mkdir logs
copy /Y factory\xeno.json xeno.json >NUL
docker compose -f compose.yaml up -d
popd
endlocal
