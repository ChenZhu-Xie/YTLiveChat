using System.Text.Json;

/// <summary>
/// Field-level baseline diffing across all renderer types and locations.
/// Flags NEW fields (observed in logs but absent from the C# response model),
/// unknown renderer types, and badge composition breakdowns.
/// </summary>
internal static class AnalyzeMode
{
    // ── Baselines ─────────────────────────────────────────────────────────────
    // Built from the C# response model records in LiveChatResponse.cs.
    // clickTrackingParams is a ubiquitous tracking sibling — included everywhere
    // so it never fires as a false-positive NEW field.

    private static readonly HashSet<string> MessageRendererBaseFields =
    [
        "id", "timestampUsec",
        "authorName", "authorPhoto", "authorBadges", "authorExternalChannelId",
        "contextMenuEndpoint", "contextMenuAccessibility",
        "trackingParams", "clickTrackingParams",
    ];

    private static readonly Dictionary<string, HashSet<string>> Baselines =
        new(StringComparer.Ordinal)
        {
            // ── addChatItemAction item renderers ──────────────────────────────
            ["liveChatTextMessageRenderer"] =
            [
                ..MessageRendererBaseFields,
                "message",
                "beforeContentButtons",
            ],
            ["liveChatPaidMessageRenderer"] =
            [
                ..MessageRendererBaseFields,
                "message",
                "purchaseAmountText",
                "headerBackgroundColor",
                "headerTextColor",
                "bodyBackgroundColor",
                "bodyTextColor",
                "authorNameTextColor",
                "timestampColor",
                "isV2Style",
                "textInputBackgroundColor",
            ],
            ["liveChatPaidStickerRenderer"] =
            [
                ..MessageRendererBaseFields,
                "sticker",
                "purchaseAmountText",
                "moneyChipBackgroundColor",
                "moneyChipTextColor",
                "backgroundColor",
                "authorNameTextColor",
                "stickerDisplayWidth",
                "stickerDisplayHeight",
                "isV2Style",
            ],
            ["liveChatMembershipItemRenderer"] =
            [
                ..MessageRendererBaseFields,
                "headerPrimaryText",
                "headerSubtext",
                "message",
            ],
            ["liveChatSponsorshipsGiftPurchaseAnnouncementRenderer"] =
            [
                ..MessageRendererBaseFields,
                "header",
            ],
            ["liveChatSponsorshipsGiftRedemptionAnnouncementRenderer"] =
            [
                ..MessageRendererBaseFields,
                "message",
            ],
            ["liveChatPlaceholderItemRenderer"] =
            [
                "id",
                "timestampUsec",
                "clickTrackingParams",
            ],
            // Loosely known — the model uses JsonObject fallback for these
            ["liveChatViewerEngagementMessageRenderer"] =
            [
                "id", "timestampUsec",
                "message", "actionButton", "icon",
                "trackingParams", "clickTrackingParams",
                // Observed on ~4% of instances (e.g. guidelines messages that are dismissable)
                "contextMenuEndpoint", "contextMenuAccessibility",
            ],
            ["liveChatModeChangeMessageRenderer"] =
            [
                "id", "timestampUsec",
                "text", "subtext", "icon",
                "trackingParams", "clickTrackingParams",
            ],

            // ── addLiveChatTickerItemAction outer item renderers ──────────────
            // Currently severely under-modeled: only id + showItemEndpoint are in the C# model.
            // Everything else should surface as NEW.
            ["liveChatTickerPaidMessageItemRenderer"] =
            [
                "id",
                "showItemEndpoint",
                "clickTrackingParams",
            ],
            ["liveChatTickerSponsorItemRenderer"] =
            [
                "id",
                "showItemEndpoint",
                "clickTrackingParams",
            ],
            ["liveChatTickerPaidStickerItemRenderer"] =
            [
                "id",
                "showItemEndpoint",
                "clickTrackingParams",
            ],
        };

    // ── Data structures ───────────────────────────────────────────────────────

