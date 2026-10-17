param(
    [switch]$Console,
    [switch]$Build,
    [switch]$Release,
    [switch]$Debug,
    [switch]$StopOnly,
    [switch]$SkipObsRefresh,
    [string]$ObsBrowserSourceName,
    [string]$ObsWebSocketUrl,
    [string]$ObsWebSocketPassword = $env:OBS_WEBSOCKET_PASSWORD
)

$ErrorActionPreference = "Stop"

if ($Release -and $Debug) {
    throw "Use either -Release or -Debug, not both."
}

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectName = "YTLiveChat.TerminalStatus"
$ProjectFile = Join-Path $ProjectDir "$ProjectName.csproj"
$ProfileDir = if ($Release) { "Release" } else { "Debug" }
$ExePath = Join-Path $ProjectDir "bin\$ProfileDir\net10.0\$ProjectName.exe"
$AppArgs = @("--urls", "http://localhost:5150")
$ProjectRoot = Split-Path -Parent $ProjectDir
$ObsModulePath = Join-Path $ProjectRoot "scripts\modules\ObsBrowserSource\ObsBrowserSource.psd1"

Import-Module $ObsModulePath -Force

Set-Location $ProjectDir

function Stop-LauncherHosts {
    if (-not (Get-Command Get-CimInstance -ErrorAction SilentlyContinue)) {
        return
    }

    $scriptPath = $MyInvocation.MyCommand.Definition
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Name -eq "powershell.exe" -or $_.Name -eq "pwsh.exe") -and
            $_.CommandLine -like "*$scriptPath*" -and
            $_.ProcessId -ne $PID
        } |
        ForEach-Object {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
}

function Get-DotnetArgs {
    param(
        [string]$Command
    )

    $args = @($Command, $ProjectFile, "-c", $ProfileDir)
    return $args
}

Get-Process $ProjectName -ErrorAction SilentlyContinue | Stop-Process -Force
Stop-LauncherHosts

if ($StopOnly) {
    exit 0
}

# Default to rebuilding before every launch so hidden restarts pick up local edits.
& dotnet @(Get-DotnetArgs -Command "build")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if ($Console) {
    $appProcess = Start-Process `
        -FilePath $ExePath `
        -ArgumentList $AppArgs `
        -WorkingDirectory $ProjectDir `
        -NoNewWindow `
        -PassThru
    try {
        Complete-BackendLaunch `
            -ApplicationName $ProjectName `
            -HealthUri "http://localhost:5150/api/status" `
            -BrowserSourceUri "http://localhost:5150" `
            -Process $appProcess `
            -SkipObsRefresh:$SkipObsRefresh `
            -ObsBrowserSourceName $ObsBrowserSourceName `
            -ObsWebSocketUrl $ObsWebSocketUrl `
            -ObsWebSocketPassword $ObsWebSocketPassword
        Wait-Process -Id $appProcess.Id
    }
    finally {
        if (-not $appProcess.HasExited) {
            Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }
    exit $appProcess.ExitCode
} else {
    $appProcess = Start-Process `
        -FilePath $ExePath `
        -ArgumentList $AppArgs `
        -WorkingDirectory $ProjectDir `
        -WindowStyle Hidden `
        -PassThru
    try {
        Complete-BackendLaunch `
            -ApplicationName $ProjectName `
            -HealthUri "http://localhost:5150/api/status" `
            -BrowserSourceUri "http://localhost:5150" `
            -Process $appProcess `
            -SkipObsRefresh:$SkipObsRefresh `
            -ObsBrowserSourceName $ObsBrowserSourceName `
            -ObsWebSocketUrl $ObsWebSocketUrl `
            -ObsWebSocketPassword $ObsWebSocketPassword
    }
    catch {
        if (-not $appProcess.HasExited) {
            Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}
