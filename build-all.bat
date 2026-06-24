@echo off
setlocal
call build-server.bat || exit /b 1
call build-dashboard.bat || exit /b 1
dotnet build src/Xeno.sln || exit /b 1
dotnet run --project src/Test.Automated -- --results results.json || exit /b 1
dotnet test src/Test.Xunit/Test.Xunit.csproj || exit /b 1
dotnet test src/Test.Nunit/Test.Nunit.csproj || exit /b 1
dotnet pack src/Xeno.Client/Xeno.Client.csproj -c Release -o artifacts/packages || exit /b 1
pushd dashboard
npm audit --audit-level=low || exit /b 1
npm run build || exit /b 1
popd
docker compose -f docker/compose.yaml config > nul || exit /b 1
endlocal
