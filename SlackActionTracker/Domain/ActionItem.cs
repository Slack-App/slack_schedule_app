namespace SlackActionTracker.Domain;

public enum ActionItemType
{
    Commitment,
    Request,
    Deadline,
    Question,
}
public class ActionItem
{
    public Guid Id { get; set; }
    public ActionItemType Type { get; set; }
    public string Text { get; set; } = default!;

    public string FullMessageText { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string ChannelId { get; set; } = default!;
    public string SlackEventId { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string Status { get; set; } = "active"; // active, completed, removed
    public string? MessageTimestamp { get; set; } 
}