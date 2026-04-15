namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Discriminates the type of a banner produced by <c>addBannerToLiveChatCommand</c>.
/// </summary>
public enum BannerType
{
    /// <summary>Unrecognised banner type.</summary>
    Unknown = 0,

    /// <summary>A pinned chat message (<c>LIVE_CHAT_BANNER_TYPE_PINNED_MESSAGE</c>).</summary>
    PinnedMessage = 1,

    /// <summary>
    /// A cross-channel redirect, shown when a stream ends and viewers are directed to another live
    /// (<c>LIVE_CHAT_BANNER_TYPE_CROSS_CHANNEL_REDIRECT</c>).
    /// </summary>
    CrossChannelRedirect = 2,

    /// <summary>
    /// An AI-generated chat summary banner (<c>LIVE_CHAT_BANNER_TYPE_CHAT_SUMMARY</c>).
    /// Shown periodically during long streams as an experimental YouTube feature.
    /// </summary>
    ChatSummary = 3,
}

/// <summary>
/// Base class for all banner types produced by <c>addBannerToLiveChatCommand</c>.
/// Pattern-match on <see cref="PinnedMessageBannerItem"/> or
/// <see cref="CrossChannelRedirectBannerItem"/> to access type-specific properties.
/// <code>
/// if (e.Banner is CrossChannelRedirectBannerItem redirect)
/// {
///     // redirect.RedirectChannelHandle, redirect.RedirectVideoId
/// }
/// else if (e.Banner is PinnedMessageBannerItem pinned)
/// {
///     // pinned.Author, pinned.Message, pinned.PinnedBy, ...
/// }
/// </code>
/// </summary>
public abstract class BannerItem
{
    /// <summary>
    /// The server-assigned action ID used to match a subsequent
    /// <c>removeBannerForLiveChatCommand</c> (see <see cref="Services.BannerRemovedEventArgs.TargetActionId"/>).
    /// </summary>
    public required string ActionId { get; set; }

    /// <summary>
    /// The kind of banner. Use this to decide which concrete subclass to cast to,
    /// or pattern-match directly on the subclass type.
    /// </summary>
    public BannerType BannerType { get; set; }
}

/// <summary>
/// A pinned chat message banner (<c>LIVE_CHAT_BANNER_TYPE_PINNED_MESSAGE</c>).
/// Produced when a channel owner or moderator pins a message in live chat.
/// </summary>
public sealed class PinnedMessageBannerItem : BannerItem
{
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

    /// <summary>Whether the author of the pinned message is a verified channel.</summary>
    public bool IsVerified { get; set; }

    /// <summary>Whether the author of the pinned message is a chat moderator.</summary>
    public bool IsModerator { get; set; }

    /// <summary>Whether the author of the pinned message is the channel owner.</summary>
    public bool IsOwner { get; set; }
}

/// <summary>
/// An AI-generated chat summary banner (<c>LIVE_CHAT_BANNER_TYPE_CHAT_SUMMARY</c>).
/// Shown periodically during long streams as an experimental YouTube feature. The summary
/// text is auto-generated from recent chat messages.
/// </summary>
public sealed class ChatSummaryBannerItem : BannerItem
{
    /// <summary>
    /// The server-assigned summary identifier (distinct from <see cref="BannerItem.ActionId"/>).
    /// </summary>
    public string? SummaryId { get; set; }

    /// <summary>
    /// The full <c>chatSummary.runs</c> content as structured message parts, preserving bold,
    /// deemphasized, and plain text runs. Typically contains: a bold title run, a newline,
    /// a deemphasized disclaimer run, a newline, and the body text run.
    /// Concatenate <see cref="TextPart.Text"/> values for a plain-text summary.
    /// </summary>
    public required MessagePart[] Summary { get; set; }
}

/// <summary>
/// A cross-channel stream redirect banner (<c>LIVE_CHAT_BANNER_TYPE_CROSS_CHANNEL_REDIRECT</c>).
/// Shown when a stream ends and viewers are prompted to follow along to another live stream,
/// or when another channel's viewers join via Squad streaming.
/// <para>
/// When a direct video destination is available (the "Go now" button variant), both
/// <see cref="RedirectChannelHandle"/> and <see cref="RedirectVideoId"/> are populated and you
/// can start a new <c>IYTLiveChat</c> session with <see cref="RedirectVideoId"/> or
/// <see cref="RedirectChannelHandle"/> immediately.
/// </para>
/// <para>
/// When only a "Learn more" link is available (Squad streaming join notification),
/// <see cref="RedirectVideoId"/> is null. Use <see cref="RedirectChannelHandle"/> to
/// look up the channel's current livestream separately.
/// </para>
/// </summary>
public sealed class CrossChannelRedirectBannerItem : BannerItem
{
    /// <summary>
    /// The @handle of the channel being redirected to (e.g. <c>"@TakanashiKiara"</c>).
    /// Extracted from the bold text run in the banner message.
    /// Pass directly to <c>IYTLiveChat.Start(handle: ...)</c> to follow the redirect.
    /// </summary>
    public required string RedirectChannelHandle { get; set; }

    /// <summary>
    /// The video ID of the destination livestream, or <see langword="null"/> when no specific
    /// video is indicated (e.g. Squad streaming join notifications show only a "Learn more" link).
    /// <para>
    /// When non-null, pass to <c>IYTLiveChat.Start(liveId: ...)</c> to connect directly.
    /// When null, use <see cref="RedirectChannelHandle"/> with <c>Start(handle: ...)</c> instead.
    /// </para>
    /// </summary>
    public string? RedirectVideoId { get; set; }

    /// <summary>
    /// Thumbnail image of the redirected channel's profile photo.
    /// </summary>
    public ImagePart? ChannelPhoto { get; set; }

    /// <summary>
    /// The full banner message as structured parts, e.g.:
    /// <list type="bullet">
    ///   <item><description>"Don't miss out! People are going to watch something from " + "@TakanashiKiara" (bold)</description></item>
    ///   <item><description>"@holoen_ceciliaimmergreen" (bold) + " and their viewers just joined. Say hello!"</description></item>
    /// </list>
    /// Concatenate <see cref="TextPart.Text"/> values for a plain-text summary.
    /// </summary>
    public required MessagePart[] BannerMessage { get; set; }
}
