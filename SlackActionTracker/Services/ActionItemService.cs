using Microsoft.EntityFrameworkCore;
using SlackActionTracker.Data;
using SlackActionTracker.Domain;

namespace SlackActionTracker.Services;

public class ActionItemService
{
    private readonly AppDbContext _context;

    public ActionItemService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ActionItem>> GetActiveItemsAsync(string userId)
    {
        return await _context.ActionItems
            .Where(a => a.UserId == userId && a.Status == "active")
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task TryCreateFromMessage(string userId, string channelId, string extractedText, string originalFullText, ActionItemType type, string eventId, string ts)
    {
        var exists = await _context.ActionItems.AnyAsync(a => a.SlackEventId == eventId);
        if (exists) return;

        var item = new ActionItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Text = extractedText,
            FullMessageText = originalFullText,
            UserId = userId,
            ChannelId = channelId,
            SlackEventId = eventId,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            MessageTimestamp = ts
        };

        _context.ActionItems.Add(item);
        await _context.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> UpdateStatusAsync(Guid itemId, string newStatus)
    {
        try
        {
            var item = await _context.ActionItems.FindAsync(itemId);

            if (item == null)
                return (false, ":x: *Item not found.* It might have been deleted or already completed.");

            if (item.Status == newStatus)
                return (false, $":information_source: This item is already *{newStatus}*.");

            item.Status = newStatus;

            if (newStatus == "completed")
                item.CompletedAt = DateTime.UtcNow;
            else if (newStatus == "removed")
                item.RemovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, $":tada: Item marked as *{newStatus}*!");
        }
        catch (Exception)
        {
            return (false, "*Database error.* I couldn't update the item. Please try again.");
        }
    }
}