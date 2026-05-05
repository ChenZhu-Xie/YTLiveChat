using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
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

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.FormatterName = GrayCategoryConsoleFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<GrayCategoryConsoleFormatter, ConsoleFormatterOptions>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<StatusStore>();

var app = builder.Build();

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "KanBan 看板";

const string reset = "\x1b[0m";
const string dim = "\x1b[90m";
const string label = "\x1b[38;2;224;175;104m";
const string overlayUrl = "\x1b[38;2;122;162;247m";
const string adminUrl = "\x1b[38;2;187;154;247m";
const string apiUrl = "\x1b[38;2;125;207;255m";

Console.WriteLine();
Console.WriteLine($"{dim} ----------------------------------------{reset}");
Console.WriteLine($" {label}URL:{reset} {overlayUrl}http://localhost:5150{reset}");
Console.WriteLine($"      {adminUrl}http://localhost:5150/admin.html{reset} {dim}Ctrl + F5 刷新{reset}");
Console.WriteLine($"      {apiUrl}http://localhost:5150/api/status{reset}");
Console.WriteLine($"{dim} ----------------------------------------{reset}");
Console.WriteLine();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!string.Equals(Path.GetExtension(context.File.Name), ".html", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});

app.MapGet("/api/status", (StatusStore store) => Results.Json(store.ToClientState()));

app.MapHub<StatusHub>("/statusHub");

app.Run();

internal sealed class GrayCategoryConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "gray-category";
    private const string Reset = "\x1b[0m";
    private const string Dim = "\x1b[90m";
    private const string Info = "\x1b[38;2;158;206;106m";
    private const string Warn = "\x1b[38;2;224;175;104m";
    private const string Error = "\x1b[38;2;247;118;142m";

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        string message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
        {
            return;
        }

        (string levelName, string levelColor) = logEntry.LogLevel switch
        {
            LogLevel.Trace => ("trce", Dim),
            LogLevel.Debug => ("dbug", Dim),
            LogLevel.Information => ("info", Info),
            LogLevel.Warning => ("warn", Warn),
            LogLevel.Error => ("fail", Error),
            LogLevel.Critical => ("crit", Error),
            _ => ("    ", Reset)
        };

        textWriter.Write(levelColor);
        textWriter.Write(levelName);
        textWriter.Write(':');
        textWriter.Write(Reset);
        textWriter.Write(' ');
        textWriter.Write(Dim);
        textWriter.Write(logEntry.Category);
        textWriter.Write('[');
        textWriter.Write(logEntry.EventId.Id);
        textWriter.Write(']');
        textWriter.WriteLine(Reset);

        if (!string.IsNullOrEmpty(message))
        {
            textWriter.Write("      ");
            textWriter.WriteLine(message);
        }

        if (logEntry.Exception is not null)
        {
            textWriter.WriteLine(logEntry.Exception);
        }
    }
}
