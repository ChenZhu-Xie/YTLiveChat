using Microsoft.AspNetCore.SignalR;
using System.Text;
using System.Text.Json;

namespace YTLiveChat.TerminalStatus.Hubs;

public class StatusStore
{
    private const string PersistenceFileName = "status_persistence.json";
    private static readonly string PersistenceDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YTLiveChat.TerminalStatus");
    private static readonly string PersistenceFilePath = Path.Combine(PersistenceDirectory, PersistenceFileName);

    public string CurrentStatus { get; set; } = "Initializing...";
    public int CursorPosition { get; set; }
    public string CurrentTitle { get; set; } = "KanBan 看板";
    public string PersistencePath => PersistenceFilePath;

    private sealed class PersistenceData
    {
        public string CurrentStatus { get; set; } = string.Empty;
        public int CursorPosition { get; set; }
        public string CurrentTitle { get; set; } = string.Empty;
    }

    public StatusStore()
    {
        Load();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(PersistenceDirectory);

            var data = new PersistenceData
            {
                CurrentStatus = CurrentStatus,
                CursorPosition = Math.Clamp(CursorPosition, 0, CurrentStatus.Length),
                CurrentTitle = CurrentTitle
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PersistenceFilePath, json);
            Console.WriteLine($"\n\x1b[90m[StatusStore]\x1b[0m Saved state to: {PersistenceFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\x1b[31m[Error]\x1b[0m Failed to save status: {ex.Message}");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(PersistenceFilePath))
            {
                return;
            }

            var json = File.ReadAllText(PersistenceFilePath);
            var data = JsonSerializer.Deserialize<PersistenceData>(json);
            if (data is null)
            {
                return;
            }

            CurrentStatus = !string.IsNullOrEmpty(data.CurrentStatus) ? data.CurrentStatus : CurrentStatus;
            CursorPosition = Math.Clamp(data.CursorPosition, 0, CurrentStatus.Length);
            CurrentTitle = !string.IsNullOrEmpty(data.CurrentTitle) ? data.CurrentTitle : CurrentTitle;
            Console.WriteLine($"\x1b[90m[StatusStore]\x1b[0m Loaded state from: {PersistenceFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\x1b[31m[Error]\x1b[0m Failed to load status: {ex.Message}");
        }
    }
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
        store.CursorPosition = Math.Clamp(cursorPosition, 0, message.Length);
        await Clients.All.SendAsync("ReceiveStatus", store.CurrentStatus, store.CursorPosition);
        PrintTerminalStatus();
        store.Save();
    }

    public async Task UpdateTitle(string title)
    {
        store.CurrentTitle = title;
        await Clients.All.SendAsync("ReceiveTitle", title);
        PrintTerminalStatus();
        Console.Title = $"KanBan 看板 | {title}";
        store.Save();
    }

    private void PrintTerminalStatus()
    {
        string gradientTitle = ApplyGeminiGradient(store.CurrentTitle);
        Console.Write($"\r \x1b[2K > {gradientTitle}\x1b[0m | {store.CurrentStatus} (Pos: {store.CursorPosition})");
    }

    private static string ApplyGeminiGradient(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder();
        int length = text.Length;

        for (int i = 0; i < length; i++)
        {
            double ratio = length > 1 ? (double)i / (length - 1) : 1.0;
            int r = (int)(74 + (ratio * (193 - 74)));
            int g = (int)(149 + (ratio * (103 - 149)));
            int b = (int)(227 + (ratio * (129 - 227)));
            sb.Append($"\x1b[38;2;{r};{g};{b}m{text[i]}");
        }

        return sb.ToString();
    }
}
