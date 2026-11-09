# Helpers for run-local.bat: start the local Xeno server or the web dashboard in the background, or wait for the
# server to become healthy. Settings come from the environment set by env.bat.
#
# Usage:
#   local-stack.ps1 -Action start-server      prints the server process id
#   local-stack.ps1 -Action start-dashboard   prints the dashboard process id
#   local-stack.ps1 -Action wait -ProcessId <id>
#   local-stack.ps1 -Action probe             exits 0 when a server already answers (checks once)
param(
    [Parameter(Mandatory = $true)][ValidateSet('start-server', 'start-dashboard', 'wait', 'probe')][string]$Action,
    [int]$ProcessId = 0
)

$ErrorActionPreference = 'Stop'
$repo = $env:XENO_REPO_ROOT
$serverDir = $env:XENO_LOCAL_SERVER_DIR
$logs = Join-Path $serverDir 'logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null

switch ($Action) {
    'start-server' {
        $dll = Join-Path $repo ("src\Xeno.Server\bin\{0}\net10.0\Xeno.Server.dll" -f $env:XENO_CONFIGURATION)
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$dll`"", 'xeno.json') -WorkingDirectory $serverDir `
            -RedirectStandardOutput (Join-Path $logs 'run-local-server.log') -RedirectStandardError (Join-Path $logs 'run-local-server.err.log') `
            -WindowStyle Hidden -PassThru
        Write-Output $process.Id
    }
    'start-dashboard' {
        $process = Start-Process -FilePath 'npm.cmd' -ArgumentList @('run', 'dev') -WorkingDirectory (Join-Path $repo 'dashboard') `
            -RedirectStandardOutput (Join-Path $logs 'run-local-dashboard.log') -RedirectStandardError (Join-Path $logs 'run-local-dashboard.err.log') `
            -WindowStyle Hidden -PassThru
        Write-Output $process.Id
    }
    'wait' {
        $url = $env:XENO_LOCAL_SERVER.TrimEnd('/') + '/'
        for ($i = 0; $i -lt 45; $i++) {
            if ($ProcessId -gt 0 -and -not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { exit 1 }
            try {
                Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
                exit 0
            } catch {
                Start-Sleep -Seconds 1
            }
        }
        exit 1
    }
    'probe' {
        $url = $env:XENO_LOCAL_SERVER.TrimEnd('/') + '/'
        try {
            Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
            exit 0
        } catch {
            exit 1
        }
    }
}
