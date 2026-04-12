namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Represents a pinned-message banner, produced by <c>addBannerToLiveChatCommand</c>.
/// </summary>
public class BannerItem
{
    /// <summary>
    /// The server-assigned action ID (used to match a subsequent <c>removeBannerForLiveChatCommand</c>).
    /// </summary>
    public required string ActionId { get; set; }

    /// <summary>
    /// The raw banner type string (e.g. "LIVE_CHAT_BANNER_TYPE_PINNED_MESSAGE").
    /// </summary>
    public string? BannerType { get; set; }

    /// <summary>
    /// The concatenated "Pinned by @handle" text from the banner header, or null if absent.
    /// </summary>
    public string? PinnedBy { get; set; }

    /// <summary>
    /// The author of the pinned message.
    /// </summary>
    public required Author Author { get; set; }

    /// <summary>
    /// The content of the pinned message.
    /// </summary>
    public required MessagePart[] Message { get; set; }

    /// <summary>
    /// The original chat item ID of the pinned message.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp of the pinned message.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
