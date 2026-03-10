using Microsoft.AspNetCore.SignalR;
using System.Text;

namespace YTLiveChat.TerminalStatus.Hubs;

public class StatusStore
{
    public string CurrentStatus { get; set; } = "Initializing...";
    public int CursorPosition { get; set; } = 0;
    public string CurrentTitle { get; set; } = "KanBan 看板";
}

public class StatusHub(StatusStore store) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ReceiveStatus", store.CurrentStatus, store.CursorPosition);
        await Clients.Caller.SendAsync("ReceiveTitle", store.CurrentTitle);
        await base.OnConnectedAsync();
    }

    public async Task UpdateStatus(string message, int cursorPosition)
    {
        store.CurrentStatus = message;
        store.CursorPosition = cursorPosition;
        await Clients.All.SendAsync("ReceiveStatus", message, cursorPosition);
        PrintTerminalStatus();
    }

    public async Task UpdateTitle(string title)
    {
        store.CurrentTitle = title;
        await Clients.All.SendAsync("ReceiveTitle", title);
        PrintTerminalStatus();
        Console.Title = $"KanBan 看板 | {title}";
    }

    private void PrintTerminalStatus()
    {
        string gradientTitle = ApplyGeminiGradient(store.CurrentTitle);
        // 使用 \r 回到行首并清除当前行，实现“同步更新”而不刷屏
        Console.Write($"\r \x1b[2K > {gradientTitle}\x1b[0m | {store.CurrentStatus} (Pos: {store.CursorPosition})");
    }

    private string ApplyGeminiGradient(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var sb = new StringBuilder();
        int length = text.Length;
        
        // 起始颜色 #4a95e3: (74, 149, 227)
        // 结束颜色 #c16781: (193, 103, 129)
        for (int i = 0; i < length; i++)
        {
            double ratio = length > 1 ? (double)i / (length - 1) : 1.0;
            
            int r = (int)(74 + ratio * (193 - 74));
            int g = (int)(149 + ratio * (103 - 149));
            int b = (int)(227 + ratio * (129 - 227));
            
            sb.Append($"\x1b[38;2;{r};{g};{b}m{text[i]}");
        }
        
        return sb.ToString();
    }
}
