namespace Cards.Web.Models;

public class TermValue
{
    public string Text { get; set; } = string.Empty;

    public string? ImageDataUrl { get; set; }

    public AudioStatus AudioStatus { get; set; } = AudioStatus.Pending;

    public string? AudioPath { get; set; }
}
