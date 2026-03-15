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
    public ActionPriority Priority { get; set; } = ActionPriority.Medium;

    public string Text { get; set; } = default!;
    public string FullMessageText { get; set; } = default!;

    public string UserId { get; set; } = default!;
    public string? AssigneeId { get; set; }

    public string ChannelId { get; set; } = default!;
    public string SlackEventId { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? RemovedAt { get; set; }

    public string Status { get; set; } = "active";
    public string? MessageTimestamp { get; set; }
    public string? DueDateText { get; set; }

    // Computed — no DB column needed
    public bool IsOverdue => Status == "active" && DueDate.HasValue && DueDate.Value.ToUniversalTime() < DateTime.UtcNow;
}
