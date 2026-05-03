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
}
