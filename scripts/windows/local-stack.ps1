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
        $framework = if ($env:XENO_FRAMEWORK) { $env:XENO_FRAMEWORK } else { 'net10.0' }
        $dll = Join-Path $repo ("src\Xeno.Server\bin\{0}\{1}\Xeno.Server.dll" -f $env:XENO_CONFIGURATION, $framework)
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @("`"$dll`"", 'xeno.json') -WorkingDirectory $serverDir `
            -RedirectStandardOutput (Join-Path $logs 'run-local-server.log') -RedirectStandardError (Join-Path $logs 'run-local-server.err.log') `
            -WindowStyle Hidden -PassThru
        Write-Output $process.Id
    }
    'start-dashboard' {
        # npm can be npm.cmd, npm.exe, or only an npm.ps1 shim; Start-Process cannot run a .ps1 directly.
        $npm = Get-Command npm -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($npm) {
            $file = $npm.Source
            $npmArgs = @('run', 'dev')
        } else {
            $script = Get-Command npm -CommandType ExternalScript -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $script) { throw 'npm was not found on PATH (install Node.js 20 or later).' }
            $file = (Get-Process -Id $PID).Path
            $npmArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$($script.Source)`"", 'run', 'dev')
        }
        $process = Start-Process -FilePath $file -ArgumentList $npmArgs -WorkingDirectory (Join-Path $repo 'dashboard') `
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
