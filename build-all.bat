@echo off
setlocal
call build-server.bat || exit /b 1
call build-dashboard.bat || exit /b 1
dotnet build src/Xeno.sln || exit /b 1
dotnet run --project src/Test.Automated -- --results results.json || exit /b 1
endlocal
