using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using YTLiveChat.Contracts;
using YTLiveChat.Contracts.Models;
using YTLiveChat.Contracts.Services;
using YTLiveChat.Services;

/// <summary>
/// Live-watch mode: connects to one or more channels/handles and captures events that are
/// unrecognized by the parser (unknown actions, unknown membership types), and optionally
/// all non-trivial events (--all-events). Produces a JSONL file containing raw action objects
/// that can be fed back into the Tools log-analysis commands.
/// </summary>
internal static class WatchMode
{
    // Action types the library handles but which produce no ChatItem (intentionally silent).
    // These are NOT unknown — filtering them avoids noise in the default capture mode.
    private static readonly HashSet<string> s_knownSkippedActionTypes = new(StringComparer.Ordinal)
    {
        "removeChatItemAction",
        "replaceChatItemAction",
        "removeChatItemByAuthorAction",
        "markChatItemsByAuthorAsDeletedAction",
        "changeEngagementPanelVisibilityAction",
        "signalAction",
        // Poll lifecycle — handled by PollStarted/PollUpdated/PollClosed events
        "showLiveChatActionPanelAction",
        "updateLiveChatPollAction",
        "closeLiveChatActionPanelAction",
        // Banner lifecycle
        "addBannerToLiveChatCommand",
        "removeBannerForLiveChatCommand",
        // Moderation state
        "liveChatReportModerationStateCommand",
    };

    // addChatItemAction item renderers the library explicitly skips (no ChatItem produced).
    private static readonly HashSet<string> s_knownSkippedRendererTypes = new(StringComparer.Ordinal)
    {
        "liveChatPlaceholderItemRenderer",
        "liveChatViewerEngagementMessageRenderer",
        "liveChatModeChangeMessageRenderer",
    };

    // For --all-events: high-volume renderers that add no analytical value. Everything
    // NOT in this list (and not tracking-only) is captured.
    private static readonly HashSet<string> s_allEventsRendererBlacklist = new(StringComparer.Ordinal)
    {
        "liveChatTextMessageRenderer",
        "liveChatPlaceholderItemRenderer",
    };

