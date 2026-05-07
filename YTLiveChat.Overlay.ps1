param(
    [switch]$Console,
    [switch]$Build,
    [switch]$Release,
    [switch]$Debug,
    [switch]$StopOnly
)

$ErrorActionPreference = "Stop"

if ($Release -and $Debug) {
    throw "Use either -Release or -Debug, not both."
}

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectName = "YTLiveChat.Overlay"
$ProjectDir = Join-Path $ProjectRoot $ProjectName
$ProjectFile = Join-Path $ProjectDir "$ProjectName.csproj"
$ProfileDir = if ($Release) { "Release" } else { "Debug" }
$ExePath = Join-Path $ProjectDir "bin\$ProfileDir\net10.0\$ProjectName.exe"
$AppArgs = @("--urls", "http://localhost:5000")

Set-Location $ProjectRoot

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
    return ,$args
}

Get-Process $ProjectName -ErrorAction SilentlyContinue | Stop-Process -Force
Stop-LauncherHosts

if ($StopOnly) {
    exit 0
}

# Default to rebuilding before every launch so hidden restarts pick up local edits.
& dotnet @(Get-DotnetArgs -Command "build")

if ($Console) {
    & $ExePath @AppArgs
} else {
    Start-Process -FilePath $ExePath -ArgumentList $AppArgs -WorkingDirectory $ProjectDir -WindowStyle Hidden
}
