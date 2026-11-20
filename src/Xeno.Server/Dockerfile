FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# The version is set once, in src/Directory.Build.props, which every project under src/ inherits.
COPY src/Directory.Build.props ./
COPY src/Xeno.Core/Xeno.Core.csproj Xeno.Core/
COPY src/Xeno.Email/Xeno.Email.csproj Xeno.Email/
COPY src/Xeno.Server/Xeno.Server.csproj Xeno.Server/
RUN dotnet restore Xeno.Server/Xeno.Server.csproj
COPY assets/ /assets/
COPY src/Xeno.Core/ Xeno.Core/
COPY src/Xeno.Email/ Xeno.Email/
COPY src/Xeno.Server/ Xeno.Server/
WORKDIR /src/Xeno.Server
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 9000
ENTRYPOINT ["dotnet", "Xeno.Server.dll", "xeno.json"]
