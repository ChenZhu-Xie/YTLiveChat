using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging.Abstractions;

using YTLiveChat.Services;

/// <summary>
/// Snapshot mode: fetches one or more YouTube channel/video pages using the library's own
/// HTTP client (which handles the consent interstitial fallback) and saves the raw HTML to
/// files suitable for use as test fixtures in YTLiveChat.Tests/TestData/WebSnapshots/.
/// </summary>
internal static class SnapshotMode
{
    private static readonly string[] s_allPageTypes = ["home", "streams", "join", "membership", "live"];

    public static async Task<int> RunAsync(string[] args)
    {
        SnapshotOptions options = ParseSnapshotOptions(args);

        if (options.Target is null)
        {
            PrintSnapshotUsage();
            return 1;
        }

        string? handle = options.Target.StartsWith("@", StringComparison.Ordinal)
            ? options.Target
            : null;
        string? channelId = !options.Target.StartsWith("@", StringComparison.Ordinal)
            && options.Target.StartsWith("UC", StringComparison.OrdinalIgnoreCase)
            ? options.Target
            : null;
        string? liveId = handle is null && channelId is null ? options.Target : null;

        string outputDir = options.OutputDir
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "YTLiveChat.Tests", "TestData", "WebSnapshots"
            ));
        Directory.CreateDirectory(outputDir);

        string date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        string safeTag = string.Concat((options.Target ?? "unknown").Select(c =>
            char.IsLetterOrDigit(c) ? c : '_'));

        using HttpClient httpClient = new() { BaseAddress = new Uri("https://www.youtube.com") };
        YTHttpClient ytHttpClient = new(httpClient, NullLogger<YTHttpClient>.Instance);

        IReadOnlyList<string> pages = options.Pages.Count > 0 ? options.Pages : s_allPageTypes;

        foreach (string page in pages)
        {
            string fileName = $"{safeTag}.{page}.{date}.html";
            string filePath = Path.Combine(outputDir, fileName);

            Console.Write($"Fetching /{page} ... ");

            try
            {
                string html = page switch
                {
                    "home" => await ytHttpClient.GetChannelPageAsync(handle, channelId).ConfigureAwait(false),
                    "streams" => await ytHttpClient.GetStreamsPageAsync(handle, channelId).ConfigureAwait(false),
                    "join" => await ytHttpClient.GetJoinPageAsync(handle, channelId).ConfigureAwait(false),
                    "membership" => await ytHttpClient.GetMembershipPageAsync(handle, channelId).ConfigureAwait(false),
                    "live" => await ytHttpClient.GetOptionsAsync(handle, channelId, liveId).ConfigureAwait(false),
                    _ => throw new ArgumentException($"Unknown page type: {page}")
                };

                await File.WriteAllTextAsync(filePath, html, new UTF8Encoding(false)).ConfigureAwait(false);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"OK ({html.Length:N0} chars)");
                Console.ResetColor();
                Console.WriteLine($" → {filePath}");

                if (options.Diagnose)
                {
                    DiagnoseHtml(html, page);
                }

                if (options.FetchOffers && page == "join")
                {
                    await FetchAndSaveOffersAsync(html, safeTag, date, outputDir, ytHttpClient).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.ResetColor();
            }
        }

        return 0;
    }

    private static (string? ItemParams, string? ApiKey, string? ClientVersion) ExtractOffersParams(string html)
    {
        Match apiKeyMatch = Regex.Match(html, @"""INNERTUBE_API_KEY""\s*:\s*""([^""]+)""");
        string? apiKey = apiKeyMatch.Success ? apiKeyMatch.Groups[1].Value : null;

        Match verMatch = Regex.Match(html, @"""INNERTUBE_CONTEXT_CLIENT_VERSION""\s*:\s*""([^""]+)""");
        string? clientVersion = verMatch.Success ? verMatch.Groups[1].Value : null;

        // Extract ypcGetOffersEndpoint.params from ytInitialData JSON
        string? itemParams = null;
        int dataIdx = html.IndexOf("ytInitialData", StringComparison.Ordinal);
        if (dataIdx >= 0)
        {
            // Find the JSON value start after the assignment
            int braceIdx = html.IndexOf('{', dataIdx);
            if (braceIdx >= 0)
            {
                // Simple pattern search — more reliable than full JSON parse on huge HTML
                Match paramsMatch = Regex.Match(
                    html[braceIdx..],
                    @"""ypcGetOffersEndpoint""\s*:\s*\{[^}]*""params""\s*:\s*""([^""]+)"""
                );
                if (paramsMatch.Success)
                    itemParams = paramsMatch.Groups[1].Value;
            }
        }

        return (itemParams, apiKey, clientVersion);
    }

    private static async Task FetchAndSaveOffersAsync(
        string joinHtml,
        string safeTag,
        string date,
        string outputDir,
        YTHttpClient ytHttpClient)
    {
        (string? itemParams, string? apiKey, string? clientVersion) = ExtractOffersParams(joinHtml);

        if (string.IsNullOrWhiteSpace(itemParams) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(clientVersion))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("  [offers] No ypcGetOffersEndpoint.params found — skipping get_offers call.");
            Console.ResetColor();
            return;
        }

        Console.Write($"  [offers] Calling get_offers (params={itemParams.Length} chars) ... ");
        try
        {
            string offersJson = await ytHttpClient
                .PostGetOffersAsync(apiKey, clientVersion, itemParams)
                .ConfigureAwait(false);

            string offersFileName = $"{safeTag}.get_offers.{date}.json";
            string offersFilePath = Path.Combine(outputDir, offersFileName);

            // Pretty-print for readability
            using JsonDocument doc = JsonDocument.Parse(offersJson);
            string prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(offersFilePath, prettyJson, new UTF8Encoding(false)).ConfigureAwait(false);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"OK ({offersJson.Length:N0} chars)");
            Console.ResetColor();
            Console.WriteLine($" → {offersFilePath}");

            // Print top-level structure for quick inspection
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("  [offers] Top-level keys: " + string.Join(", ",
                doc.RootElement.EnumerateObject().Select(p => p.Name)));
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void DiagnoseHtml(string html, string page)
    {
        string[] keys =
        [
            "sponsorshipsTierRenderer",
            "sponsorshipsOfferRenderer",
            "sponsorshipsPerksRenderer",
            "sponsorshipsPerkRenderer",
            "ypcGetOffersEndpoint",
            "hasYpcMetadata",
            "ypcTrailerRenderer",
            "membershipButton",
            "joinButton",
            "offerButtonRenderer",
            "joinMemberships",
            "gridSponsor",
            "sponsorBadge",
            "INNERTUBE_API_KEY",
            "ytInitialData",
        ];

        bool anyFound = false;
        foreach (string key in keys)
        {
            if (html.Contains(key, StringComparison.Ordinal))
            {
                if (!anyFound)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"  [{page}] Keys found:");
                    Console.ResetColor();
                    anyFound = true;
                }
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"    + {key}");
                Console.ResetColor();
            }
        }

        if (!anyFound)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [{page}] No membership/offer keys found.");
            Console.ResetColor();
        }
    }

    private static SnapshotOptions ParseSnapshotOptions(string[] args)
    {
        string? target = null;
        string? outputDir = null;
        List<string> pages = [];
        bool diagnose = false;
        bool fetchOffers = false;

        foreach (string arg in args)
        {
            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintSnapshotUsage();
                Environment.Exit(0);
            }

            if (arg.Equals("--diagnose", StringComparison.OrdinalIgnoreCase))
            {
                diagnose = true;
                continue;
            }

            if (arg.Equals("--fetch-offers", StringComparison.OrdinalIgnoreCase))
            {
                fetchOffers = true;
                // Ensure join page is included when --fetch-offers is used
                continue;
            }

            const string pagesPrefix = "--pages=";
            if (arg.StartsWith(pagesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string p in arg[pagesPrefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    pages.Add(p.Trim().ToLowerInvariant());
                }
                continue;
            }

            const string outputPrefix = "--output-dir=";
            if (arg.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                outputDir = arg[outputPrefix.Length..];
                continue;
            }

            target ??= arg;
        }

        // --fetch-offers implicitly requires the join page
        if (fetchOffers && pages.Count > 0 && !pages.Contains("join"))
        {
            pages = [.. pages, "join"];
        }

        return new SnapshotOptions(target, pages, outputDir, diagnose, fetchOffers);
    }

    public static void PrintSnapshotUsage()
    {
        Console.WriteLine("━━━ snapshot ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("  dotnet run --project YTLiveChat.Tools -- snapshot <@handle|UCxxx|liveId> [options]");
        Console.WriteLine();
        Console.WriteLine("  Fetches YouTube channel/video pages and saves HTML snapshots as test fixtures.");
        Console.WriteLine("  Uses the library's own HTTP client with consent-interstitial handling.");
        Console.WriteLine();
        Console.WriteLine("  Options:");
        Console.WriteLine("    --pages=<list>         Comma-separated pages to fetch. Default: all.");
        Console.WriteLine("                           Values: home, streams, join, membership, live");
        Console.WriteLine("    --output-dir=<path>    Output directory. Default: YTLiveChat.Tests/TestData/WebSnapshots/");
        Console.WriteLine("    --diagnose             After saving, print which membership/offer keys are present.");
        Console.WriteLine("    --fetch-offers         After saving the join page, call get_offers and save the raw JSON.");
        Console.WriteLine("                           Implicitly adds 'join' to --pages if not already included.");
        Console.WriteLine();
        Console.WriteLine("  Examples:");
        Console.WriteLine("    dotnet run --project YTLiveChat.Tools -- snapshot @HakosBaelz --diagnose");
        Console.WriteLine("    dotnet run --project YTLiveChat.Tools -- snapshot @HakosBaelz --pages=join --fetch-offers");
        Console.WriteLine("    dotnet run --project YTLiveChat.Tools -- snapshot @Alofokeradioshow --pages=join --fetch-offers");
        Console.WriteLine("    dotnet run --project YTLiveChat.Tools -- snapshot @AkiRosenthal --pages=live");
    }

    private sealed record SnapshotOptions(
        string? Target,
        IReadOnlyList<string> Pages,
        string? OutputDir,
        bool Diagnose,
        bool FetchOffers
    );
}
