using Microsoft.EntityFrameworkCore;
using SlackActionTracker.Data;
using SlackActionTracker.Domain;

namespace SlackActionTracker.Services;

public record WeeklyStats(int Created, int Completed, int Overdue, int StillActive);

public class ActionItemService
{
    private readonly AppDbContext _context;

    public ActionItemService(AppDbContext context)
    {
        _context = context;
    }

    // Returns items owned by or assigned to a user, sorted by priority then due date
    public async Task<List<ActionItem>> GetActiveItemsAsync(string userId)
    {
        return await _context.ActionItems
            .Where(a => (a.UserId == userId || a.AssigneeId == userId)
                     && a.Status == "active"
                     && a.Type != ActionItemType.Deadline)
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.DueDate.HasValue ? 0 : 1)
            .ThenBy(a => a.DueDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    // Returns all active items in a channel for the board view
    public async Task<List<ActionItem>> GetChannelActiveItemsAsync(string channelId)
    {
        return await _context.ActionItems
            .Where(a => a.ChannelId == channelId
                     && a.Status == "active"
                     && a.Type != ActionItemType.Deadline)
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.DueDate.HasValue ? 0 : 1)
            .ThenBy(a => a.DueDate)
            .ToListAsync();
    }

    // All distinct user IDs that have active items (owners + assignees)
    public async Task<List<string>> GetUsersWithActiveItemsAsync()
    {
        var owners = await _context.ActionItems
            .Where(a => a.Status == "active")
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync();

        var assignees = await _context.ActionItems
            .Where(a => a.Status == "active" && a.AssigneeId != null)
            .Select(a => a.AssigneeId!)
            .Distinct()
            .ToListAsync();

        return owners.Union(assignees).Distinct().ToList();
    }

    // All distinct channel IDs that had activity this week — for the weekly digest
    public async Task<List<string>> GetActiveChannelsThisWeekAsync()
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        return await _context.ActionItems
            .Where(a => a.CreatedAt >= weekAgo)
            .Select(a => a.ChannelId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<WeeklyStats> GetWeeklyStatsForChannelAsync(string channelId)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var items = await _context.ActionItems
            .Where(a => a.ChannelId == channelId && a.CreatedAt >= weekAgo)
            .ToListAsync();

        return new WeeklyStats(
            Created: items.Count,
            Completed: items.Count(a => a.Status == "completed"),
            Overdue: items.Count(a => a.IsOverdue),
            StillActive: items.Count(a => a.Status == "active")
        );
    }

    // Create from a parsed Slack message event
    public async Task<ActionItem?> TryCreateFromMessage(
        string userId, string channelId,
        string extractedText, string originalFullText,
        ActionItemType type, string eventId, string ts,
        string? dueDateText = null, DateTime? dueDate = null)
    {
        var exists = await _context.ActionItems.AnyAsync(a => a.SlackEventId == eventId);
        if (exists) return null;

        var item = new ActionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Priority = ActionPriority.Medium,
            Text = extractedText,
            FullMessageText = originalFullText,
            UserId = userId,
            ChannelId = channelId,
            SlackEventId = eventId,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            MessageTimestamp = ts,
            DueDateText = dueDateText,
            DueDate = dueDate
        };

        _context.ActionItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    // Create from a modal submission or slash command
    public async Task<ActionItem> CreateFromModalAsync(
        string userId, string channelId,
        string text, ActionItemType type,
        ActionPriority priority, string? assigneeId,
        DateTime? dueDate, string? dueDateText)
    {
        var item = new ActionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Priority = priority,
            Text = text,
            FullMessageText = text,
            UserId = userId,
            AssigneeId = assigneeId != userId ? assigneeId : null,
            ChannelId = channelId,
            SlackEventId = $"modal-{Guid.NewGuid()}",
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            DueDate = dueDate,
            DueDateText = dueDateText
        };

        _context.ActionItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<(bool Success, string Message)> UpdateStatusAsync(Guid itemId, string newStatus)
    {
        try
        {
            var item = await _context.ActionItems.FindAsync(itemId);

            if (item == null)
                return (false, ":x: *Item not found.* It may have already been deleted.");

            if (item.Status == newStatus)
                return (false, $":information_source: This item is already *{newStatus}*.");

            item.Status = newStatus;

            if (newStatus == "completed") item.CompletedAt = DateTime.UtcNow;
            else if (newStatus == "removed") item.RemovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, $":tada: Item marked as *{newStatus}*!");
        }
        catch (Exception)
        {
            return (false, ":warning: *Database error.* Couldn't update the item. Please try again.");
        }
    }
}
