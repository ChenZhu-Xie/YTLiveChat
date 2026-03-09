using Microsoft.AspNetCore.SignalR;

namespace YTLiveChat.TerminalStatus.Hubs;

public class StatusStore
{
    public string CurrentStatus { get; set; } = "Initializing...";
    public int CursorPosition { get; set; } = 0;
    public string CurrentTitle { get; set; } = "NVIM | status.txt";
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
    }

    public async Task UpdateTitle(string title)
    {
        store.CurrentTitle = title;
        await Clients.All.SendAsync("ReceiveTitle", title);
    }
}
