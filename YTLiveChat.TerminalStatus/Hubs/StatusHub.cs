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
    private static readonly TimeSpan EditorLockDuration = TimeSpan.FromSeconds(4);

    public string CurrentStatus { get; set; } = "Initializing...";
    public int CursorPosition { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionEnd { get; set; }
    public string SelectionDirection { get; set; } = "none";
    public long Revision { get; private set; }
    public string CurrentTitle { get; set; } = "KanBan 看板";
    public string PersistencePath => PersistenceFilePath;

    private string? EditorTabId { get; set; }
    private DateTimeOffset? EditorLockExpiresAt { get; set; }

    private sealed class PersistenceData
    {
        public string CurrentStatus { get; set; } = string.Empty;
        public int CursorPosition { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionEnd { get; set; }
        public string SelectionDirection { get; set; } = "none";
        public long Revision { get; set; }
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
                SelectionStart = Math.Clamp(SelectionStart, 0, CurrentStatus.Length),
                SelectionEnd = Math.Clamp(SelectionEnd, 0, CurrentStatus.Length),
                SelectionDirection = NormalizeSelectionDirection(SelectionDirection),
                Revision = Revision,
                CurrentTitle = CurrentTitle
            };

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PersistenceFilePath, json);
            Console.WriteLine($"\n\x1b[90m[StatusStore]\x1b[0m Saved state to: {PersistenceFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\x1b[31m[Error]\x1b[0m Failed to save status: {ex.Message}");
        }
    }

    public bool TryBeginUpdate(long revision)
    {
        if (revision <= 0)
        {
            Revision += 1;
            return true;
        }

        if (revision < Revision)
        {
            return false;
        }

        Revision = revision;
        return true;
    }

    public void SetSelection(int cursorPosition, int? selectionStart, int? selectionEnd, string? selectionDirection)
    {
        int normalizedStart = Math.Clamp(selectionStart ?? cursorPosition, 0, CurrentStatus.Length);
        int normalizedEnd = Math.Clamp(selectionEnd ?? normalizedStart, 0, CurrentStatus.Length);
        if (normalizedStart > normalizedEnd)
        {
            (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        }

        SelectionStart = normalizedStart;
        SelectionEnd = normalizedEnd;
        SelectionDirection = NormalizeSelectionDirection(selectionDirection);
        CursorPosition = Math.Clamp(cursorPosition, 0, CurrentStatus.Length);
    }

    public bool TryAcquireEditorLock(string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            return false;
        }

        if (HasActiveEditorLock() && !string.Equals(EditorTabId, tabId, StringComparison.Ordinal))
        {
            return false;
        }

        EditorTabId = tabId;
        EditorLockExpiresAt = DateTimeOffset.UtcNow.Add(EditorLockDuration);
        return true;
    }

    public bool RenewEditorLock(string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            return false;
        }

        if (HasActiveEditorLock() && !string.Equals(EditorTabId, tabId, StringComparison.Ordinal))
        {
            return false;
        }

        EditorTabId = tabId;
        EditorLockExpiresAt = DateTimeOffset.UtcNow.Add(EditorLockDuration);
        return true;
    }

    public bool ReleaseEditorLock(string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId) || !string.Equals(EditorTabId, tabId, StringComparison.Ordinal))
        {
            return false;
        }

        EditorTabId = null;
        EditorLockExpiresAt = null;
        return true;
    }

    public bool CanEdit(string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            return true;
        }

        return !HasActiveEditorLock() || string.Equals(EditorTabId, tabId, StringComparison.Ordinal);
    }

    public object ToClientState()
    {
        return new
        {
            title = CurrentTitle,
            status = CurrentStatus,
            cursorPosition = Math.Clamp(CursorPosition, 0, CurrentStatus.Length),
            selectionStart = Math.Clamp(SelectionStart, 0, CurrentStatus.Length),
            selectionEnd = Math.Clamp(SelectionEnd, 0, CurrentStatus.Length),
            selectionDirection = NormalizeSelectionDirection(SelectionDirection),
            revision = Revision,
            editorTabId = HasActiveEditorLock() ? EditorTabId : null,
            editorLockExpiresAt = HasActiveEditorLock() ? EditorLockExpiresAt : null
        };
    }

    public (string? EditorTabId, DateTimeOffset? ExpiresAt) GetEditorLockState()
    {
        return HasActiveEditorLock()
            ? (EditorTabId, EditorLockExpiresAt)
            : (null, null);
    }

    private bool HasActiveEditorLock()
    {
        if (string.IsNullOrWhiteSpace(EditorTabId) || EditorLockExpiresAt is null)
        {
            return false;
        }

        if (EditorLockExpiresAt <= DateTimeOffset.UtcNow)
        {
            EditorTabId = null;
            EditorLockExpiresAt = null;
            return false;
        }

        return true;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(PersistenceFilePath))
            {
                return;
            }

            string json = File.ReadAllText(PersistenceFilePath);
            PersistenceData? data = JsonSerializer.Deserialize<PersistenceData>(json);
            if (data is null)
            {
                return;
            }

            CurrentStatus = !string.IsNullOrEmpty(data.CurrentStatus) ? data.CurrentStatus : CurrentStatus;
            CursorPosition = Math.Clamp(data.CursorPosition, 0, CurrentStatus.Length);
            int persistedSelectionStart = data.SelectionStart;
            int persistedSelectionEnd = data.SelectionEnd;
            if (persistedSelectionStart == 0 && persistedSelectionEnd == 0 && CursorPosition != 0)
            {
                persistedSelectionStart = CursorPosition;
                persistedSelectionEnd = CursorPosition;
            }

            SelectionStart = Math.Clamp(persistedSelectionStart, 0, CurrentStatus.Length);
            SelectionEnd = Math.Clamp(persistedSelectionEnd, SelectionStart, CurrentStatus.Length);
            SelectionDirection = NormalizeSelectionDirection(data.SelectionDirection);
            Revision = Math.Max(0, data.Revision);
            CurrentTitle = !string.IsNullOrEmpty(data.CurrentTitle) ? data.CurrentTitle : CurrentTitle;
            Console.WriteLine($"\x1b[90m[StatusStore]\x1b[0m Loaded state from: {PersistenceFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\x1b[31m[Error]\x1b[0m Failed to load status: {ex.Message}");
        }
    }

    private static string NormalizeSelectionDirection(string? selectionDirection)
    {
        return selectionDirection is "forward" or "backward" ? selectionDirection : "none";
    }
}

