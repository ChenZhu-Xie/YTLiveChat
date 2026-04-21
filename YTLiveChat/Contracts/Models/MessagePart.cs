namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Base class for individual message parts
/// </summary>
public abstract class MessagePart { }

/// <summary>
/// Image variant of a message part
/// </summary>
public class ImagePart : MessagePart
{
    /// <summary>
    /// URL of the image
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Alt string of the image
    /// </summary>
    public string? Alt { get; set; }

    /// <summary>
    /// Create a quasi json representation of an ImagePart
    /// </summary>
    /// <returns>String representation of Image in quasi json</returns>
    public override string ToString() => $"{{Image: {{Alt: {Alt}, Url: {Url}}}}}";
}

/// <summary>
/// Emoji variant of a message part
/// </summary>
public class EmojiPart : ImagePart
{
    /// <summary>
    /// Text representation of the emoji
    /// </summary>
    public required string EmojiText { get; set; }

    /// <summary>
    /// Whether or not Emoji is a custom emoji of the channel
    /// </summary>
    public bool IsCustomEmoji { get; set; }

    /// <summary>
    /// Create a quasi json representation of an EmojiPart
    /// </summary>
    /// <returns>String representation of an Emoji in quasi json</returns>
    public override string ToString() =>
        $"{{Emoji: {{EmojiText: {EmojiText}, Alt: {Alt}, Url: {Url}, IsCustomEmoji: {IsCustomEmoji}}}}}";
};

/// <summary>
/// Text run variant of a message part, carrying the raw text and optional inline formatting flags.
/// Use <see cref="Text"/> for plain-text rendering; check <see cref="Bold"/>, <see cref="Italics"/>,
/// and <see cref="IsDeemphasized"/> for rich rendering. All flags default to <see langword="false"/>.
/// </summary>
public class TextPart : MessagePart
{
    /// <summary>
    /// Contained text of the message
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Whether this run is rendered in bold.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Whether this run is rendered in italics.
    /// </summary>
    public bool Italics { get; set; }

    /// <summary>
    /// Whether this run is rendered in a deemphasized (subdued) style,
    /// e.g. the disclaimer line in an AI-generated chat summary banner.
    /// </summary>
    public bool IsDeemphasized { get; set; }

    /// <summary>
    /// Per-run text color as a 6-digit uppercase hex string (e.g. <c>"FFFFFF"</c>), or
    /// <see langword="null"/> when the run carries no explicit color. Currently observed
    /// on <c>liveChatBannerRedirectRenderer</c> message runs.
    /// </summary>
    public string? TextColor { get; set; }

    /// <summary>
    /// Return the text
    /// </summary>
    /// <returns>string representation of TextPart</returns>
    public override string ToString() => Text;
}
