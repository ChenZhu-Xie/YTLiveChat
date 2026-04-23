using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using YTLiveChat.TerminalStatus.Hubs;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        int currentPid = Environment.ProcessId;
        const string processName = "YTLiveChat.TerminalStatus";
        const int port = 5150;

        string killCommand =
            $"-Command \"Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue | " +
            $"Select-Object -ExpandProperty OwningProcess | ForEach-Object {{ if ($_ -ne {currentPid}) {{ Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }} }}; " +
            $"Get-Process -Name '{processName}' -ErrorAction SilentlyContinue | ForEach-Object {{ if ($_.Id -ne {currentPid}) {{ Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }} }}\"";

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
    catch
    {
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<StatusStore>();

var app = builder.Build();

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "KanBan 看板";

Console.WriteLine();
Console.WriteLine(" ----------------------------------------");
Console.WriteLine(" URL: http://localhost:5150");
Console.WriteLine("      http://localhost:5150/admin.html");
Console.WriteLine("      http://localhost:5150/api/status");
Console.WriteLine(" ----------------------------------------");
Console.WriteLine();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", (StatusStore store) => Results.Json(new
{
    title = store.CurrentTitle,
    status = store.CurrentStatus,
    cursorPosition = Math.Clamp(store.CursorPosition, 0, store.CurrentStatus.Length),
    selectionStart = Math.Clamp(store.SelectionStart, 0, store.CurrentStatus.Length),
    selectionEnd = Math.Clamp(store.SelectionEnd, 0, store.CurrentStatus.Length),
    selectionDirection = store.SelectionDirection,
    persistencePath = store.PersistencePath
}));

app.MapHub<StatusHub>("/statusHub");

app.Run();
