namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Represents a single membership tier for a YouTube channel.
/// Returned by <see cref="Services.IYTLiveChat.GetMembershipTiersAsync"/>.
/// </summary>
public class MembershipTier
{
    /// <summary>
    /// Tier display name as shown to viewers (e.g. <c>"Rat Bro"</c>, <c>"Rat Boss!"</c>).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Pre-formatted price string as supplied by YouTube (e.g. <c>"CHF 5.95/month"</c>).
    /// Includes the currency symbol and billing period; exact format depends on the viewer's locale.
    /// </summary>
    public required string PriceText { get; set; }

    /// <summary>
    /// Perks exclusive to this tier (does not include perks inherited from lower tiers).
    /// </summary>
    public IReadOnlyList<MembershipPerk> Perks { get; set; } = [];

    /// <summary>
    /// Badge image URLs awarded to members at each loyalty milestone.
    /// YouTube awards different badges at 1, 2, 6, 12, 24, and 36+ months.
    /// URLs are <c>yt3.ggpht.com</c> thumbnails; the <c>=s64-k-nd</c> suffix requests 64 px.
    /// </summary>
    public IReadOnlyList<string> BadgeImageUrls { get; set; } = [];

    /// <summary>
    /// Custom emoji available to members of this tier.
    /// </summary>
    public IReadOnlyList<MembershipEmoji> CustomEmojis { get; set; } = [];
}

/// <summary>
/// A single membership perk (benefit) belonging to a <see cref="MembershipTier"/>.
/// </summary>
public class MembershipPerk
{
    /// <summary>
    /// Perk title as shown in the membership offer dialog
    /// (e.g. <c>"Loyalty badges next to your name in comments and live chat"</c>).
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Optional perk description (e.g. <c>"Behold eye perms"</c>).
    /// Null when the channel did not provide additional text for this perk.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// A custom emoji available to members, extracted from the membership offer.
/// </summary>
public class MembershipEmoji
{
    /// <summary>
    /// Emoji name / accessibility label as set by the channel (e.g. <c>"wazzup"</c>, <c>"BRUH"</c>).
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Thumbnail URL for the emoji image. Prefer the 48 px variant when available
    /// (URL contains <c>=w48-h48</c>); the 24 px variant (<c>=w24-h24</c>) is the fallback.
    /// </summary>
    public required string ImageUrl { get; set; }
}
