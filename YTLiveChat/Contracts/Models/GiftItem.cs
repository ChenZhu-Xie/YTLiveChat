namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Represents a virtual gift sent by a viewer using YouTube Jewels.
/// Produced by <c>giftMessageViewModel</c> in an <c>addChatItemAction</c>.
/// </summary>
/// <remarks>
/// YouTube Jewels is a virtual gifting system distinct from gift memberships
/// (<c>liveChatSponsorshipsGiftPurchaseAnnouncementRenderer</c>). Viewers spend
/// Jewels (purchased with real money) to send named gift items to the streamer.
/// </remarks>
public class GiftItem
{
    /// <summary>
    /// Unique identifier for this gift action.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The sender's @handle (e.g. <c>"@yaniescobar2170"</c>).
    /// </summary>
    public required string AuthorHandle { get; set; }

    /// <summary>
    /// Pre-formatted description as supplied by YouTube,
    /// e.g. <c>"sent Gold coin for 10 Jewels"</c>.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Client-side image name for the gift icon (e.g. <c>"GIFT"</c>).
    /// This is a symbol identifier, not a URL — render it using YouTube's
    /// client resource system or treat it as a display label.
    /// </summary>
    public string? GiftImageName { get; set; }

    /// <summary>
    /// ARGB color tint of the gift icon as a 6-digit uppercase hex string
    /// (e.g. <c>"FF0000"</c>), or <see langword="null"/> when absent.
    /// </summary>
    public string? GiftImageColor { get; set; }
}
