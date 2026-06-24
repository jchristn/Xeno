@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-server.bat ^<docker-image-tag^>
  exit /b 1
)

set "IMAGE_TAG=%~1"
docker build -f src/Xeno.Server/Dockerfile -t jchristn77/xeno-server:%IMAGE_TAG% -t jchristn77/xeno-server:latest . || exit /b 1
endlocal
