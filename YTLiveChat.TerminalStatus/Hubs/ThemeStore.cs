using System.Text.Json;

namespace YTLiveChat.TerminalStatus.Hubs;

public sealed class ThemeStore
{
    public const int DefaultAutoCycleMs = 100_000;
    public const int StagePlaybackMs = 100_000;

    private static readonly string[] OrderedThemeIds =
    [
        "mist-cottage",
        "rain-greenhouse",
        "harvest-barn",
        "winter-kitchen"
    ];

    private static readonly string[] PlaybackThemes =
    [
        "mist-cottage", "mist-cottage", "mist-cottage",
        "rain-greenhouse", "rain-greenhouse", "rain-greenhouse", "rain-greenhouse",
        "harvest-barn", "harvest-barn", "harvest-barn",
        "winter-kitchen", "winter-kitchen", "winter-kitchen"
    ];

    public static IReadOnlyList<string> ThemeIds => OrderedThemeIds;

    private const string PersistenceFileName = "theme_persistence.json";
    private static readonly string PersistenceDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YTLiveChat.TerminalStatus");
    private static readonly string PersistenceFilePath = Path.Combine(PersistenceDirectory, PersistenceFileName);
    private readonly Lock _gate = new();
    private string _baseTheme = "harvest-barn";
    private long _changedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public ThemeStore()
    {
        Load();
    }

    public object ToClientState()
    {
        lock (_gate)
        {
            return CreateClientState();
        }
    }

    public object Change(string direction)
    {
        lock (_gate)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string currentTheme = ResolveThemeAt(now);
            int currentIndex = Array.IndexOf(OrderedThemeIds, currentTheme);
            int offset = direction switch
            {
                "next" => 1,
                "previous" => -1,
                _ when ThemeIds.Contains(direction) => Array.IndexOf(OrderedThemeIds, direction) - currentIndex,
                _ => throw new ArgumentOutOfRangeException(nameof(direction))
            };

            _baseTheme = ThemeIds[(currentIndex + offset + ThemeIds.Count) % ThemeIds.Count];
            _changedAt = now;
            Save();
            return CreateClientState();
        }
    }

    public bool Supports(string direction)
    {
        return direction is "next" or "previous" || ThemeIds.Contains(direction);
    }

    private object CreateClientState()
    {
        return new
        {
            theme = _baseTheme,
            changedAt = _changedAt,
            autoCycleMs = StagePlaybackMs,
            transitionMs = 1_200,
            themeIds = ThemeIds
        };
    }

    private string ResolveThemeAt(long timestamp)
    {
        int baseIndex = Array.IndexOf(PlaybackThemes, _baseTheme);
        if (baseIndex < 0) baseIndex = 0;
        long elapsed = Math.Max(0, timestamp - _changedAt);
        long playlistDuration = PlaybackThemes.Length * (long)StagePlaybackMs;
        long cycleElapsed = elapsed % playlistDuration;
        int slot = (int)(cycleElapsed / StagePlaybackMs);
        return PlaybackThemes[(baseIndex + slot) % PlaybackThemes.Length];
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(PersistenceFilePath))
            {
                return;
            }

            PersistenceData? data = JsonSerializer.Deserialize<PersistenceData>(
                File.ReadAllText(PersistenceFilePath));
            if (data is null || !ThemeIds.Contains(data.Theme))
            {
                return;
            }

            _baseTheme = data.Theme;
            _changedAt = data.ChangedAt > 0 ? data.ChangedAt : _changedAt;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\x1b[31m[Error]\x1b[0m Failed to load theme: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(PersistenceDirectory);
            PersistenceData data = new()
            {
                Theme = _baseTheme,
                ChangedAt = _changedAt
            };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PersistenceFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\x1b[31m[Error]\x1b[0m Failed to save theme: {ex.Message}");
        }
    }

    private sealed class PersistenceData
    {
        public string Theme { get; set; } = string.Empty;
        public long ChangedAt { get; set; }
    }
}
