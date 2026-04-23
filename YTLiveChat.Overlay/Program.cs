using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using YTLiveChat.Contracts.Services;
using YTLiveChat.DependencyInjection;

// 1. 自动化环境清理 (仅限 Windows)
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        var currentPid = Environment.ProcessId;
        var processName = "YTLiveChat.Overlay";
        
        // 清理同名的其他残留进程 (防止构建时的文件锁定)
        var killCommand = $"-Command \"Get-Process -Name '{processName}' -ErrorAction SilentlyContinue | ForEach-Object {{ if ($_.Id -ne {currentPid}) {{ Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }} }}\"";
        
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
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.FormatterName = GrayCategoryConsoleFormatter.FormatterName);
builder.Logging.AddConsoleFormatter<GrayCategoryConsoleFormatter, ConsoleFormatterOptions>();
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
builder.Services.AddYTLiveChat(builder.Configuration);

// 配置选项
builder.Services.Configure<YTLiveChat.Contracts.YTLiveChatOptions>(options => {
    options.RequestFrequency = 1000;
#pragma warning disable CS0618 // 开启持续监控模式 (BETA)
    options.EnableContinuousLivestreamMonitor = true;
    options.LiveCheckFrequency = 30000; // 每30秒检查一次是否开播
#pragma warning restore CS0618
});

var app = builder.Build();

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "YTLiveChat Overlay";

const string reset = "\x1b[0m";
const string dim = "\x1b[90m";
const string label = "\x1b[38;2;224;175;104m";
const string overlayUrl = "\x1b[38;2;122;162;247m";
const string testUrl = "\x1b[38;2;187;154;247m";
const string socketUrl = "\x1b[38;2;125;207;255m";
const string systemColor = "\x1b[38;2;158;206;106m";
const string errorColor = "\x1b[38;2;247;118;142m";
const string timeColor = "\x1b[38;2;86;95;137m";
const string authorColor = "\x1b[38;2;255;158;100m";
const string messageColor = "\x1b[38;2;192;202;245m";

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

app.UseWebSockets();
app.UseDefaultFiles(); // 修复 404：允许访问根目录加载 index.html
app.UseStaticFiles();

var sockets = new ConcurrentDictionary<Guid, WebSocket>();
var messageHistory = new ConcurrentQueue<string>(); // 存储最近 30 条消息的 JSON
var seenMessageIds = new ConcurrentDictionary<string, byte>();
var seenMessageIdsQueue = new ConcurrentQueue<string>();
const int MaxSeenMessagesCache = 1000;

// 监听 YouTube 聊天
var chatService = app.Services.GetRequiredService<IYTLiveChat>();

chatService.InitialPageLoaded += (s, e) =>
    Console.WriteLine($"{systemColor}[SYSTEM]{reset} Connected to live: {overlayUrl}{e.LiveId}{reset}");

chatService.ChatReceived += async (sender, e) =>
{
    // 消息去重
    if (!seenMessageIds.TryAdd(e.ChatItem.Id, 0))
    {
        return;
    }

    seenMessageIdsQueue.Enqueue(e.ChatItem.Id);
    while (seenMessageIdsQueue.Count > MaxSeenMessagesCache)
    {
        if (seenMessageIdsQueue.TryDequeue(out var oldId))
        {
            seenMessageIds.TryRemove(oldId, out _);
        }
    }

    var authorName = e.ChatItem.Author.Name;
    var messageText = string.Join("", e.ChatItem.Message.Select(p => p is YTLiveChat.Contracts.Models.TextPart t ? t.Text : (p is YTLiveChat.Contracts.Models.EmojiPart em ? em.EmojiText : "")));

    Console.WriteLine(
        $"{timeColor}[{DateTime.Now:HH:mm:ss}]{reset} {authorColor}{authorName}{reset}: {messageColor}{messageText}{reset}");

    var message = new
    {
        id = e.ChatItem.Id,
        author = authorName,
        text = messageText,
        parts = e.ChatItem.Message.Select(p => p switch
        {
            YTLiveChat.Contracts.Models.TextPart tp => (object)new { type = "text", text = tp.Text },
            YTLiveChat.Contracts.Models.EmojiPart ep => new { type = "emoji", url = ep.Url, emojiText = ep.EmojiText },
            YTLiveChat.Contracts.Models.ImagePart ip => new { type = "image", url = ip.Url, alt = ip.Alt },
            _ => new { type = "unknown" }
        }),
        isSuperChat = e.ChatItem.Superchat != null,
        amount = e.ChatItem.Superchat?.AmountString,
        isMembership = e.ChatItem.IsMembership,
        avatar = e.ChatItem.Author.Thumbnail?.Url,
        sticker = e.ChatItem.Superchat?.Sticker?.Url
    };

    var json = JsonSerializer.Serialize(message, jsonOptions);
    
    // 更新历史记录
    messageHistory.Enqueue(json);
    while (messageHistory.Count > 30) messageHistory.TryDequeue(out _);

    var bytes = Encoding.UTF8.GetBytes(json);

    foreach (var socket in sockets.Values)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
};