public class StatusHub(StatusStore store) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await SendStatusAsync(Clients.Caller);
        await Clients.Caller.SendAsync("ReceiveTitle", store.CurrentTitle);
        await SendLockStateAsync(Clients.Caller);
        await base.OnConnectedAsync();
    }

    public async Task ClaimLock(string tabId)
    {
        store.TryAcquireEditorLock(tabId);
        await SendLockStateAsync(Clients.All);
    }

    public async Task RenewLock(string tabId)
    {
        store.RenewEditorLock(tabId);
        await SendLockStateAsync(Clients.All);
    }

    public async Task ReleaseLock(string tabId)
    {
        store.ReleaseEditorLock(tabId);
        await SendLockStateAsync(Clients.All);
    }

    public async Task UpdateStatus(
        string message,
        int cursorPosition,
        int? selectionStart = null,
        int? selectionEnd = null,
        string? selectionDirection = null,
        long revision = 0,
        string? tabId = null)
    {
        if (!store.CanEdit(tabId))
        {
            await SendStatusAsync(Clients.Caller);
            await SendLockStateAsync(Clients.Caller);
            return;
        }

        if (!string.IsNullOrWhiteSpace(tabId))
        {
            store.RenewEditorLock(tabId);
        }

        if (!store.TryBeginUpdate(revision))
        {
            await SendStatusAsync(Clients.Caller);
            return;
        }

        store.CurrentStatus = message;
        store.SetSelection(cursorPosition, selectionStart, selectionEnd, selectionDirection);
        await SendStatusAsync(Clients.All);
        await SendLockStateAsync(Clients.All);
        PrintTerminalStatus();
        store.Save();
    }

    public async Task UpdateCursor(
        int cursorPosition,
        int? selectionStart = null,
        int? selectionEnd = null,
        string? selectionDirection = null,
        long revision = 0,
        string? tabId = null)
    {
        if (!store.CanEdit(tabId))
        {
            await SendStatusAsync(Clients.Caller);
            await SendLockStateAsync(Clients.Caller);
            return;
        }

        if (!string.IsNullOrWhiteSpace(tabId))
        {
            store.RenewEditorLock(tabId);
        }

        if (!store.TryBeginUpdate(revision))
        {
            await SendStatusAsync(Clients.Caller);
            return;
        }

        store.SetSelection(cursorPosition, selectionStart, selectionEnd, selectionDirection);
        await SendStatusAsync(Clients.All);
        await SendLockStateAsync(Clients.All);
        PrintTerminalStatus();
        store.Save();
    }

    public async Task UpdateTitle(string title, string? tabId = null)
    {
        if (!store.CanEdit(tabId))
        {
            await SendLockStateAsync(Clients.Caller);
            await Clients.Caller.SendAsync("ReceiveTitle", store.CurrentTitle);
            return;
        }

        if (!string.IsNullOrWhiteSpace(tabId))
        {
            store.RenewEditorLock(tabId);
        }

        store.CurrentTitle = title;
        await Clients.All.SendAsync("ReceiveTitle", title);
        await SendLockStateAsync(Clients.All);
        PrintTerminalStatus();
        Console.Title = $"KanBan 看板 | {title}";
        store.Save();
    }

    private void PrintTerminalStatus()
    {
        string gradientTitle = ApplyGeminiGradient(store.CurrentTitle);
        string coloredStatus = ApplyStatusLineColors(store.CurrentStatus);
        Console.Write($"\r \x1b[2K > {gradientTitle}\x1b[0m | {coloredStatus}\x1b[0m (Pos: {store.CursorPosition}, Sel: {store.SelectionStart}-{store.SelectionEnd}, Rev: {store.Revision})");
    }

    private Task SendStatusAsync(IClientProxy client)
    {
        return client.SendAsync(
            "ReceiveStatus",
            store.CurrentStatus,
            store.CursorPosition,
            store.SelectionStart,
            store.SelectionEnd,
            store.SelectionDirection,
            store.Revision);
    }

    private Task SendLockStateAsync(IClientProxy client)
    {
        (string? editorTabId, DateTimeOffset? expiresAt) = store.GetEditorLockState();
        return client.SendAsync(
            "ReceiveLock",
            editorTabId,
            expiresAt?.ToUnixTimeMilliseconds());
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

    private static string ApplyStatusLineColors(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        (int R, int G, int B)[] colors =
        [
            (122, 162, 247),
            (187, 154, 247),
            (125, 207, 255),
            (224, 175, 104),
            (247, 118, 142),
            (158, 206, 106),
            (180, 249, 248),
            (255, 158, 100)
        ];

        var sb = new StringBuilder();
        int lineIndex = 0;
        AppendColor(sb, colors[lineIndex]);

        foreach (char character in text)
        {
            sb.Append(character);
            if (character == '\n')
            {
                lineIndex++;
                AppendColor(sb, colors[lineIndex % colors.Length]);
            }
        }

        return sb.ToString();
    }

    private static void AppendColor(StringBuilder sb, (int R, int G, int B) color)
    {
        sb.Append($"\x1b[38;2;{color.R};{color.G};{color.B}m");
    }
}
