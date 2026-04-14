namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Represents the type of viewer engagement message.
/// </summary>
public enum EngagementMessageType
{
    /// <summary>The specific type could not be determined.</summary>
    Unknown,

    /// <summary>
    /// YouTube community guidelines reminder shown at the start of chat
    /// ("Welcome to live chat! Remember to guard your privacy and abide by our community guidelines.").
    /// </summary>
    CommunityGuidelines,

    /// <summary>
    /// Subscribers-only mode notification shown when the channel restricts chat
    /// to subscribers of a minimum duration.
    /// </summary>
    SubscribersOnly,

    /// <summary>
    /// Poll result summary shown after a poll closes (icon type "POLL").
    /// </summary>
    PollResult,
}

/// <summary>
/// Represents a YouTube viewer engagement message (<c>liveChatViewerEngagementMessageRenderer</c>),
/// such as community guidelines reminders, subscribers-only mode notices, or poll result summaries.
/// </summary>
public class EngagementItem
{
    /// <summary>Unique identifier for this engagement message.</summary>
    public required string Id { get; set; }

    /// <summary>Timestamp when the engagement message was issued.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The type of engagement message.</summary>
    public EngagementMessageType MessageType { get; set; } = EngagementMessageType.Unknown;

    /// <summary>
    /// Full message text, concatenated from all text runs.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Structured message parts (text segments) from the message runs.
    /// </summary>
    public MessagePart[] MessageParts { get; set; } = [];

    /// <summary>
    /// URL for a "Learn more" action button, if present.
    /// Typically points to a YouTube support page.
    /// </summary>
    public string? LearnMoreUrl { get; set; }
}
