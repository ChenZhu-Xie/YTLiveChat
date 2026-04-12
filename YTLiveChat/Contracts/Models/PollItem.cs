namespace YTLiveChat.Contracts.Models;

/// <summary>
/// Represents one voting option in a live poll.
/// </summary>
public class PollChoice
{
    /// <summary>
    /// The display text of the choice.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Whether the authenticated viewer has selected this choice.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Fraction of total votes for this choice (0.0–1.0). Zero before any votes are cast.
    /// Multiply by 100 to get the percentage.
    /// </summary>
    public double VoteRatio { get; set; }
}

/// <summary>
/// Represents the current state of a live poll, produced by
/// <c>showLiveChatActionPanelAction</c> (initial open) and
/// <c>updateLiveChatPollAction</c> (live updates).
/// </summary>
public class PollItem
{
    /// <summary>
    /// The unique identifier for this poll.
    /// </summary>
    public required string PollId { get; set; }

    /// <summary>
    /// The poll question text, or null when the server sends an empty question field.
    /// </summary>
    public string? Question { get; set; }

    /// <summary>
    /// The handle of the creator who started the poll (e.g. "@ShirakamiFubuki").
    /// </summary>
    public string? CreatorHandle { get; set; }

    /// <summary>
    /// Total number of votes cast, parsed from the server's metadata text (e.g. "1,234 votes" → 1234).
    /// Null if the vote-count field is absent or cannot be parsed.
    /// </summary>
    public int? TotalVotes { get; set; }

    /// <summary>
    /// The poll choices in display order.
    /// </summary>
    public required IReadOnlyList<PollChoice> Choices { get; set; }

    /// <summary>
    /// True when produced by <c>showLiveChatActionPanelAction</c> (poll just opened);
    /// false when produced by <c>updateLiveChatPollAction</c> (vote-count update).
    /// </summary>
    public bool IsNew { get; set; }
}