chatService.ErrorOccurred += (s, e) => {
    Console.WriteLine($"{errorColor}[ERROR]{reset} {timeColor}{DateTime.Now:HH:mm:ss}{reset} {e.GetException().Message}");
};

// --- 自动重连逻辑 ---
chatService.ChatStopped += async (s, e) =>
{
    Console.WriteLine($"{systemColor}[SYSTEM]{reset} {timeColor}{DateTime.Now:HH:mm:ss}{reset} Monitor stopped. Reason: {e.Reason}");
    
    // 延迟 30 秒后尝试重连，避免频繁请求
    const int reconnectDelayMs = 30000;
    Console.WriteLine($"{systemColor}[SYSTEM]{reset} Reconnecting in {reconnectDelayMs / 1000}s...");
    
    await Task.Delay(reconnectDelayMs);
    
    Console.WriteLine($"{systemColor}[SYSTEM]{reset} Restarting monitor...");
    chatService.Start(handle: "@xczphysics");
};

// WebSocket 终结点
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var id = Guid.NewGuid();
        
        // 1. 发送历史记录
        foreach (var historyJson in messageHistory)
        {
            var historyBytes = Encoding.UTF8.GetBytes(historyJson);
            await webSocket.SendAsync(new ArraySegment<byte>(historyBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        // 2. 注册到活跃连接池
        sockets.TryAdd(id, webSocket);
        try
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
        }
        finally
        {
            sockets.TryRemove(id, out _);
        }
    }
});

app.MapGet("/test", async () => {
    var testMsg = new {
        id = Guid.NewGuid().ToString(),
        author = "测试用户",
        text = "OBS 链路正常！测试 emoji: [:heart:] 和 Super Sticker",
        parts = new object[] {
            new { type = "text", text = "OBS 链路正常！测试 emoji: " },
            new { type = "emoji", url = "https://yt3.ggpht.com/8LAn6mE6S-C-6oXn-M-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8=w24-h24-c-k-nd", emojiText = "[:heart:]" },
            new { type = "text", text = " 和 Super Sticker" }
        },
        isSuperChat = true,
        amount = "￥100.00",
        isMembership = false,
        avatar = "https://www.gstatic.com/images/branding/product/1x/avatar_circle_blue_512dp.png",
        sticker = "https://yt3.ggpht.com/8LAn6mE6S-C-6oXn-M-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8-8X-8=w64-h64-c-k-nd"
    };
    var json = JsonSerializer.Serialize(testMsg, jsonOptions);
    var bytes = Encoding.UTF8.GetBytes(json);
    foreach (var socket in sockets.Values) {
        if (socket.State == WebSocketState.Open)
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    return "测试成功！";
});

Console.WriteLine();
Console.WriteLine($"{dim} ----------------------------------------{reset}");
Console.WriteLine($" {label}URL:{reset} {overlayUrl}http://localhost:5000{reset}");
Console.WriteLine($"      {testUrl}http://localhost:5000/test{reset}");
Console.WriteLine($"      {socketUrl}ws://localhost:5000/ws{reset}");
Console.WriteLine($"{dim} ----------------------------------------{reset}");
Console.WriteLine($"{systemColor}[SYSTEM]{reset} Starting YTLiveChat Overlay (monitor mode)...");
Console.WriteLine();
chatService.Start(handle: "@xczphysics");

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
