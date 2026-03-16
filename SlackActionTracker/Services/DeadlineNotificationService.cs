using SlackActionTracker.Domain;
using SlackActionTracker.UI;

namespace SlackActionTracker.Services;

public class DeadlineNotificationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeadlineNotificationService> _logger;

    public DeadlineNotificationService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<DeadlineNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[DeadlineNotification] Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            // Run every day at 09:00 UTC (1 hour after the daily digest)
            var nextRun = now.Hour < 9
                ? now.Date.AddHours(9)
                : now.Date.AddDays(1).AddHours(9);

            var delay = nextRun - now;
            _logger.LogInformation("[DeadlineNotification] Next run at {NextRun} UTC (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await SendDeadlineNotificationsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendDeadlineNotificationsAsync(CancellationToken ct)
    {
        _logger.LogInformation("[DeadlineNotification] Checking for items due today...");

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Find all active items due today, grouped by the responsible user
        var itemsDueToday = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                context.ActionItems.Where(a =>
                    a.Status == "active" &&
                    a.DueDate.HasValue &&
                    a.DueDate.Value >= today &&
                    a.DueDate.Value < tomorrow),
                ct);

        if (!itemsDueToday.Any())
        {
            _logger.LogInformation("[DeadlineNotification] No items due today.");
            return;
        }

        // Group by the responsible user (assignee if set, otherwise owner)
        var byUser = itemsDueToday
            .GroupBy(a => !string.IsNullOrEmpty(a.AssigneeId) ? a.AssigneeId : a.UserId);

        foreach (var group in byUser)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await SendDeadlineDmAsync(group.Key, group.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DeadlineNotification] Failed to notify user {UserId}", group.Key);
            }
        }

        _logger.LogInformation("[DeadlineNotification] Done. Notified {Count} users.", byUser.Count());
    }

    private async Task SendDeadlineDmAsync(string userId, List<ActionItem> items)
    {
        var count = items.Count;
        var headerText = count == 1
            ? ":alarm_clock: You have *1 action item due today*"
            : $":alarm_clock: You have *{count} action items due today*";

        var blocks = new List<object>
        {
            new {
                type = "header",
                text = new { type = "plain_text", text = "Due Today", emoji = true }
            },
            new {
                type = "section",
                text = new { type = "mrkdwn", text = headerText }
            },
            new { type = "divider" }
        };

        foreach (var item in items)
        {
            var assignedBy = !string.IsNullOrEmpty(item.AssigneeId) && item.AssigneeId != item.UserId
                ? $" · Assigned by <@{item.UserId}>"
                : "";

            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"{SlackMessageBuilder.GetEmoji(item.Type)} {SlackMessageBuilder.PriorityEmoji(item.Priority)} *{item.Text}*{assignedBy}"
                },
                accessory = new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Mark Done" },
                    style = "primary",
                    value = item.Id.ToString(),
                    action_id = "home_complete"
                }
            });
        }

        blocks.Add(new { type = "divider" });
        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = "_These items are due today. Mark them done or they will appear as overdue tomorrow._" }
            }
        });

        var client = _httpClientFactory.CreateClient("Slack");
        var payload = new
        {
            channel = userId,
            text = $"Reminder: {count} action item{(count == 1 ? "" : "s")} due today",
            blocks
        };

        await client.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
    }
}
