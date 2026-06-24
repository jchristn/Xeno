@echo off
setlocal
if "%~1"=="" (
  echo Usage: build-dashboard.bat ^<docker-image-tag^>
  exit /b 1
)

set "IMAGE_TAG=%~1"
docker build -f dashboard/Dockerfile -t jchristn77/xeno-dashboard:%IMAGE_TAG% -t jchristn77/xeno-dashboard:latest dashboard || exit /b 1
endlocal
