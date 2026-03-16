using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using YTLiveChat.TerminalStatus.Hubs;

// 1. 自动化环境清理 (仅限 Windows)
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        var currentPid = Environment.ProcessId;
        var processName = "YTLiveChat.TerminalStatus";
        var port = 5150;
        
        // 同时清理：1. 占用端口的进程  2. 同名的其他残留进程 (防止文件锁定)
        var killCommand = $"-Command \"Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | ForEach-Object {{ if ($_ -ne {currentPid}) {{ Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }} }}; Get-Process -Name '{processName}' -ErrorAction SilentlyContinue | ForEach-Object {{ if ($_.Id -ne {currentPid}) {{ Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }} }}\"";
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = killCommand,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();
    }
    catch { /* 忽略任何清理过程中的错误 */ }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<StatusStore>();

var app = builder.Build();

// 2. 设置控制台初始状态
Console.OutputEncoding = Encoding.UTF8;
Console.Title = "KanBan 看板";

// 3. 打印状态
Console.WriteLine();
Console.WriteLine(" ----------------------------------------");
Console.WriteLine($" URL: \x1b[36mhttp://localhost:5150\x1b[0m");
Console.WriteLine($"      \x1b[36mhttp://localhost:5150/admin.html\x1b[0m");
Console.WriteLine(" ----------------------------------------");
Console.WriteLine();

// 关键：必须在 UseStaticFiles 之前调用，才能让 / 访问到 index.html
app.UseDefaultFiles(); 
app.UseStaticFiles();

app.MapHub<StatusHub>("/statusHub");

app.Run();
