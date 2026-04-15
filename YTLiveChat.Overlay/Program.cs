using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

chatService.InitialPageLoaded += (s, e) => Console.WriteLine($"[系统] 已连接到直播间: {e.LiveId}");

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

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {authorName}: {messageText}");

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
    Console.WriteLine($"[错误] {DateTime.Now:HH:mm:ss} - {e.GetException().Message}");
};

// --- 自动重连逻辑 ---
chatService.ChatStopped += async (s, e) =>
{
    Console.WriteLine($"[系统] {DateTime.Now:HH:mm:ss} - 监控已停止。原因: {e.Reason}");
    
    // 延迟 30 秒后尝试重连，避免频繁请求
    const int reconnectDelayMs = 30000;
    Console.WriteLine($"[系统] 将在 {reconnectDelayMs / 1000} 秒后尝试自动重新连接...");
    
    await Task.Delay(reconnectDelayMs);
    
    Console.WriteLine($"[系统] 正在尝试重新启动监控...");
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

Console.WriteLine("Starting YTLiveChat Overlay (Monitor Mode)...");
chatService.Start(handle: "@xczphysics");

app.Run();
