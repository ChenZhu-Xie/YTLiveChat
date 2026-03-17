# 1. 强力清理残留进程 (针对本项目的 .exe 和 PowerShell 进程)
$ScriptPath = $MyInvocation.MyCommand.Definition
$AppName = "YTLiveChat.TerminalStatus"

# A. 清理应用程序进程 (解开文件锁)
Get-Process $AppName -ErrorAction SilentlyContinue | Stop-Process -Force

# B. 通过命令行清理 (清理运行本脚本的 PowerShell，但排除当前窗口)
if (Get-Command Get-CimInstance -ErrorAction SilentlyContinue) {
    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { 
        ($_.Name -eq "powershell.exe" -or $_.Name -eq "pwsh.exe") -and 
        $_.CommandLine -like "*$ScriptPath*" -and 
        $_.ProcessId -ne $PID 
    } | ForEach-Object { 
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue 
    }
}
Start-Sleep -Seconds 1

dotnet run