    private sealed class FieldEntry
    {
        public int Count;
        public string Example = string.Empty;
    }

    private sealed class RendererStats
    {
        public string Location = string.Empty;
        public string RendererType = string.Empty;
        public int TotalCount;
        // All field names observed → count + one example value
        public readonly Dictionary<string, FieldEntry> Fields = new(StringComparer.Ordinal);
        // Badge composition
        public int BadgeCustomThumbnailCount;
        public int BadgeIconTypeCount;
        public int BadgeUnknownCount;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    public static int Run(string[] args)
    {
        List<string> paths = [];
        bool verbose = false;

        foreach (string arg in args)
        {
            if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
                continue;
            }
            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                return 0;
            }
            paths.Add(arg);
        }

        if (paths.Count == 0)
        {
            Console.WriteLine("No log paths provided. Enter one or more paths separated by ';' or ',':");
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                foreach (string segment in input.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
                {
                    string p = segment.Trim();
                    if (!string.IsNullOrWhiteSpace(p))
                        paths.Add(p);
                }
            }
        }

        if (paths.Count == 0)
        {
            PrintUsage();
            return 1;
        }

        // (location, rendererType) → stats
        // We use a stable insertion-order dictionary so sections print in encounter order
        Dictionary<string, RendererStats> stats = new(StringComparer.Ordinal);
        // Action type counts (for top-level summary)
        Dictionary<string, int> actionCounts = new(StringComparer.Ordinal);
        // Renderer types we have no baseline for at all
        HashSet<string> unknownRendererTypes = new(StringComparer.Ordinal);
        List<string> parseErrors = [];
        int totalActions = 0;

        foreach (string path in paths)
        {
            if (!File.Exists(path))
            {
                parseErrors.Add($"Missing file: {path}");
                continue;
            }

            try
            {
                foreach (JsonElement action in LogReader.ReadActions(path))
                {
                    totalActions++;
                    string? actionType = LogReader.GetActionType(action);
                    if (actionType == null)
                        continue;

                    Increment(actionCounts, actionType);

                    if (actionType == "addChatItemAction")
                    {
                        ProcessAddChatItem(action, stats, unknownRendererTypes);
                    }
                    else if (actionType == "addLiveChatTickerItemAction")
                    {
                        ProcessTickerItem(action, stats, unknownRendererTypes);
                    }
                }
            }
            catch (Exception ex)
            {
                parseErrors.Add($"{path}: {ex.Message}");
            }
        }

        // ── Print report ──────────────────────────────────────────────────────

        Console.WriteLine();
        WriteSectionHeader($"FIELD ANALYSIS REPORT — {paths.Count} file(s), {totalActions:N0} actions");
        Console.WriteLine();

