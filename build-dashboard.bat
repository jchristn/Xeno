@echo off
setlocal
docker build -f dashboard/Dockerfile -t jchristn77/xeno-dashboard:v0.1.0 -t jchristn77/xeno-dashboard:latest dashboard
endlocal
