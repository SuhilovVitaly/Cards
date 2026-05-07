using System.Text.Json.Serialization;

namespace Cards.Web.Models;

public class TermCard
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public Language Language1 { get; set; }

    public Language Language2 { get; set; }

    public TermValue Value1 { get; set; } = new();

    public TermValue Value2 { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the last time the card creator viewed this card during training
    /// (i.e. pressed "Next"). Used as the basis for spaced repetition scheduling.
    /// </summary>
    public DateTime? LastViewedAt { get; set; }

    /// <summary>
    /// Current SRS memorization level. New cards start at <see cref="SrsLevel.Level1"/>.
    /// </summary>
    public SrsLevel SrsLevel { get; set; } = SrsLevel.Level1;

    /// <summary>
    /// Number of correct attempts accumulated toward advancing to the next SRS level.
    /// Incremented each time the user presses "Next" without first pressing "Show translation".
    /// </summary>
    public int CorrectAttempts { get; set; }

    /// <summary>
    /// Single image for the card. New cards write the picture into <see cref="Value1"/>.
    /// Falls back to <see cref="Value2"/> for legacy cards that stored it on the second value.
    /// </summary>
    [JsonIgnore]
    public string? ImageDataUrl => Value1.ImageDataUrl ?? Value2.ImageDataUrl;
}