    public static async Task<int> RunAsync(string[] args)
    {
        WatchOptions options = ParseWatchOptions(args);

        if (options.Targets.Count == 0)
        {
            PrintWatchUsage();
            return 1;
        }

        string outputPath = options.OutputPath ?? BuildDefaultOutputPath();
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        Console.WriteLine($"Watch mode — output: {outputPath}");
        Console.WriteLine($"Targets ({options.Targets.Count}): {string.Join(", ", options.Targets.Select(t => t.Tag))}");
        if (options.SkippedLiveIds.Count > 0)
        {
            Console.WriteLine($"Skipping live IDs: {string.Join(", ", options.SkippedLiveIds)}");
        }

        string captureDesc = options.AllEvents
            ? "all events (except regular chat and placeholders)"
            : options.AllMembership
                ? "all membership events + unknowns"
                : "unknown actions + unknown membership types";
        Console.WriteLine($"Capturing: {captureDesc}");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // AutoFlush = true causes StreamWriter to flush its internal buffer to the underlying
        // FileStream after every write, which in turn flushes to the OS file cache.
        // Each captured event is visible on disk immediately — no batching until stop.
        using StreamWriter writer = new(outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        int[] capturedCount = [0];
        object fileLock = new();
        object consoleLock = new();

        List<(WatchTarget Target, IYTLiveChat Chat, HttpClient HttpClient)> sessions = [];

        foreach (WatchTarget target in options.Targets)
        {
#pragma warning disable CS0618
            YTLiveChatOptions ytOptions = new()
            {
                YoutubeBaseUrl = "https://www.youtube.com",
                EnableContinuousLivestreamMonitor = target.IsHandleOrChannel,
                LiveCheckFrequency = options.CheckIntervalMs,
                RequireActiveBroadcastForAutoDetectedStream = !options.IncludeScheduled,
                IgnoredAutoDetectedLiveIds = [.. options.SkippedLiveIds],
            };
#pragma warning restore CS0618

            HttpClient httpClient = new() { BaseAddress = new Uri("https://www.youtube.com") };
            YTHttpClient ytHttpClient = new(httpClient, NullLogger<YTHttpClient>.Instance);
            IYTLiveChat chat = new YTLiveChat.Services.YTLiveChat(
                ytOptions,
                ytHttpClient,
                NullLogger<YTLiveChat.Services.YTLiveChat>.Instance
            );

            sessions.Add((target, chat, httpClient));

            WireHandlers(target, chat, options, writer, fileLock, consoleLock, capturedCount);

            StartChat(target, chat);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C — expected
        }
        finally
        {
            foreach ((_, IYTLiveChat chat, HttpClient httpClient) in sessions)
            {
                chat.Stop();
                if (chat is IDisposable d)
                {
                    d.Dispose();
                }

                httpClient.Dispose();
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Stopped. {capturedCount[0]} event(s) captured → {outputPath}");

        if (capturedCount[0] > 0)
        {
            Console.WriteLine();
            Console.WriteLine("To inspect the captured events:");
            Console.WriteLine(
                $"  dotnet run --project YTLiveChat.Tools -- --variants {outputPath}"
            );
            Console.WriteLine(
                $"  dotnet run --project YTLiveChat.Tools -- --dump-renderer=liveChatMembershipItemRenderer {outputPath}"
            );
        }

        return 0;
    }

    private static void WireHandlers(
        WatchTarget target,
        IYTLiveChat chat,
        WatchOptions options,
        StreamWriter writer,
        object fileLock,
        object consoleLock,
        int[] capturedCount
    )
    {
        chat.RawActionReceived += (_, e) =>
        {
            // Get the action type. If null, this is a tracking-only entry
            // (e.g. only clickTrackingParams with no actual action/command property).
            // These carry no data and should be silently discarded in all modes.
            string? actionType = GetActionType(e.RawAction);
            if (actionType == null)
            {
                return;
            }

            bool hasItem = e.ParsedChatItem != null;
            bool isUnknownMembership =
                e.ParsedChatItem?.MembershipDetails?.EventType == MembershipEventType.Unknown;
            bool hasMembership = hasItem && e.ParsedChatItem!.MembershipDetails != null;

            string reason;
            string rendererKey = GetRendererKey(e.RawAction);

            if (options.AllEvents)
            {
                // --all-events: capture everything except the high-volume blacklisted renderers.
                if (s_allEventsRendererBlacklist.Contains(rendererKey))
                {
                    return;
                }

                if (!hasItem && s_knownSkippedActionTypes.Contains(actionType))
                {
                    reason = "known";
                }
                else if (hasMembership)
                {
                    reason = isUnknownMembership ? "unknown-membership" : "membership";
                }
                else if (hasItem)
                {
                    reason = "parsed";
                }
                else
                {
                    reason = "unknown";
                }
            }
            else
            {
                // Default mode: capture only genuinely unknown events + optional memberships.
                bool isKnownMembership = options.AllMembership && hasMembership;

                if (!hasItem)
                {
                    // Known action types the library handles without producing a ChatItem.
                    // These are NOT unknown — don't capture them.
                    if (s_knownSkippedActionTypes.Contains(actionType))
                    {
                        return;
                    }

                    // Known renderers the library intentionally ignores.
                    string? rendererType = GetRendererTypeFromItem(e.RawAction);
                    if (rendererType != null && s_knownSkippedRendererTypes.Contains(rendererType))
                    {
                        return;
                    }

                    reason = "unknown-action";
                }
                else if (isUnknownMembership)
                {
                    reason = "unknown-membership";
                }
                else if (isKnownMembership)
                {
                    reason = "membership";
                }
                else
                {
                    return;
                }
            }

            string compactJson = JsonSerializer.Serialize(e.RawAction);

            lock (fileLock)
            {
                writer.WriteLine(compactJson);
                capturedCount[0]++;
            }

            lock (consoleLock)
            {
                Console.Write('[');
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(DateTimeOffset.Now.ToString("HH:mm:ss"));
                Console.ResetColor();
                Console.Write("] ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"[{target.Tag}]");
                Console.ResetColor();
                Console.Write(' ');
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[CAPTURED]");
                Console.ResetColor();
                Console.WriteLine($" {reason} | {rendererKey}");
            }
        };

#pragma warning disable CS0618
        chat.InitialPageLoaded += (_, e) => WriteStatus(consoleLock, target.Tag, "LIVE", ConsoleColor.Green, e.LiveId);
        chat.LivestreamStarted += (_, e) => WriteStatus(consoleLock, target.Tag, "STREAM START", ConsoleColor.Green, e.LiveId);
        chat.LivestreamEnded += (_, e) => WriteStatus(consoleLock, target.Tag, "STREAM END", ConsoleColor.DarkYellow, e.LiveId);
        chat.LivestreamInaccessible += (_, e) => WriteStatus(consoleLock, target.Tag, "BLOCKED", ConsoleColor.DarkYellow, e.LiveId);
#pragma warning restore CS0618
        chat.ChatStopped += (_, e) => WriteStatus(consoleLock, target.Tag, "STOPPED", ConsoleColor.Red, e.Reason);
        chat.ErrorOccurred += (_, e) => WriteStatus(consoleLock, target.Tag, "ERROR", ConsoleColor.Red, e.GetException().Message);
    }

    private static void StartChat(WatchTarget target, IYTLiveChat chat)
    {
        if (target.Handle != null)
        {
            chat.Start(handle: target.Handle);
        }
        else if (target.ChannelId != null)
        {
            chat.Start(channelId: target.ChannelId);
        }
        else
        {
            chat.Start(liveId: target.LiveId!);
        }
    }

    private static void WriteStatus(object consoleLock, string tag, string label, ConsoleColor color, string? detail)
    {
        lock (consoleLock)
        {
            Console.Write('[');
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(DateTimeOffset.Now.ToString("HH:mm:ss"));
            Console.ResetColor();
            Console.Write("] ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"[{tag}]");
            Console.ResetColor();
            Console.Write(' ');
            Console.ForegroundColor = color;
            Console.Write($"[{label}]");
            Console.ResetColor();
            if (!string.IsNullOrWhiteSpace(detail))
            {
                Console.Write($" {detail}");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Returns the action/command type property name (e.g. "addChatItemAction"), or null
    /// if the object has no property ending in "Action"/"Command" — i.e. it's tracking-only.
    /// </summary>
    private static string? GetActionType(JsonElement action)
    {
        if (action.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty prop in action.EnumerateObject())
        {
            if (
                prop.Name.EndsWith("Action", StringComparison.Ordinal)
                || prop.Name.EndsWith("Command", StringComparison.Ordinal)
            )
            {
                return prop.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the renderer name from inside addChatItemAction.item (e.g. "liveChatTextMessageRenderer"),
    /// or null if the action is not an addChatItemAction or the item is missing.
    /// </summary>
    private static string? GetRendererTypeFromItem(JsonElement action)
    {
        if (
            !action.TryGetProperty("addChatItemAction", out JsonElement addChat)
            || !addChat.TryGetProperty("item", out JsonElement item)
            || item.ValueKind != JsonValueKind.Object
        )
        {
            return null;
        }

        using JsonElement.ObjectEnumerator enumerator = item.EnumerateObject();
        return enumerator.MoveNext() ? enumerator.Current.Name : null;
    }

    private static string GetRendererKey(JsonElement action)
    {
        if (action.ValueKind != JsonValueKind.Object)
        {
            return "?";
        }

        using JsonElement.ObjectEnumerator outer = action.EnumerateObject();
        if (!outer.MoveNext())
        {
            return "?";
        }

        // Try to dig one level deeper to get the renderer key inside addChatItemAction.item
        JsonElement actionValue = outer.Current.Value;
        if (
            actionValue.ValueKind == JsonValueKind.Object
            && actionValue.TryGetProperty("item", out JsonElement item)
            && item.ValueKind == JsonValueKind.Object
        )
        {
            using JsonElement.ObjectEnumerator itemEnum = item.EnumerateObject();
            if (itemEnum.MoveNext())
            {
                return itemEnum.Current.Name;
            }
        }

        return outer.Current.Name;
    }

    private static string BuildDefaultOutputPath()
    {
        string fileName = $"watch_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.jsonl";
        return Path.GetFullPath(Path.Combine("logs", fileName));
    }

    private static WatchOptions ParseWatchOptions(string[] args)
    {
        List<WatchTarget> targets = [];
        List<string> skippedLiveIds = [];
        int checkIntervalMs = 10000;
        bool includeScheduled = false;
        bool allMembership = false;
        bool allEvents = false;
        string? outputPath = null;

        foreach (string arg in args)
        {
            if (arg.Equals("--include-scheduled", StringComparison.OrdinalIgnoreCase))
            {
                includeScheduled = true;
                continue;
            }

            if (arg.Equals("--all-membership", StringComparison.OrdinalIgnoreCase))
            {
                allMembership = true;
                continue;
            }

            if (arg.Equals("--all-events", StringComparison.OrdinalIgnoreCase))
            {
                allEvents = true;
                continue;
            }

            const string skipPrefix = "--skip=";
            if (arg.StartsWith(skipPrefix, StringComparison.OrdinalIgnoreCase))
            {
                skippedLiveIds.AddRange(
                    arg[skipPrefix.Length..]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                );
                continue;
            }

            const string checkIntervalPrefix = "--check-interval=";
            if (arg.StartsWith(checkIntervalPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(arg[checkIntervalPrefix.Length..], out int parsed) && parsed > 0)
                {
                    checkIntervalMs = parsed;
                }

                continue;
            }

            const string outputPrefix = "--output=";
            if (arg.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                outputPath = Path.GetFullPath(arg[outputPrefix.Length..]);
                continue;
            }

            targets.Add(ParseTarget(arg));
        }

        return new WatchOptions(targets, skippedLiveIds, checkIntervalMs, includeScheduled, allMembership, allEvents, outputPath);
    }

    private static WatchTarget ParseTarget(string identifier)
    {
        if (identifier.StartsWith("@", StringComparison.Ordinal))
        {
            return new WatchTarget(identifier, Handle: identifier, ChannelId: null, LiveId: null, IsHandleOrChannel: true);
        }

        if (identifier.StartsWith("UC", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchTarget(identifier, Handle: null, ChannelId: identifier, LiveId: null, IsHandleOrChannel: true);
        }

        return new WatchTarget(identifier, Handle: null, ChannelId: null, LiveId: identifier, IsHandleOrChannel: false);
    }

    private static void PrintWatchUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine(
            "  dotnet run --project YTLiveChat.Tools -- watch [options] <@handle|UCxxx|liveId> [...]"
        );
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine(
            "  --skip=<id[,id...]>       Skip these video IDs when auto-detecting streams (comma-separated)."
        );
        Console.WriteLine(
            "  --check-interval=<ms>     How often to poll for a live stream. Default: 10000."
        );
        Console.WriteLine(
            "  --include-scheduled       Include scheduled streams and free-chat. Default: active broadcasts only."
        );
        Console.WriteLine(
            "  --all-membership          Also capture all successfully-parsed membership events, not just Unknown ones."
        );
        Console.WriteLine(
            "  --all-events              Capture all non-trivial events (memberships, superchats, polls, banners,"
        );
        Console.WriteLine(
            "                            moderation, unknowns, etc.). Skips only regular chat messages and"
        );
        Console.WriteLine(
            "                            placeholder items. Use this to verify correctness of known event types."
        );
        Console.WriteLine(
            "  --output=<path>           Output JSONL file path. Default: logs/watch_<timestamp>.jsonl."
        );
        Console.WriteLine();
        Console.WriteLine("What gets captured:");
        Console.WriteLine(
            "  - Any action the parser did not produce a ChatItem for (completely unknown renderer/action)."
        );
        Console.WriteLine(
            "  - Membership events parsed with EventType=Unknown (e.g. tier-upgrade events)."
        );
        Console.WriteLine(
            "  - With --all-membership: all membership events (New/Milestone/Gift/Redemption too)."
        );
        Console.WriteLine(
            "  - With --all-events: everything except regular chat (liveChatTextMessageRenderer)"
        );
        Console.WriteLine(
            "    and placeholder items (liveChatPlaceholderItemRenderer)."
        );
        Console.WriteLine();
        Console.WriteLine("Reason labels in console output:");
        Console.WriteLine("  unknown-action     Unrecognized action or renderer — new event type, investigate.");
        Console.WriteLine("  unknown-membership Membership event with EventType=Unknown — unrecognized subtype.");
        Console.WriteLine("  membership         Known membership event (with --all-membership or --all-events).");
        Console.WriteLine("  known              Known action type that doesn't produce a ChatItem (polls, banners, etc.).");
        Console.WriteLine("  parsed             Any other successfully-parsed ChatItem (superchats, stickers, etc.).");
        Console.WriteLine();
        Console.WriteLine("Output is a JSONL file (one raw action JSON per line) compatible with");
        Console.WriteLine("the log-analysis commands (--dump-renderer, --variants, etc.).");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine(
            "  dotnet run --project YTLiveChat.Tools -- watch @SomeStreamer @OtherStreamer"
        );
        Console.WriteLine(
            "  dotnet run --project YTLiveChat.Tools -- watch --skip=abc123,def456 --output=upgrades.jsonl @SomeStreamer"
        );
        Console.WriteLine(
            "  dotnet run --project YTLiveChat.Tools -- watch --all-events @SomeStreamer"
        );
    }

    private sealed record WatchOptions(
        IReadOnlyList<WatchTarget> Targets,
        IReadOnlyList<string> SkippedLiveIds,
        int CheckIntervalMs,
        bool IncludeScheduled,
        bool AllMembership,
        bool AllEvents,
        string? OutputPath
    );

    private sealed record WatchTarget(
        string Tag,
        string? Handle,
        string? ChannelId,
        string? LiveId,
        bool IsHandleOrChannel
    );
}
