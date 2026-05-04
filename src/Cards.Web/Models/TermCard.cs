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
    /// Single image for the card. New cards write the picture into <see cref="Value1"/>.
    /// Falls back to <see cref="Value2"/> for legacy cards that stored it on the second value.
    /// </summary>
    [JsonIgnore]
    public string? ImageDataUrl => Value1.ImageDataUrl ?? Value2.ImageDataUrl;
}