        // Top-level action summary
        Console.WriteLine("Action type counts:");
        foreach (KeyValuePair<string, int> kv in actionCounts.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {kv.Value,7:N0}  {kv.Key}");
        Console.WriteLine();

        // Group stats by location
        IEnumerable<IGrouping<string, RendererStats>> groups = stats.Values
            .GroupBy(s => s.Location, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        bool anyNewFields = false;

        foreach (IGrouping<string, RendererStats> group in groups)
        {
            WriteSectionHeader($"Location: {group.Key}");
            Console.WriteLine();

            foreach (RendererStats rs in group.OrderByDescending(s => s.TotalCount))
            {
                bool hasBaseline = Baselines.TryGetValue(rs.RendererType, out HashSet<string>? baseline);
                Console.Write("  ");
                WriteColor(rs.RendererType, ConsoleColor.Cyan);
                Console.WriteLine($"  ({rs.TotalCount:N0} instances){(hasBaseline ? "" : "  [NO BASELINE]")}");

                // Sort fields: NEW first, then known alphabetically
                IEnumerable<KeyValuePair<string, FieldEntry>> ordered = rs.Fields
                    .OrderBy(f =>
                    {
                        bool known = hasBaseline && baseline!.Contains(f.Key);
                        return known ? 1 : 0; // NEW first
                    })
                    .ThenByDescending(f => f.Value.Count)
                    .ThenBy(f => f.Key, StringComparer.Ordinal);

                foreach (KeyValuePair<string, FieldEntry> field in ordered)
                {
                    bool known = hasBaseline && baseline!.Contains(field.Key);
                    double pct = rs.TotalCount > 0 ? field.Value.Count * 100.0 / rs.TotalCount : 0;
                    string tag = known ? "[known]" : "[NEW]  ";

                    Console.Write("    ");
                    if (!known)
                    {
                        WriteColor(tag, ConsoleColor.Yellow);
                        anyNewFields = true;
                    }
                    else if (verbose)
                    {
                        WriteColor(tag, ConsoleColor.DarkGray);
                    }
                    else
                    {
                        continue; // skip known fields in non-verbose mode
                    }

                    Console.Write($"  {field.Key,-40}  {field.Value.Count,6:N0}/{rs.TotalCount:N0}  ({pct,5:F1}%)");
                    if (!known)
                        Console.Write($"  eg: {field.Value.Example}");
                    Console.WriteLine();
                }

                // Report baseline fields never seen in this location
                if (hasBaseline && verbose)
                {
                    foreach (string knownField in baseline!.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        if (!rs.Fields.ContainsKey(knownField))
                        {
                            Console.Write("    ");
                            WriteColor("[missing]", ConsoleColor.DarkGray);
                            Console.WriteLine($"  {knownField,-40}  (never observed in this file)");
                        }
                    }
                }

                // Badge breakdown
                if (rs.BadgeCustomThumbnailCount + rs.BadgeIconTypeCount + rs.BadgeUnknownCount > 0)
                {
                    int total = rs.BadgeCustomThumbnailCount + rs.BadgeIconTypeCount + rs.BadgeUnknownCount;
                    Console.WriteLine($"    [badges]   customThumbnail={rs.BadgeCustomThumbnailCount:N0}  iconType={rs.BadgeIconTypeCount:N0}  unknown={rs.BadgeUnknownCount:N0}  (total badge instances={total:N0})");
                }

                Console.WriteLine();
            }
        }

        if (!anyNewFields && !verbose)
        {
            Console.WriteLine("  (No new fields detected. Run with --verbose to see all known fields.)");
            Console.WriteLine();
        }

        // Unknown renderer types
        WriteSectionHeader("Unknown Renderer Types (no baseline, not in known set)");
        Console.WriteLine();
        HashSet<string> allKnown = new(Baselines.Keys, StringComparer.Ordinal);
        bool anyUnknown = false;
        foreach (string rt in unknownRendererTypes.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!allKnown.Contains(rt))
            {
                anyUnknown = true;
                Console.Write("  ");
                WriteColor($"[UNKNOWN]  {rt}", ConsoleColor.Red);
                Console.WriteLine();
            }
        }
        if (!anyUnknown)
        {
            Console.WriteLine("  (none)");
        }
        Console.WriteLine();

        if (parseErrors.Count > 0)
        {
            WriteSectionHeader("Parse Errors");
            foreach (string e in parseErrors)
                Console.WriteLine($"  {e}");
            Console.WriteLine();
            return 2;
        }

        return 0;
    }

    // ── Action processors ─────────────────────────────────────────────────────

    private static void ProcessAddChatItem(
        JsonElement action,
        Dictionary<string, RendererStats> stats,
        HashSet<string> unknownRendererTypes)
    {
        if (!action.TryGetProperty("addChatItemAction", out JsonElement addChat) ||
            !addChat.TryGetProperty("item", out JsonElement item))
            return;

        if (!LogReader.TryGetSingleRenderer(item, out string? rendererType, out JsonElement rendererValue) ||
            rendererType == null)
            return;

        ObserveRenderer("addChatItemAction", rendererType, rendererValue, stats, unknownRendererTypes);
    }

    private static void ProcessTickerItem(
        JsonElement action,
        Dictionary<string, RendererStats> stats,
        HashSet<string> unknownRendererTypes)
    {
        if (!action.TryGetProperty("addLiveChatTickerItemAction", out JsonElement tickerAction) ||
            !tickerAction.TryGetProperty("item", out JsonElement tickerItem))
            return;

        // Outer item renderer (the ticker bar entry itself)
        if (LogReader.TryGetSingleRenderer(tickerItem, out string? outerRenderer, out JsonElement outerValue) &&
            outerRenderer != null)
        {
            ObserveRenderer("ticker.item", outerRenderer, outerValue, stats, unknownRendererTypes);
        }

        // Nested renderer inside showItemEndpoint → showLiveChatItemEndpoint → renderer
        if (LogReader.TryGetNestedShowRenderer(tickerItem, out string? nestedRenderer, out JsonElement nestedValue) &&
            nestedRenderer != null)
        {
            ObserveRenderer("ticker.showLiveChatItemEndpoint", nestedRenderer, nestedValue, stats, unknownRendererTypes);
        }
    }

    // ── Field observation ─────────────────────────────────────────────────────

    private static void ObserveRenderer(
        string location,
        string rendererType,
        JsonElement rendererValue,
        Dictionary<string, RendererStats> stats,
        HashSet<string> unknownRendererTypes)
    {
        string key = $"{location}:{rendererType}";
        if (!stats.TryGetValue(key, out RendererStats? rs))
        {
            rs = new RendererStats { Location = location, RendererType = rendererType };
            stats[key] = rs;
        }
        rs.TotalCount++;

        if (!Baselines.ContainsKey(rendererType))
            _ = unknownRendererTypes.Add(rendererType);

        if (rendererValue.ValueKind != JsonValueKind.Object)
            return;

        foreach (JsonProperty prop in rendererValue.EnumerateObject())
        {
            if (!rs.Fields.TryGetValue(prop.Name, out FieldEntry? entry))
            {
                entry = new FieldEntry
                {
                    Example = LogReader.SummarizeValue(prop.Value, maxLength: 80),
                };
                rs.Fields[prop.Name] = entry;
            }
            entry.Count++;

            // Track badge composition from authorBadges
            if (prop.Name == "authorBadges" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement badge in prop.Value.EnumerateArray())
                {
                    if (badge.TryGetProperty("liveChatAuthorBadgeRenderer", out JsonElement badgeRenderer))
                    {
                        if (badgeRenderer.TryGetProperty("customThumbnail", out _))
                            rs.BadgeCustomThumbnailCount++;
                        else if (badgeRenderer.TryGetProperty("icon", out _))
                            rs.BadgeIconTypeCount++;
                        else
                            rs.BadgeUnknownCount++;
                    }
                    else
                    {
                        rs.BadgeUnknownCount++;
                    }
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void WriteSectionHeader(string text)
    {
        string line = new('━', Math.Min(text.Length + 4, 72));
        WriteColor(line, ConsoleColor.DarkCyan);
        Console.WriteLine();
        WriteColor($"  {text}", ConsoleColor.DarkCyan);
        Console.WriteLine();
        WriteColor(line, ConsoleColor.DarkCyan);
        Console.WriteLine();
    }

    private static void WriteColor(string text, ConsoleColor color)
    {
        ConsoleColor prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = prev;
    }

    private static void Increment(Dictionary<string, int> dict, string key) =>
        dict[key] = dict.TryGetValue(key, out int v) ? v + 1 : 1;

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project YTLiveChat.Tools -- analyze [options] <logPath1> [logPath2 ...]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --verbose / -v    Show all fields (known + new + missing). Default: new fields only.");
        Console.WriteLine("  --help            Show this message.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project YTLiveChat.Tools -- analyze logs/watch_20260413_194235.jsonl");
        Console.WriteLine("  dotnet run --project YTLiveChat.Tools -- analyze --verbose logs/_old/*.json");
    }
}
